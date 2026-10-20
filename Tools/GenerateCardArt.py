#!/usr/bin/env python3
"""Card illustrations: one picture per card, so cards no longer share the type placeholder (sword, shield, orb).
Each card is a recipe on a 44x32 pixel canvas (the aspect of the frame's art window), saved x4 (176x128) in
Assets/Resources/Game/Cards/Art/<card id>.png like the two original status illustrations:
  * a dithered backdrop: palette ramp, light spot, vignette and dust (4x4 Bayer, like the status art);
  * Dungeon Crawl Stone Soup tiles (CC0, the same library the original placeholders come from), read from the
    Python repository: items drawn as sprites, spell icons cropped from their bevel and screened over the backdrop;
  * procedural effects: slashes, impacts, glows, sparks, speed lines, drops, cracks, waves, bubbles, dice, chains.
The original art files (status_espora, status_moho) are never touched. Cards/layout.json gets the full "art" list.
Usage:
  python Tools/GenerateCardArt.py                     writes every illustration (+ .meta if missing) and layout.json
  python Tools/GenerateCardArt.py --only a_tajo,m_rayo
  python Tools/GenerateCardArt.py --preview DIR       also writes labelled contact sheets in DIR
  python Tools/GenerateCardArt.py --check             regenerates in memory and compares with the files (exit 1 on difference)
  --source-dir PY_REPO                                the Python repository (default ../../python/last-wish from the workspace)
Exit codes: 0 ok, 1 differences or validation failure, 2 bad usage or missing tiles.
"""
import argparse
import hashlib
import json
import math
import os
import sys
import zlib
try:
    from PIL import Image, ImageDraw
except ImportError:  # pragma: no cover
    sys.stderr.write("Pillow 11 es necesario: python -m pip install pillow==11.0.0\n")
    sys.exit(2)

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
GAME = os.path.join(ROOT, "Assets", "Resources", "Game")
OUT_DIR = os.path.join(GAME, "Cards", "Art")
LAYOUT = os.path.join(GAME, "Cards", "layout.json")
CARDS_JSON = os.path.join(GAME, "Data", "cards.json")
ORIGINAL_ART = {"status_espora", "status_moho"}
META_TEMPLATE = "status_moho.png.meta"
DEFAULT_SOURCE = os.path.normpath(os.path.join(ROOT, "..", "..", "python", "last-wish"))
DCSS = os.path.join("assets", "dungeon-crawl-stone-soup-full")

W, H, SCALE = 44, 32, 4
BAYER = [[0, 8, 2, 10], [12, 4, 14, 6], [3, 11, 1, 9], [15, 7, 13, 5]]


def hash01(x, y, s=0):
    h = (x * 374761393 + y * 668265263 + s * 982451653) & 0xFFFFFFFF
    h = ((h ^ (h >> 13)) * 1274126177) & 0xFFFFFFFF
    return ((h ^ (h >> 16)) & 0xFFFF) / 65535.0


def mix(a, b, t):
    return tuple(int(round(a[i] + (b[i] - a[i]) * t)) for i in range(3))


def bayer(x, y):
    return BAYER[y & 3][x & 3] / 16.0


def clamp(v, lo=0.0, hi=1.0):
    return lo if v < lo else hi if v > hi else v


# ------------------------------------------------------------------ colours
WHITE = (255, 255, 255)
STEEL = (170, 200, 255)
RED = (225, 45, 40)
BLOOD = (150, 14, 20)
ORANGE = (255, 150, 40)
YELLOW = (255, 232, 110)
GOLD = (250, 200, 70)
CYAN = (110, 235, 255)
BLUE = (80, 150, 255)
GREEN = (120, 235, 90)
TOXIC = (90, 210, 60)
PURPLE = (190, 110, 255)
PINK = (255, 120, 210)
BROWN = (120, 84, 46)
DARK = (12, 10, 14)

# backdrop palettes: (dark, mid, light)
PALETTES = {
    "steel": ((10, 12, 18), (38, 44, 60), (112, 126, 152)),
    "stone": ((12, 11, 10), (44, 41, 38), (122, 114, 102)),
    "blood": ((14, 4, 6), (60, 14, 18), (155, 42, 40)),
    "ember": ((16, 6, 4), (68, 24, 10), (212, 102, 32)),
    "water": ((4, 10, 22), (16, 42, 80), (72, 152, 212)),
    "earth": ((14, 10, 6), (54, 37, 20), (152, 110, 62)),
    "air": ((6, 16, 20), (26, 58, 64), (132, 202, 202)),
    "poison": ((6, 12, 6), (20, 46, 18), (92, 172, 62)),
    "night": ((8, 8, 16), (30, 27, 54), (102, 92, 162)),
    "shadow": ((6, 4, 10), (27, 15, 38), (92, 52, 124)),
    "arcane": ((6, 6, 22), (24, 25, 78), (102, 112, 232)),
    "gold": ((14, 10, 4), (58, 43, 14), (212, 162, 62)),
    "forest": ((6, 12, 6), (22, 43, 20), (112, 162, 82)),
    "royal": ((12, 4, 16), (45, 16, 58), (172, 82, 212)),
}


# ------------------------------------------------------------------ canvas
class Pic:
    def __init__(self):
        self.px = [(0, 0, 0)] * (W * H)

    def get(self, x, y):
        return self.px[y * W + x]

    def put(self, x, y, c, a=1.0):
        x, y = int(math.floor(x)), int(math.floor(y))
        if not (0 <= x < W and 0 <= y < H) or a <= 0:
            return
        if a >= 1:
            self.px[y * W + x] = tuple(int(v) for v in c[:3])
        else:
            self.px[y * W + x] = mix(self.px[y * W + x], c, a)

    def add(self, x, y, c, a=1.0):
        x, y = int(math.floor(x)), int(math.floor(y))
        if not (0 <= x < W and 0 <= y < H) or a <= 0:
            return
        o = self.px[y * W + x]
        self.px[y * W + x] = tuple(min(255, int(round(o[i] + c[i] * a))) for i in range(3))

    def screen(self, x, y, c, a=1.0):
        x, y = int(math.floor(x)), int(math.floor(y))
        if not (0 <= x < W and 0 <= y < H) or a <= 0:
            return
        o = self.px[y * W + x]
        s = tuple(255 - (255 - o[i]) * (255 - c[i]) / 255.0 for i in range(3))
        self.px[y * W + x] = mix(o, s, min(1.0, a))

    def image(self):
        im = Image.new("RGBA", (W, H))
        im.putdata([(r, g, b, 255) for (r, g, b) in self.px])
        return im.resize((W * SCALE, H * SCALE), Image.NEAREST)


def backdrop(p, pal, light, seed):
    dark, mid, lit = PALETTES[pal]
    ramp = [mix((0, 0, 0), dark, 0.7), dark, mix(dark, mid, 0.5), mid, mix(mid, lit, 0.35), mix(mid, lit, 0.62)]
    lx, ly = light
    n = len(ramp)
    for y in range(H):
        for x in range(W):
            d = math.hypot((x + 0.5 - lx) / 25.0, (y + 0.5 - ly) / 19.0)
            t = 0.1 + 0.14 * (1 - y / (H - 1.0)) + 0.8 * max(0.0, 1 - d) ** 1.3
            v = ((x + 0.5 - W / 2.0) / (W / 2.0)) ** 2 + ((y + 0.5 - H / 2.0) / (H / 2.0)) ** 2
            t -= 0.16 * v
            f = clamp(t) * (n - 1) + (bayer(x, y) - 0.47) * 0.95
            i = max(0, min(n - 1, int(round(f))))
            if t > 0.12 and hash01(x, y, seed) > 0.978:
                i = min(n - 1, i + 1)
            p.put(x, y, ramp[i])


# ------------------------------------------------------------------ Dungeon Crawl tiles
class Tiles:
    def __init__(self, root):
        self.root = root
        self.cache = {}
        self.used = set()

    def load(self, name):
        if name not in self.cache:
            path = os.path.join(self.root, *name.split("/")) + ".png"
            if not os.path.exists(path):
                raise FileNotFoundError("falta el tile de Dungeon Crawl: " + name)
            self.cache[name] = Image.open(path).convert("RGBA")
        self.used.add(name)
        return self.cache[name]


TILES = None  # set in main


def _prepare(name, flip=False, vflip=False, rot=0, icon=False, key=False):
    im = TILES.load(name)
    if icon:
        # spell/ability icons: drop the transparent margin and the 1 px grey bevel
        x0, y0, x1, y1 = im.getchannel("A").getbbox()
        im = im.crop((x0 + 1, y0 + 1, x1 - 1, y1 - 1))
    if key:
        im = _key_black(im)
    if flip:
        im = im.transpose(Image.FLIP_LEFT_RIGHT)
    if vflip:
        im = im.transpose(Image.FLIP_TOP_BOTTOM)
    if rot:
        im = im.transpose({90: Image.ROTATE_270, 180: Image.ROTATE_180, 270: Image.ROTATE_90}[rot])
    return im


def _key_black(im, limit=22):
    """Flood-fills the near-black background from the borders (keeps black outlines inside the figure)."""
    im = im.copy()
    w, h = im.size
    px = im.load()
    seen = set()
    stack = [(x, y) for x in range(w) for y in (0, h - 1)] + [(x, y) for y in range(h) for x in (0, w - 1)]
    while stack:
        x, y = stack.pop()
        if (x, y) in seen or not (0 <= x < w and 0 <= y < h):
            continue
        seen.add((x, y))
        r, g, b, a = px[x, y]
        if a == 0 or max(r, g, b) <= limit:
            px[x, y] = (0, 0, 0, 0)
            stack += [(x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)]
    return im


def S(p, name, x=6, y=0, flip=False, vflip=False, rot=0, alpha=1.0, tint=None, fill=None, halo=None, mode="normal"):
    """Draws a tile with its top-left at (x, y). tint multiplies, fill paints a silhouette, halo=(colour, alpha)
    rims the opaque shape with 1 px of light, mode is normal | add | screen."""
    im = _prepare(name, flip, vflip, rot)
    _blit(p, im, x, y, alpha, tint, fill, halo, mode)


def icon(p, name, cx=22, cy=16, mode="screen", alpha=1.0, key=False, fade=3, flip=False):
    """Spell icon content (26x26 without the bevel) centred on (cx, cy). Screened icons fade near their square edge."""
    im = _prepare(name, flip=flip, icon=True, key=key)
    w, h = im.size
    x0, y0 = int(round(cx - w / 2.0)), int(round(cy - h / 2.0))
    if mode == "screen":
        px = im.load()
        for j in range(h):
            for i in range(w):
                r, g, b, a = px[i, j]
                e = min(i, j, w - 1 - i, h - 1 - j)
                k = 1.0 if e >= fade else (e + 0.5) / (fade + 0.5)
                if a:
                    p.screen(x0 + i, y0 + j, (r, g, b), alpha * k * a / 255.0)
    else:
        _blit(p, im, x0, y0, alpha, None, None, None, "normal")


def _blit(p, im, x, y, alpha, tint, fill, halo, mode):
    w, h = im.size
    px = im.load()
    if halo:
        col, ha = halo
        solid = {(i, j) for j in range(h) for i in range(w) if px[i, j][3] >= 160}
        rim = set()
        for (i, j) in solid:
            for di, dj in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                q = (i + di, j + dj)
                if q not in solid and not (0 <= q[0] < w and 0 <= q[1] < h and px[q[0], q[1]][3] >= 160):
                    rim.add(q)
        for (i, j) in rim:
            p.put(x + i, y + j, col, ha)
    for j in range(h):
        for i in range(w):
            r, g, b, a = px[i, j]
            if a == 0:
                continue
            c = (r, g, b)
            if fill is not None:
                lum = (r * 0.3 + g * 0.59 + b * 0.11) / 255.0
                c = mix(fill, mix(fill, WHITE, 0.25), lum * 0.6) if a > 128 else fill
            elif tint is not None:
                c = tuple(int(c[k] * tint[k] / 255.0) for k in range(3))
            k = alpha * a / 255.0
            if mode == "add":
                p.add(x + i, y + j, c, k)
            elif mode == "screen":
                p.screen(x + i, y + j, c, k)
            else:
                p.put(x + i, y + j, c, k)


def wall(p, y0, names, merlons=True, rough=False, seed=0):
    """A row of wall tiles from y0 down; merlons cut battlements, rough breaks the top edge."""
    layer = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    x = -((32 * len(names) - W) // 2)
    for k in range(0, 3):
        for i, n in enumerate(names):
            layer.alpha_composite(TILES.load(n), (x + (k * len(names) + i) * 32 - 32, y0 - 32 * 0))
    px = layer.load()
    for xx in range(W):
        top = y0
        if merlons and (xx // 4) % 2 == 1:
            top = y0 + 3
        if rough:
            top = y0 + int(hash01(xx // 2, 0, seed) * 3)
        for yy in range(y0, H):
            r, g, b, a = px[xx, yy]
            if yy < top or a == 0:
                continue
            p.put(xx, yy, (r, g, b))
        if top < H:
            p.put(xx, top, (8, 8, 10))
    for xx in range(W):  # light rim on the top of every merlon
        top = y0 + (3 if merlons and (xx // 4) % 2 == 1 else 0)
        if rough:
            top = y0 + int(hash01(xx // 2, 0, seed) * 3)
        if top + 1 < H:
            p.put(xx, top + 1, WHITE, 0.25)


# ------------------------------------------------------------------ procedural effects
def glow(p, cx, cy, r, col, strength=0.7, ry=None, levels=4):
    """Additive radial light, quantised in dithered steps so it stays pixel art."""
    ry = ry or r
    for y in range(H):
        for x in range(W):
            d = math.hypot((x + 0.5 - cx) / r, (y + 0.5 - cy) / ry)
            if d >= 1:
                continue
            a = (1 - d) ** 1.6 * strength
            q = math.floor(a * levels + bayer(x, y)) / float(levels)
            if q > 0:
                p.add(x, y, col, q)


def slash(p, cx, cy, r, a0, a1, col=WHITE, edge=STEEL, w=2.0, alpha=1.0):
    """Crescent along a circle (angles in degrees, screen space: 0 right, 90 down), tapered at both ends."""
    span = (a1 - a0) % 360 or 360
    for y in range(H):
        for x in range(W):
            dx, dy = x + 0.5 - cx, y + 0.5 - cy
            d = math.hypot(dx, dy)
            if d > r + 0.5 or d < r - w - 1:
                continue
            u = ((math.degrees(math.atan2(dy, dx)) - a0) % 360) / span
            if u > 1:
                continue
            width = w * math.sin(math.pi * u) ** 0.7
            if width < 0.35:
                continue
            if r - width <= d <= r + 0.5:
                p.put(x, y, col if d >= r - width * 0.55 else edge, alpha)


def line(p, x0, y0, x1, y1, col, a=1.0, mode="put"):
    n = int(max(abs(x1 - x0), abs(y1 - y0)) * 2) + 1
    seen = set()
    for i in range(n + 1):
        t = i / float(n)
        x, y = int(math.floor(x0 + (x1 - x0) * t)), int(math.floor(y0 + (y1 - y0) * t))
        if (x, y) in seen:
            continue
        seen.add((x, y))
        (p.add if mode == "add" else p.put)(x, y, col, a)


def streak(p, x0, y0, x1, y1, col=WHITE, edge=STEEL):
    """A thin straight cut with a bright core and a soft rim."""
    line(p, x0, y0 + 1, x1, y1 + 1, edge, 0.55)
    line(p, x0, y0, x1, y1, col)


def burst(p, cx, cy, r, col=YELLOW, core=WHITE, rays=8, turn=0.0):
    """Impact star: long and short rays alternating, bright diamond core."""
    for i in range(rays):
        ang = turn + i * 2 * math.pi / rays
        length = r if i % 2 == 0 else r * 0.55
        steps = int(length * 2) + 1
        for s in range(steps):
            t = s / 2.0
            p.put(cx + math.cos(ang) * t, cy + math.sin(ang) * t, core if t < length * 0.4 else col)
    for dx, dy in ((0, 0), (1, 0), (-1, 0), (0, 1), (0, -1)):
        p.put(cx + dx, cy + dy, core)


def twinkle(p, cx, cy, r, col=WHITE):
    """Four-point star."""
    for t in range(-r, r + 1):
        a = 1.0 if abs(t) <= r // 2 else 0.6
        p.put(cx + t, cy, col, a)
        p.put(cx, cy + t, col, a)
    p.put(cx, cy, WHITE)


def sparks(p, n, box, col, seed, plus=0.3, alpha=1.0):
    x0, y0, x1, y1 = box
    for i in range(n):
        x = x0 + int(hash01(i, 1, seed) * (x1 - x0))
        y = y0 + int(hash01(i, 2, seed) * (y1 - y0))
        if hash01(i, 3, seed) < plus:
            for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                p.put(x + dx, y + dy, col, 0.7 * alpha)
            p.put(x, y, WHITE, alpha)
        else:
            p.put(x, y, col, alpha)


def speed(p, n, box, col=WHITE, seed=0, alpha=0.55, vertical=False):
    """Motion streaks; each fades from its head to its tail."""
    x0, y0, x1, y1 = box
    for i in range(n):
        if vertical:
            x = x0 + int(hash01(i, 5, seed) * (x1 - x0))
            length = 3 + int(hash01(i, 6, seed) * (y1 - y0) * 0.6)
            ys = y0 + int(hash01(i, 7, seed) * max(1, (y1 - y0 - length)))
            for k in range(length):
                p.put(x, ys + k, col, alpha * (k + 1) / length)
        else:
            y = y0 + int(hash01(i, 5, seed) * (y1 - y0))
            length = 3 + int(hash01(i, 6, seed) * (x1 - x0) * 0.6)
            xs = x0 + int(hash01(i, 7, seed) * max(1, (x1 - x0 - length)))
            for k in range(length):
                p.put(xs + k, y, col, alpha * (k + 1) / length)


def drop(p, cx, cy, r, base, hi=WHITE):
    """Teardrop pointing up, lit from the top-left."""
    top = cy - r * 2.3
    for y in range(int(top) - 1, int(cy + r) + 2):
        for x in range(int(cx - r) - 1, int(cx + r) + 2):
            fx, fy = x + 0.5, y + 0.5
            inside = math.hypot(fx - cx, fy - cy) <= r
            if not inside and top <= fy <= cy:
                half = r * ((fy - top) / (cy - top)) ** 1.4
                inside = abs(fx - cx) <= half
            if not inside:
                continue
            u, v = (fx - cx) / (r + 0.01), (fy - cy) / (r + 0.01)
            lamb = clamp(0.55 - 0.45 * u - 0.35 * v)
            c = mix(mix(base, (0, 0, 0), 0.55), base, lamb + (bayer(x, y) - 0.5) * 0.3)
            p.put(x, y, c)
    p.put(cx - r * 0.45, cy - r * 0.35, hi)
    if r >= 3:
        p.put(cx - r * 0.45, cy - r * 0.35 - 1, hi, 0.6)


def drops(p, pts, base=BLOOD):
    for (x, y, r) in pts:
        drop(p, x, y, r, base, mix(base, WHITE, 0.6))


def crack(p, x0, y0, x1, y1, seed, col=(6, 5, 6), lit=(150, 140, 120), branches=2, inner=None):
    pts = []
    n = 9
    for i in range(n + 1):
        t = i / float(n)
        off = (hash01(i, 11, seed) - 0.5) * 3.2 if 0 < i < n else 0
        nx, ny = -(y1 - y0), (x1 - x0)
        ln = math.hypot(nx, ny) or 1
        pts.append((x0 + (x1 - x0) * t + nx / ln * off, y0 + (y1 - y0) * t + ny / ln * off))
    for (ax, ay), (bx, by) in zip(pts, pts[1:]):
        line(p, ax + 1, ay + 1, bx + 1, by + 1, lit, 0.5)
        line(p, ax, ay, bx, by, inner or col)
    for b in range(branches):
        i = 2 + int(hash01(b, 12, seed) * (n - 4))
        ax, ay = pts[i]
        ang = math.atan2(y1 - y0, x1 - x0) + (0.9 if b % 2 == 0 else -0.9)
        L = 3 + hash01(b, 13, seed) * 4
        line(p, ax, ay, ax + math.cos(ang) * L, ay + math.sin(ang) * L, inner or col)


def ring(p, cx, cy, r, col, a=1.0, ry=None, mode="put"):
    ry = ry or r
    steps = int(2 * math.pi * max(r, ry) * 2) + 8
    seen = set()
    for s in range(steps):
        ang = s * 2 * math.pi / steps
        q = (int(math.floor(cx + math.cos(ang) * r)), int(math.floor(cy + math.sin(ang) * ry)))
        if q in seen:
            continue
        seen.add(q)
        (p.add if mode == "add" else p.put)(q[0], q[1], col, a)


def bubble(p, cx, cy, r, col=CYAN):
    for y in range(int(cy - r) - 1, int(cy + r) + 2):
        for x in range(int(cx - r) - 1, int(cx + r) + 2):
            d = math.hypot(x + 0.5 - cx, y + 0.5 - cy)
            if d <= r - 1:
                p.add(x, y, col, 0.12)
    ring(p, cx, cy, r - 0.5, col, 0.85)
    p.put(cx - r * 0.45, cy - r * 0.5, WHITE)
    if r >= 4:
        p.put(cx - r * 0.45 + 1, cy - r * 0.5, WHITE, 0.7)
        p.put(cx - r * 0.45, cy - r * 0.5 + 1, WHITE, 0.7)


def heart(p, cx, cy, s, base, hi=WHITE, outline=(20, 6, 8)):
    inside = {}
    for y in range(int(cy - s * 1.4) - 1, int(cy + s * 1.4) + 2):
        for x in range(int(cx - s * 1.4) - 1, int(cx + s * 1.4) + 2):
            u, v = (x + 0.5 - cx) / s, -(y + 0.5 - cy) / s + 0.15
            if (u * u + v * v - 1) ** 3 - u * u * v ** 3 <= 0:
                lamb = clamp(0.6 - 0.35 * u + 0.35 * v)
                inside[(x, y)] = mix(mix(base, (0, 0, 0), 0.5), base, lamb + (bayer(x, y) - 0.5) * 0.3)
    _outlined(p, inside, outline)
    p.put(cx - s * 0.55, cy - s * 0.45, hi)
    p.put(cx - s * 0.55 + 1, cy - s * 0.45, hi, 0.6)


def _outlined(p, pixels, outline):
    rim = set()
    for (i, j) in pixels:
        for q in ((i + 1, j), (i - 1, j), (i, j + 1), (i, j - 1)):
            if q not in pixels:
                rim.add(q)
    for q in rim:
        p.put(q[0], q[1], outline)
    for q, c in pixels.items():
        p.put(q[0], q[1], c)


def blade(p, x, y, ang, L=12, handle=4, metal=((238, 242, 252), (140, 150, 176)), grip=(112, 66, 34), guard=(214, 176, 70)):
    """A knife from its pommel at (x, y) along ang (degrees, screen space), outlined like the tiles."""
    a = math.radians(ang)
    dx, dy = math.cos(a), math.sin(a)
    nx, ny = -dy, dx
    px = {}

    def put(fx, fy, c):
        px[(int(math.floor(fx)), int(math.floor(fy)))] = c
    t = 0.0
    while t < handle:
        put(x + dx * t, y + dy * t, grip if int(t) % 2 == 0 else mix(grip, (0, 0, 0), 0.3))
        t += 0.4
    gx, gy = x + dx * handle, y + dy * handle
    for k in (-2, -1, 0, 1, 2):
        put(gx + nx * k * 0.75, gy + ny * k * 0.75, guard)
    t = 0.8
    while t < L:
        bx, by = gx + dx * t, gy + dy * t
        if t < L * 0.8:
            put(bx + nx * 0.9, by + ny * 0.9, metal[1])
        put(bx, by, metal[0])
        t += 0.35
    _outlined(p, px, (16, 14, 22))


def cloud(p, cx, cy, w, h, base, seed, alpha=1.0, outline=True):
    """A cluster of lit puffs (smoke, steam, storm clouds), 4 dithered tones and a dark rim."""
    circles = []
    n = max(3, int(w / 8))
    for k in range(n):
        t = k / float(n - 1)
        r = h * (0.45 + 0.3 * math.sin(math.pi * t)) * (0.75 + 0.5 * hash01(k, 71, seed))
        circles.append((cx - w / 2.0 + w * t + (hash01(k, 73, seed) - 0.5) * 3, cy + h * 0.2 - r * 0.3, r))
    tops = max(1, n // 2)
    for k in range(tops):
        t = (k + 0.5) / float(tops)
        r = h * (0.6 + 0.35 * hash01(k, 72, seed))
        circles.append((cx - w * 0.32 + w * 0.64 * t + (hash01(k, 74, seed) - 0.5) * 4, cy - h * 0.15, r))
    dark, lit = mix(base, (0, 0, 0), 0.55), mix(base, WHITE, 0.35)
    tones = [dark, mix(dark, base, 0.6), base, lit]
    px = {}
    for y in range(int(cy - h) - 2, int(cy + h) + 2):
        for x in range(int(cx - w / 2.0 - h) - 2, int(cx + w / 2.0 + h) + 2):
            best = None
            for (ccx, ccy, r) in circles:
                u, v = (x + 0.5 - ccx) / r, (y + 0.5 - ccy) / r
                d = u * u + v * v
                if d <= 1 and y + 0.5 <= cy + h * 0.55:
                    z = math.sqrt(1 - d)
                    lamb = clamp(0.25 + 0.75 * (-0.5 * u - 0.6 * v + 0.62 * z))
                    best = lamb if best is None else max(best, lamb)
            if best is not None:
                i = max(0, min(3, int(round(best * 3 + (bayer(x, y) - 0.5) * 0.9))))
                px[(x, y)] = tones[i]
    if outline:
        rim = {q for (i, j) in px for q in ((i + 1, j), (i - 1, j), (i, j + 1), (i, j - 1)) if q not in px}
        for q in rim:
            p.put(q[0], q[1], mix(dark, (0, 0, 0), 0.5), alpha)
    for q, c in px.items():
        p.put(q[0], q[1], c, alpha)


def arrowhead(p, x, y, ang, col, size=3):
    a = math.radians(ang)
    for k in range(size + 1):
        for s in (-1, 1):
            b = a + math.pi - s * 0.6
            p.put(x + math.cos(b) * k, y + math.sin(b) * k, col)
    p.put(x, y, col)


def wisp(p, x, y0, y1, col, phase=0.0, alpha=0.6, amp=1.6):
    """A rising wavy line (steam, updraft) that fades upwards."""
    for y in range(int(y1), int(y0) + 1):
        t = (y0 - y) / float(max(1, y0 - y1))
        xx = x + math.sin(y * 0.45 + phase) * amp * (0.5 + t)
        p.put(xx, y, col, alpha * (1 - t * 0.85))
        p.put(xx + 1, y, col, alpha * (1 - t * 0.85) * 0.5)


def die(p, x, y, n, body=(236, 230, 218), shade=(150, 140, 128), pip=(40, 16, 20)):
    """An 11x11 die with a dark outline, a bevel and n pips."""
    s = 11
    for j in range(-1, s + 1):
        for i in range(-1, s + 1):
            corner = (i in (-1, s)) and (j in (-1, s))
            if corner:
                continue
            if i in (-1, s) or j in (-1, s):
                p.put(x + i, y + j, (20, 14, 16))
                continue
            c = body
            if i == s - 1 or j == s - 1:
                c = shade
            if i == 0 or j == 0:
                c = mix(body, WHITE, 0.6)
            p.put(x + i, y + j, c)
    lo, mid, hi = 2, 5, 8
    spots = {1: [(mid, mid)], 2: [(lo, lo), (hi, hi)], 3: [(lo, lo), (mid, mid), (hi, hi)],
             4: [(lo, lo), (hi, lo), (lo, hi), (hi, hi)], 5: [(lo, lo), (hi, lo), (mid, mid), (lo, hi), (hi, hi)],
             6: [(lo, lo), (hi, lo), (lo, mid), (hi, mid), (lo, hi), (hi, hi)]}[n]
    for (i, j) in spots:
        p.put(x + i, y + j, pip)
        p.put(x + i, y + j + 1, mix(pip, body, 0.5))


def minicard(p, x, y, back=(40, 52, 120), w=7, h=10, alpha=1.0, trim=(214, 176, 80)):
    """A small card seen from the back: dark outline, gold trim, coloured field with a light diamond."""
    hi = mix(back, WHITE, 0.45)
    cx, cy = (w - 1) / 2.0, (h - 1) / 2.0
    for j in range(h):
        for i in range(w):
            if (i in (0, w - 1)) and (j in (0, h - 1)):
                continue
            if i in (0, w - 1) or j in (0, h - 1):
                c = (16, 12, 14)
            elif i in (1, w - 2) or j in (1, h - 2):
                c = trim if (i + j) % 5 else mix(trim, WHITE, 0.4)
            elif abs(i - cx) / max(1.0, cx - 1.5) + abs(j - cy) / max(1.0, cy - 1.5) <= 0.75:
                c = hi
            else:
                c = back if (i * 3 + j) % 7 else mix(back, (0, 0, 0), 0.25)
            p.put(x + i, y + j, c, alpha)


def chain(p, x0, y, n, step, col=GOLD, last=None):
    for k in range(n):
        cx = x0 + k * step
        c = last if (last is not None and k == n - 1) else col
        dark = mix(c, (0, 0, 0), 0.6)
        rx, ry = (4.4, 3.0) if k % 2 == 0 else (2.0, 3.6)
        for d in (1.0, 0.0):
            ring(p, cx + d, y + d, rx, dark if d else c, 1.0, ry=ry)
            ring(p, cx + d, y + d, rx - 0.8, dark if d else mix(c, WHITE, 0.3), 1.0, ry=ry - 0.8)


def waves(p, y0, seed, deep=(16, 48, 98), lit=(60, 140, 210), foam=(210, 240, 255), amp=1.6, period=11.0, phase=0.0):
    for x in range(W):
        top = y0 + amp * math.sin((x + phase) * 2 * math.pi / period) + amp * 0.4 * math.sin((x + seed) * 0.9)
        for y in range(H):
            if y + 0.5 < top:
                continue
            depth = (y - top) / max(1.0, H - top)
            c = mix(lit, deep, clamp(depth * 1.6 + (bayer(x, y) - 0.5) * 0.35))
            p.put(x, y, c)
        p.put(x, top, foam)
        if hash01(x, 21, seed) > 0.6:
            p.put(x, top + 1, foam, 0.5)


def spiral(p, cx, cy, r, turns, col, w=1, alpha=1.0):
    steps = int(turns * 120)
    for s in range(steps):
        t = s / float(steps)
        ang = t * turns * 2 * math.pi
        rr = r * t
        for k in range(w):
            p.put(cx + math.cos(ang) * (rr + k * 0.6), cy + math.sin(ang) * (rr + k * 0.6) * 0.8, col, alpha * (0.35 + 0.65 * t))


def bolt(p, x0, y0, x1, y1, seed, col=CYAN, core=WHITE, segs=6):
    pts = [(x0, y0)]
    for i in range(1, segs):
        t = i / float(segs)
        pts.append((x0 + (x1 - x0) * t + (hash01(i, 31, seed) - 0.5) * 6, y0 + (y1 - y0) * t))
    pts.append((x1, y1))
    for (ax, ay), (bx, by) in zip(pts, pts[1:]):
        line(p, ax + 1, ay, bx + 1, by, col, 0.8)
        line(p, ax - 1, ay, bx - 1, by, col, 0.5)
        line(p, ax, ay, bx, by, core)


def arrow_up(p, cx, cy, col=RED, size=4):
    for j in range(size):
        for i in range(-j, j + 1):
            p.put(cx + i, cy - size + j, col)
    for j in range(size):
        p.put(cx, cy + j, col)
        p.put(cx - 1, cy + j, col)
        p.put(cx + 1, cy + j, mix(col, (0, 0, 0), 0.4))


def cross(p, cx, cy, col=RED, arm=2):
    for t in range(-arm, arm + 1):
        p.put(cx + t, cy, col)
        p.put(cx, cy + t, col)
    p.put(cx - 1, cy - 1, WHITE, 0.0)


def ghosts(p, name, x, y, offsets, col, flip=False):
    for k, (dx, a) in enumerate(offsets):
        S(p, name, x + dx, y, flip=flip, fill=col, alpha=a)


def arc_path(p, cx, cy, r, a0, a1, col, dash=2, alpha=0.8):
    span = (a1 - a0) % 360
    steps = int(span / 360.0 * 2 * math.pi * r * 1.5) + 2
    for s in range(steps):
        if (s // dash) % 2:
            continue
        ang = math.radians(a0 + span * s / float(steps))
        p.put(cx + math.cos(ang) * r, cy + math.sin(ang) * r, col, alpha)


# ------------------------------------------------------------------ tile folders
Wp = "item/weapon/"
WA = "item/weapon/artefact/"
SH = "item/armor/shields/"
HG = "item/armor/headgear/"
HA = "item/armor/hands/"
FT = "item/armor/feet/"
BK = "item/armor/back/"
TO = "item/armor/torso/"
AA = "item/armor/artefact/"
MI = "item/misc/"
PO = "item/potion/"
WD = "item/wand/"
SC = "item/scroll/"
GO = "item/gold/"
AM = "item/amulet/"
BO = "item/book/"
EF = "effect/"
CO = "gui/spells/components/"
SPF = "gui/spells/fire/"
SPI = "gui/spells/ice/"
SPA = "gui/spells/air/"
SPE = "gui/spells/earth/"
SPP = "gui/spells/poison/"
SPC = "gui/spells/conjuration/"
SPN = "gui/spells/enchantment/"
SPT = "gui/spells/translocation/"
SPX = "gui/spells/necromancy/"
SPM = "gui/spells/transmutation/"
SPS = "gui/spells/summoning/"
AB = "gui/abilities/"
DS = "dungeon/statues/"
DW = "dungeon/wall/"
BL = "misc/blood/"
MN = "monster/"


# ------------------------------------------------------------------ recipes
# id: (palette, light spot, draw(p, seed))
R = {}


def card(cid, pal, light=(22, 14)):
    def deco(fn):
        if cid in R:
            raise ValueError("receta duplicada: " + cid)
        R[cid] = (pal, light, fn)
        return fn
    return deco


# --- neutral / shared attacks (acero)
@card("a_golpe_ferreo", "steel", (30, 9))
def _(p, s):
    S(p, Wp + "hammer_2_new", 5, 1)
    burst(p, 30, 8, 7, ORANGE)
    sparks(p, 7, (22, 1, 42, 16), YELLOW, s)


@card("a_tajo", "steel", (22, 16))
def _(p, s):
    S(p, Wp + "cutlass_4", 6, 1)
    slash(p, 22, 22, 17, 195, 335, WHITE, STEEL, 2.6)


@card("a_arremetida", "steel", (28, 12))
def _(p, s):
    speed(p, 8, (0, 8, 20, 30), STEEL, s)
    S(p, Wp + "spear_2_new", 8, 0)
    burst(p, 34, 5, 5, WHITE, WHITE)


@card("a_furia", "blood", (22, 16))
def _(p, s):
    glow(p, 22, 16, 18, RED, 0.55)
    S(p, Wp + "war_axe_2", 6, 0, halo=(RED, 0.85))
    sparks(p, 10, (1, 1, 43, 31), ORANGE, s, 0.4)


@card("a_punio", "earth", (26, 12))
def _(p, s):
    speed(p, 6, (0, 10, 14, 28), YELLOW, s, 0.45)
    S(p, CO + "hand_4", 6, 2)
    burst(p, 30, 9, 8, YELLOW)


@card("a_golpe_pesado", "stone", (24, 22))
def _(p, s):
    crack(p, 6, 29, 40, 28, s, branches=3)
    S(p, Wp + "mace_large_1_new", 6, -2)
    burst(p, 29, 6, 6, ORANGE)


@card("a_patada", "earth", (26, 14))
def _(p, s):
    speed(p, 7, (0, 8, 16, 26), WHITE, s, 0.5)
    S(p, FT + "boots_1_brown_new", 6, 0)
    burst(p, 33, 12, 7, YELLOW)


@card("a_corte_rapido", "steel", (24, 14))
def _(p, s):
    speed(p, 8, (0, 4, 18, 28), STEEL, s, 0.5)
    S(p, Wp + "sabre_2", 7, 0)
    streak(p, 8, 26, 38, 6)
    streak(p, 14, 29, 41, 12)


@card("a_embestida", "steel", (26, 16))
def _(p, s):
    speed(p, 10, (0, 4, 24, 28), STEEL, s, 0.6)
    S(p, HG + "helmet_2", 9, 1)
    burst(p, 37, 16, 6, WHITE, WHITE)


@card("a_gran_golpe", "ember", (28, 10))
def _(p, s):
    S(p, Wp + "giant_spiked_club_new", 5, 1)
    burst(p, 30, 7, 11, ORANGE, YELLOW)
    sparks(p, 8, (18, 0, 44, 20), YELLOW, s, 0.5)


@card("a_golpe_total", "steel", (22, 20))
def _(p, s):
    ring(p, 22, 27, 20, STEEL, 0.45, ry=5)
    ring(p, 22, 27, 13, WHITE, 0.7, ry=3.5)
    S(p, Wp + "war_axe_4", 6, -3)
    sparks(p, 6, (0, 22, 44, 32), WHITE, s)


@card("a_instinto", "night", (22, 10))
def _(p, s):
    glow(p, 22, 8, 13, PURPLE, 0.6)
    S(p, CO + "eye", 6, -1)
    minicard(p, 8, 18, (130, 30, 40), 9, 12)
    minicard(p, 18, 16, (40, 60, 140), 9, 12)
    minicard(p, 28, 18, (100, 40, 140), 9, 12)


# --- rogue attacks
@card("r_estocada_oportuna", "night", (32, 6))
def _(p, s):
    S(p, Wp + "rapier_2", 6, 0)
    twinkle(p, 34, 4, 4)
    sparks(p, 5, (26, 0, 44, 14), STEEL, s)


@card("r_pinchazo", "night", (24, 12))
def _(p, s):
    S(p, Wp + "elven_dagger", 6, 1)
    burst(p, 30, 9, 4, WHITE, WHITE, 8, 0.39)
    drops(p, [(33, 18, 1.4), (36, 23, 1.0)])


@card("r_golpe_desesperado", "blood", (22, 14))
def _(p, s):
    minicard(p, 3, 4, alpha=0.9)
    minicard(p, 34, 20, alpha=0.8)
    S(p, Wp + "orcish_dagger", 7, 0)
    sparks(p, 8, (0, 0, 44, 32), ORANGE, s, 0.4)


@card("r_lluvia_de_dagas", "night", (22, 8))
def _(p, s):
    speed(p, 10, (0, 0, 44, 22), STEEL, s, 0.35, vertical=True)
    for (x, y) in ((-4, -6), (8, 0), (20, -8), (30, 2), (14, 10)):
        S(p, Wp + "dagger_new", x, y, rot=90)


@card("r_golpe_de_gracia", "night", (26, 12))
def _(p, s):
    glow(p, 34, 6, 10, BLUE, 0.6)
    S(p, WA + "urand_finisher", 6, 0)
    sparks(p, 8, (24, 0, 44, 16), CYAN, s, 0.5)


@card("r_corte_y_guardia", "steel", (22, 14))
def _(p, s):
    S(p, SH + "buckler_1_new", -1, 2)
    S(p, Wp + "short_sword_2_new", 14, 0)
    streak(p, 22, 28, 42, 8)


@card("r_cuchillada_errante", "night", (24, 14))
def _(p, s):
    arc_path(p, 22, 30, 18, 200, 340, PURPLE)
    arc_path(p, 22, 34, 12, 190, 300, STEEL)
    S(p, WA + "urand_knife_of_accuracy", 7, 0)


@card("r_abanico_de_cuchillas", "night", (22, 24))
def _(p, s):
    glow(p, 22, 27, 13, PURPLE, 0.6)
    for ang in (-165, -128, -90, -52, -15):
        a = math.radians(ang)
        blade(p, 22 + math.cos(a) * 3, 28 + math.sin(a) * 3, ang, 14, 4)
    sparks(p, 6, (2, 0, 42, 20), STEEL, s, 0.4)


@card("r_punalada_trapera", "shadow", (24, 14))
def _(p, s):
    S(p, CO + "person_1", 2, 0, fill=(58, 42, 84), halo=(PURPLE, 0.5))
    S(p, Wp + "dagger_3", 12, 2)
    burst(p, 18, 14, 4, RED, WHITE)


@card("r_tajo_veloz", "steel", (24, 14))
def _(p, s):
    speed(p, 9, (0, 2, 16, 30), STEEL, s, 0.55)
    S(p, Wp + "scimitar_2", 7, 0)
    slash(p, 24, 24, 15, 200, 320, WHITE, STEEL, 1.6)


@card("r_tirar_y_cortar", "night", (24, 14))
def _(p, s):
    S(p, MI + "misc_deck_rare_new", -2, 3)
    S(p, Wp + "cutlass_1", 13, 0)
    streak(p, 16, 30, 42, 10)


@card("r_rafaga_de_cortes", "blood", (22, 16))
def _(p, s):
    S(p, Wp + "katana_2", 6, 0)
    slash(p, 22, 26, 16, 200, 320, WHITE, (255, 150, 150), 1.8)
    slash(p, 16, 10, 14, 340, 100, WHITE, (255, 150, 150), 1.6)
    slash(p, 30, 12, 12, 100, 220, WHITE, (255, 150, 150), 1.4)


@card("r_lanzar_la_daga", "night", (30, 10))
def _(p, s):
    for k in range(9):
        p.put(4 + k * 2, 28 - k * 2, STEEL, 0.25 + k * 0.06)
    S(p, Wp + "dagger_7", 11, -2)
    twinkle(p, 36, 4, 3)


@card("r_corte_afortunado", "gold", (24, 12))
def _(p, s):
    S(p, GO + "gold_pile_1", -3, 10)
    S(p, Wp + "sabre_1_silver", 9, 0)
    twinkle(p, 33, 5, 4, YELLOW)
    sparks(p, 9, (0, 0, 44, 32), GOLD, s, 0.5)


@card("r_abrir_la_guardia", "steel", (20, 16))
def _(p, s):
    S(p, SH + "shield_3_round", 2, 2)
    crack(p, 12, 6, 24, 26, s, (10, 6, 6), (240, 220, 200), 2)
    S(p, Wp + "rapier_1", 15, -1)


@card("r_hoja_unica", "gold", (22, 14))
def _(p, s):
    glow(p, 22, 15, 17, GOLD, 0.55)
    S(p, WA + "spwpn_sword_of_power_new", 6, 0, halo=(GOLD, 0.75))
    sparks(p, 8, (0, 0, 44, 32), YELLOW, s, 0.5)


# --- mage elemental attacks
@card("mg_chispa_arcana", "arcane", (22, 16))
def _(p, s):
    glow(p, 22, 16, 14, PURPLE, 0.5)
    S(p, EF + "magic_bolt_5", 6, 0)
    S(p, EF + "flame_1", -3, 6)
    S(p, EF + "frost_0", 15, -6)


@card("mg_lengua_fuego", "ember", (22, 16))
def _(p, s):
    icon(p, SPF + "flame_tongue_new")


@card("mg_bola_fuego", "ember", (22, 16))
def _(p, s):
    icon(p, SPF + "fireball_new")
    sparks(p, 6, (0, 0, 44, 32), ORANGE, s)


@card("mg_gota", "water", (22, 12))
def _(p, s):
    ring(p, 22, 28, 12, CYAN, 0.35, ry=2.5)
    ring(p, 22, 28, 7, CYAN, 0.6, ry=1.5)
    drop(p, 22, 20, 6.5, (60, 150, 235))
    drops(p, [(10, 10, 1.6), (34, 8, 1.3)], (60, 150, 235))


@card("mg_ola", "water", (22, 8))
def _(p, s):
    waves(p, 13, s, amp=3.0, period=22.0, phase=4)
    waves(p, 20, s + 1, amp=2.0, period=13.0, phase=1, deep=(10, 30, 70))
    sparks(p, 8, (0, 8, 44, 18), (220, 245, 255), s, 0.2)


@card("mg_diluvio", "water", (22, 6))
def _(p, s):
    for i in range(26):
        x = int(hash01(i, 41, s) * 46) - 2
        y = 8 + int(hash01(i, 42, s) * 18)
        line(p, x, y, x - 2, y + 4, (150, 200, 255), 0.7)
    cloud(p, 22, 5, 42, 7, (60, 84, 130), s)
    waves(p, 27, s, amp=1.0, period=9.0)


@card("mg_piedra", "earth", (26, 14))
def _(p, s):
    speed(p, 8, (0, 6, 20, 28), (220, 190, 150), s, 0.5)
    S(p, MI + "misc_stone_new", 8, 0)


@card("mg_terremoto", "earth", (22, 18))
def _(p, s):
    icon(p, SPE + "shatter_new", 22, 13)
    crack(p, 2, 28, 42, 27, s, branches=4)
    S(p, EF + "stone_0_new", -6, 12)
    S(p, EF + "stone_0_new", 22, 14, flip=True)


@card("mg_rafaga", "air", (22, 16))
def _(p, s):
    icon(p, SPA + "airstrike_new")
    speed(p, 6, (0, 4, 14, 28), WHITE, s, 0.4)


@card("mg_ciclon", "air", (22, 16))
def _(p, s):
    S(p, EF + "tornado_2", 6, 0)
    sparks(p, 10, (2, 4, 42, 30), (200, 240, 240), s, 0.2)


@card("mg_vapor_hirviente", "water", (22, 22))
def _(p, s):
    glow(p, 22, 26, 16, ORANGE, 0.35, ry=6)
    for k, x in enumerate((7, 14, 22, 29, 36)):
        wisp(p, x, 22, 2 + k % 2 * 4, (235, 245, 255), k * 1.7, 0.75, 1.8)
    waves(p, 23, s, amp=1.2, period=8.0)
    for (x, y, r) in ((8, 27, 1.5), (16, 29, 1.0), (30, 27, 2.0), (37, 29, 1.0)):
        bubble(p, x, y, r + 1, (220, 245, 255))


@card("mg_grieta", "earth", (22, 18))
def _(p, s):
    glow(p, 22, 18, 18, ORANGE, 0.55, ry=12)
    crack(p, 4, 4, 38, 30, s, (40, 10, 4), (255, 200, 120), 3, inner=(255, 140, 40))
    crack(p, 6, 30, 32, 3, s + 1, (6, 5, 6), (150, 140, 120), 2)
    sparks(p, 8, (8, 4, 38, 30), ORANGE, s, 0.3)


@card("mg_llamarada", "ember", (22, 18))
def _(p, s):
    S(p, EF + "cloud_fire_1", -6, 4, alpha=0.85)
    S(p, EF + "cloud_fire_1", 18, 4, alpha=0.85)
    S(p, EF + "cloud_fire_2", 6, 0)


# --- warrior attacks
@card("wr_corte_sangriento", "blood", (22, 14))
def _(p, s):
    S(p, Wp + "long_sword_2", 6, 0)
    slash(p, 22, 24, 16, 200, 330, (255, 200, 200), RED, 2.2)
    drops(p, [(10, 26, 1.6), (35, 25, 1.3), (30, 29, 1.0)])


@card("wr_tajo_brutal", "steel", (22, 14))
def _(p, s):
    S(p, Wp + "greatsword_1_new", 6, 0)
    slash(p, 20, 30, 24, 210, 330, WHITE, STEEL, 3.2)


@card("wr_golpe_escudo", "steel", (26, 16))
def _(p, s):
    speed(p, 8, (0, 4, 16, 28), STEEL, s, 0.55)
    S(p, SH + "shield_3", 6, 0)
    burst(p, 36, 15, 7, YELLOW)


@card("wr_embate_firme", "stone", (26, 12))
def _(p, s):
    S(p, Wp + "mace_2_new", 6, 0)
    burst(p, 30, 8, 7, ORANGE)
    sparks(p, 5, (22, 0, 44, 18), YELLOW, s)


@card("wr_golpe_doble", "steel", (22, 14))
def _(p, s):
    S(p, Wp + "double_sword_new", 6, 0)
    streak(p, 4, 22, 30, 2)
    streak(p, 14, 30, 40, 10)


@card("wr_puno_hierro", "steel", (24, 14))
def _(p, s):
    S(p, HA + "gauntlet_1", 5, 1)
    burst(p, 31, 12, 7, YELLOW)
    arrow_up(p, 37, 26, RED, 4)


@card("wr_sed_batalla", "blood", (22, 14))
def _(p, s):
    glow(p, 22, 14, 16, RED, 0.5)
    S(p, WA + "urand_leech", 6, 0)
    drops(p, [(8, 26, 1.6), (36, 24, 1.4), (12, 6, 1.1)])


@card("wr_hachazo_temerario", "blood", (24, 12))
def _(p, s):
    S(p, Wp + "battle_axe_4", 6, 0)
    slash(p, 24, 26, 18, 205, 320, WHITE, RED, 2.0)
    drops(p, [(9, 25, 1.4), (37, 27, 1.2)])


@card("wr_torbellino_acero", "steel", (22, 16))
def _(p, s):
    slash(p, 22, 16, 15, 0, 110, WHITE, STEEL, 1.6)
    slash(p, 22, 16, 15, 120, 230, WHITE, STEEL, 1.6)
    slash(p, 22, 16, 15, 240, 350, WHITE, STEEL, 1.6)
    S(p, Wp + "broad_axe_2", 6, 0)


@card("wr_represalia", "blood", (24, 12))
def _(p, s):
    glow(p, 24, 12, 16, RED, 0.6)
    S(p, Wp + "eveningstar_2_new", 6, 0, halo=(RED, 0.6))
    burst(p, 29, 7, 8, RED, (255, 220, 200))


@card("wr_sacrificio", "blood", (22, 18))
def _(p, s):
    glow(p, 22, 18, 16, RED, 0.55)
    S(p, Wp + "demon_blade", 6, 0)
    drops(p, [(10, 24, 1.8), (16, 28, 1.2), (34, 26, 1.5)])


@card("wr_martillo_sismico", "stone", (22, 24))
def _(p, s):
    ring(p, 22, 28, 20, (220, 200, 160), 0.4, ry=4)
    crack(p, 2, 29, 42, 28, s, branches=4)
    S(p, Wp + "war_hammer", 6, -4, halo=(STEEL, 0.55))
    sparks(p, 8, (0, 20, 44, 32), (220, 190, 140), s)


# --- shields and guards (escudo)
@card("e_guardia_solida", "steel", (22, 14))
def _(p, s):
    glow(p, 22, 14, 16, BLUE, 0.35)
    S(p, SH + "shield_2_new", 6, 0)


@card("e_escudo_solido", "steel", (22, 14))
def _(p, s):
    S(p, SH + "shield_1", 6, 0)
    twinkle(p, 15, 7, 2)


@card("e_fortaleza", "stone", (22, 14))
def _(p, s):
    wall(p, 22, [DW + "brick_gray_0", DW + "brick_gray_1"])
    S(p, TO + "plate_mail_2", 6, -2)


@card("e_baluarte", "steel", (22, 14))
def _(p, s):
    glow(p, 22, 14, 17, STEEL, 0.4)
    S(p, SH + "lshield_dd_dk", 6, 0, halo=(STEEL, 0.5))


@card("e_parada", "steel", (22, 10))
def _(p, s):
    S(p, Wp + "long_sword_5", 1, 1)
    S(p, Wp + "long_sword_6", 11, 1, flip=True)
    burst(p, 22, 9, 6, YELLOW, WHITE)


@card("e_capa_hierro", "arcane", (22, 14))
def _(p, s):
    glow(p, 22, 14, 16, BLUE, 0.45)
    S(p, TO + "chain_mail_1", 6, 0)


@card("e_torre", "stone", (22, 14))
def _(p, s):
    S(p, SH + "large_shield_1_new", 6, 0)


@card("e_escudo_reactivo", "night", (22, 14))
def _(p, s):
    ring(p, 22, 15, 14, CYAN, 0.5)
    S(p, SH + "buckler_2_new", 6, 0)
    sparks(p, 8, (4, 0, 40, 30), CYAN, s, 0.5)


@card("e_agilidad", "forest", (24, 14))
def _(p, s):
    speed(p, 9, (0, 4, 18, 30), (200, 255, 200), s, 0.5)
    S(p, FT + "boots_4_green", 7, 0)


@card("e_gran_muralla", "stone", (22, 6))
def _(p, s):
    wall(p, 9, [DW + "brick_brown_0", DW + "brick_brown_1"])
    wall(p, 21, [DW + "brick_brown_2", DW + "brick_brown_3"], merlons=False)


@card("e_muro_de_mana", "arcane", (22, 16))
def _(p, s):
    icon(p, SPI + "condensation_shield_new")
    sparks(p, 8, (0, 0, 44, 32), BLUE, s, 0.4)


@card("e_retribucion", "steel", (24, 14))
def _(p, s):
    for k in range(4):
        minicard(p, 2 + k, 20 - k * 2, (60, 60, 80), 9, 12)
    S(p, SH + "shield_dd_scion", 10, 0)


@card("r_paso_atras", "night", (20, 16))
def _(p, s):
    speed(p, 8, (26, 6, 44, 28), STEEL, s, 0.45)
    S(p, FT + "low_boots", 4, 2, flip=True)


@card("r_guardia_evasiva", "night", (22, 14))
def _(p, s):
    S(p, SH + "elven_buckler_1", -2, 0, fill=(70, 60, 120), alpha=0.35)
    S(p, SH + "elven_buckler_1", 2, 0, fill=(90, 80, 150), alpha=0.5)
    S(p, SH + "elven_buckler_1", 8, 0)


@card("r_muro_de_humo", "shadow", (22, 12))
def _(p, s):
    cloud(p, 22, 12, 34, 9, (96, 92, 108), s + 1)
    cloud(p, 22, 22, 46, 10, (130, 126, 140), s)
    sparks(p, 5, (0, 0, 44, 10), (150, 140, 170), s, 0.2)


@card("r_quiebro", "night", (22, 16))
def _(p, s):
    icon(p, SPT + "blink")


@card("r_finta_doble", "night", (22, 14))
def _(p, s):
    ghosts(p, Wp + "quickblade", -2, 0, [(-3, 0.25)], (90, 80, 150))
    ghosts(p, Wp + "quickblade", 14, 0, [(3, 0.25)], (90, 80, 150), flip=True)
    S(p, Wp + "quickblade", -2, 0)
    S(p, Wp + "quickblade", 14, 0, flip=True)


@card("r_sombra_esquiva", "shadow", (24, 14))
def _(p, s):
    S(p, CO + "person_1", -3, 0, fill=(120, 96, 180), alpha=0.3)
    S(p, CO + "person_1", 3, 0, fill=(130, 104, 196), alpha=0.5)
    S(p, CO + "person_1", 9, 0, fill=(22, 14, 32), halo=(PURPLE, 0.8))


@card("r_manto_raido", "night", (22, 14))
def _(p, s):
    S(p, BK + "cloak_1_leather", 6, 0)
    for (x, y) in ((14, 26), (21, 28), (27, 27)):
        p.put(x, y, (8, 8, 16))


@card("r_rodar", "night", (22, 16))
def _(p, s):
    slash(p, 22, 17, 13, 150, 60, WHITE, STEEL, 1.8)
    a = math.radians(60)
    arrowhead(p, 22 + math.cos(a) * 12.5, 17 + math.sin(a) * 12.5, 150, WHITE, 3)
    S(p, CO + "running", 6, 1, rot=90, halo=(PURPLE, 0.6))
    speed(p, 5, (0, 22, 14, 30), STEEL, s, 0.4)


@card("r_deshacerse", "night", (20, 18))
def _(p, s):
    S(p, CO + "hand_3", -2, 6)
    speed(p, 6, (16, 2, 30, 26), STEEL, s, 0.4)
    minicard(p, 24, 3, (130, 30, 40), 8, 11)
    minicard(p, 34, 10, (40, 60, 140), 8, 11, 0.9)
    minicard(p, 27, 19, (100, 40, 140), 8, 11, 0.8)


@card("r_senuelo", "forest", (22, 14))
def _(p, s):
    ring(p, 22, 15, 13, RED, 0.6)
    ring(p, 22, 15, 8, RED, 0.45)
    S(p, MN + "nonliving/wood_golem", 6, 0)


@card("r_capa_de_sombras", "shadow", (22, 14))
def _(p, s):
    glow(p, 22, 14, 17, PURPLE, 0.5)
    S(p, BK + "cloak_4", 6, 0, tint=(150, 120, 200), halo=(PURPLE, 0.55))
    sparks(p, 8, (0, 0, 44, 32), PURPLE, s, 0.3)


@card("r_contraataque", "steel", (22, 14))
def _(p, s):
    S(p, SH + "buckler_3_new", -2, 2)
    S(p, Wp + "rapier_3", 13, 0)
    burst(p, 20, 14, 5, YELLOW, WHITE)


@card("r_estilo_propio", "gold", (22, 12))
def _(p, s):
    glow(p, 22, 12, 15, GOLD, 0.4)
    S(p, HG + "cap_jester", 6, 0)
    sparks(p, 10, (0, 0, 44, 32), YELLOW, s, 0.5)


# --- mage shields
@card("mg_marea", "water", (32, 6))
def _(p, s):
    glow(p, 33, 6, 8, (220, 240, 255), 0.7)
    ring(p, 33, 6, 4, (230, 240, 255), 1.0)
    waves(p, 16, s, amp=2.2, period=16.0, phase=2)
    waves(p, 23, s + 3, amp=1.6, period=10.0, phase=6, deep=(8, 26, 60))


@card("mg_burbuja", "water", (20, 14))
def _(p, s):
    bubble(p, 21, 16, 11, CYAN)
    bubble(p, 36, 7, 3, CYAN)
    bubble(p, 7, 24, 2.5, CYAN)
    bubble(p, 38, 25, 2, CYAN)


@card("mg_muro_roca", "earth", (22, 6))
def _(p, s):
    wall(p, 8, [DW + "lab-rock_0", DW + "lab-rock_1"], merlons=False, rough=True, seed=s)
    S(p, EF + "stone_0_new", -4, -12)


@card("mg_piel_granito", "stone", (22, 12))
def _(p, s):
    glow(p, 22, 14, 16, (200, 170, 120), 0.4)
    S(p, DS + "granite_statue", 6, 0)


# --- warrior shields
@card("wr_escudo_sangre", "blood", (22, 14))
def _(p, s):
    S(p, SH + "shield_3_round", 6, 0)
    drops(p, [(9, 8, 1.8), (35, 12, 1.5), (34, 24, 1.1)])


@card("wr_postura_firme", "stone", (22, 18))
def _(p, s):
    for x in range(2, 42):
        p.put(x, 27, (90, 84, 76))
    S(p, FT + "boots_iron_2", 6, -2)
    sparks(p, 6, (2, 24, 42, 31), (150, 140, 120), s)


@card("wr_muro_acero", "steel", (22, 14))
def _(p, s):
    S(p, SH + "large_shield_2_new", -3, 0)
    S(p, SH + "large_shield_2_new", 15, 0)


@card("wr_respiro", "forest", (22, 14))
def _(p, s):
    glow(p, 22, 14, 16, GREEN, 0.4)
    S(p, PO + "pink", 6, 0)
    for (x, y) in ((8, 8), (35, 6), (37, 22), (9, 23)):
        cross(p, x, y, GREEN, 2)


@card("wr_coraza_viva", "blood", (22, 14))
def _(p, s):
    glow(p, 22, 13, 15, RED, 0.45)
    S(p, TO + "orcish_platemail", 6, 0)
    heart(p, 21, 12, 3.2, (230, 50, 60))


@card("wr_segunda_piel", "earth", (22, 14))
def _(p, s):
    glow(p, 22, 14, 16, (150, 220, 120), 0.4)
    S(p, TO + "troll_leather_armor", 6, 0, halo=((170, 235, 140), 0.6))


# --- magic (magia)
@card("m_chispa", "arcane", (22, 16))
def _(p, s):
    glow(p, 22, 16, 12, YELLOW, 0.5)
    S(p, EF + "magic_bolt_6", 6, 0)
    sparks(p, 8, (4, 2, 40, 30), YELLOW, s, 0.4)


@card("m_rayo", "arcane", (22, 16))
def _(p, s):
    icon(p, SPA + "lightning_bolt_new")


@card("m_absorcion", "arcane", (22, 16))
def _(p, s):
    icon(p, SPT + "gravitas")


@card("m_impulso", "arcane", (22, 16))
def _(p, s):
    icon(p, SPN + "haste_new")


@card("m_torbellino", "air", (22, 16))
def _(p, s):
    icon(p, SPA + "tornado")


@card("m_barrera_magica", "arcane", (22, 16))
def _(p, s):
    icon(p, SPA + "repel_missiles_new")


@card("m_vision", "arcane", (22, 14))
def _(p, s):
    icon(p, SPN + "see_invisible")


@card("m_hechizo_menor", "arcane", (28, 8))
def _(p, s):
    S(p, WD + "gem_glass_new", 5, 2)
    twinkle(p, 30, 6, 3, CYAN)
    sparks(p, 6, (24, 0, 42, 14), CYAN, s, 0.4)


@card("m_concentracion", "arcane", (22, 14))
def _(p, s):
    ring(p, 22, 26, 12, PURPLE, 0.5, ry=3)
    ring(p, 22, 26, 7, CYAN, 0.6, ry=2)
    glow(p, 22, 13, 12, BLUE, 0.5)
    S(p, SPA + "levitation", 6, -1)


@card("m_conjuro", "arcane", (22, 16))
def _(p, s):
    icon(p, SPN + "spectral_weapon")


@card("m_veneno", "poison", (22, 16))
def _(p, s):
    S(p, PO + "emerald", 6, 0)
    for (x, y, r) in ((10, 6, 1.5), (34, 9, 2), (31, 3, 1)):
        bubble(p, x, y, r + 1, TOXIC)


@card("m_vision_del_caos", "royal", (22, 16))
def _(p, s):
    S(p, EF + "cloud_chaos_2", 6, 0, alpha=0.85)
    S(p, CO + "eye", 6, 0)


@card("m_grito_de_guerra", "ember", (18, 14))
def _(p, s):
    S(p, MI + "misc_horn", 2, 1)
    for r in (6, 10, 14):
        slash(p, 26, 14, r, 300, 60, (255, 230, 180), ORANGE, 1.0, 0.8)


@card("r_preparacion", "night", (22, 14))
def _(p, s):
    S(p, BO + "manual_2", 6, 0)
    sparks(p, 8, (0, 0, 44, 32), CYAN, s, 0.4)


@card("r_rebuscar", "earth", (22, 12))
def _(p, s):
    minicard(p, 8, 5, (150, 34, 40), 9, 13)
    minicard(p, 18, 2, (40, 70, 160), 9, 13)
    minicard(p, 28, 5, (110, 46, 156), 9, 13)
    S(p, CO + "hand_2", 6, 8)
    sparks(p, 5, (4, 0, 40, 10), YELLOW, s, 0.5)


@card("r_astucia", "night", (22, 14))
def _(p, s):
    S(p, MN + "animals/jackal_new", 6, 0)
    twinkle(p, 33, 6, 3, YELLOW)


@card("r_dagas_ocultas", "shadow", (22, 14))
def _(p, s):
    S(p, BK + "cloak_2", 6, 0, tint=(170, 150, 210))
    S(p, Wp + "knife", -3, 4)
    S(p, Wp + "knife", 15, 4, flip=True)
    twinkle(p, 8, 10, 2)
    twinkle(p, 36, 10, 2)


@card("r_afilar", "steel", (24, 12))
def _(p, s):
    S(p, MI + "misc_stone_inert", -4, 10)
    S(p, Wp + "short_sword_3", 8, -1)
    sparks(p, 10, (10, 10, 30, 30), ORANGE, s, 0.3)


@card("r_reflejos", "night", (22, 14))
def _(p, s):
    S(p, MI + "mirror", 6, 0)
    twinkle(p, 18, 8, 3)
    sparks(p, 6, (0, 0, 44, 32), STEEL, s, 0.4)


@card("r_danza_de_sombras", "shadow", (22, 16))
def _(p, s):
    icon(p, SPS + "summon_shadow_creatures_new")


@card("r_chatarra", "earth", (22, 20))
def _(p, s):
    S(p, Wp + "orcish_short_sword", -4, 2)
    S(p, FT + "low_boots", 16, 6)
    S(p, MI + "key", 4, 12)
    S(p, HG + "cap_1", 12, 8)


@card("r_vaciar_bolsillos", "night", (22, 20))
def _(p, s):
    S(p, GO + "gold_pile_5", 6, 6)
    minicard(p, 6, 2, alpha=0.9)
    minicard(p, 34, 4, alpha=0.9)
    minicard(p, 22, -1, alpha=0.8)


@card("r_juego_de_manos", "night", (26, 10))
def _(p, s):
    arc_path(p, 22, 20, 13, 200, 340, YELLOW, 2, 0.7)
    S(p, CO + "hand_2", 0, 4)
    minicard(p, 27, 3, (120, 30, 60), 9, 13)
    sparks(p, 8, (16, 0, 44, 22), YELLOW, s, 0.5)


@card("r_carterista", "night", (22, 16))
def _(p, s):
    S(p, CO + "hand_1", -1, 0)
    S(p, GO + "gold_pile_2", 14, 6)
    sparks(p, 6, (20, 8, 42, 28), GOLD, s, 0.5)


@card("r_hoja_envenenada", "poison", (22, 14))
def _(p, s):
    S(p, SPP + "poison_brand", 6, 0)
    drops(p, [(14, 26, 1.5), (19, 29, 1.0)], (90, 200, 60))


@card("r_tirar_los_dados", "gold", (22, 16))
def _(p, s):
    speed(p, 6, (0, 6, 12, 26), YELLOW, s, 0.4)
    die(p, 9, 14, 5)
    die(p, 25, 7, 3)
    sparks(p, 6, (0, 0, 44, 32), GOLD, s, 0.5)


@card("r_marcar_objetivo", "blood", (22, 16))
def _(p, s):
    ring(p, 22, 16, 12, RED)
    ring(p, 22, 16, 7, RED, 0.8)
    ring(p, 22, 16, 2, (255, 200, 200))
    for (a, b) in ((2, 8), (36, 42)):
        line(p, a, 16, b, 16, RED)
    for (a, b) in ((0, 6), (26, 32)):
        line(p, 22, a, 22, b, RED)


@card("mg_avivar", "ember", (28, 16))
def _(p, s):
    S(p, EF + "cloud_fire_2", 14, 0)
    speed(p, 6, (12, 8, 26, 26), (255, 230, 200), s, 0.5)
    S(p, MI + "misc_fan_new", -4, 4)


@card("mg_brisa", "air", (22, 14))
def _(p, s):
    for (y, a0, a1) in ((8, 0, 30), (16, 6, 40), (24, 2, 34)):
        for x in range(a0, a1):
            p.put(x, y + math.sin(x * 0.35) * 1.5, (220, 250, 250), 0.45)
    S(p, CO + "bird", 6, 0)
    for (x, y) in ((6, 12), (36, 20), (30, 5)):
        p.put(x, y, (120, 200, 90))
        p.put(x + 1, y, (80, 150, 60))


@card("mg_corriente", "air", (22, 26))
def _(p, s):
    glow(p, 22, 30, 16, (180, 240, 240), 0.5, ry=8)
    for k, x in enumerate((8, 16, 24, 32, 38)):
        top = 4 + (k % 3) * 2
        wisp(p, x, 31, top, (225, 252, 252), k * 1.3, 0.75, 1.2)
        arrowhead(p, x + math.sin(top * 0.45 + k * 1.3) * 1.2 * 1.5, top - 1, -90, WHITE, 2)
    for (x, y) in ((12, 14), (29, 9), (35, 20), (20, 22)):
        p.put(x, y, (130, 210, 100))
        p.put(x + 1, y - 1, (90, 160, 70))


@card("mg_ojo_huracan", "air", (22, 16))
def _(p, s):
    S(p, EF + "cloud_storm_2", 6, 0)
    glow(p, 22, 16, 7, CYAN, 0.8)
    S(p, CO + "eye", 6, 0)


@card("mg_remolino", "water", (22, 16))
def _(p, s):
    spiral(p, 22, 16, 18, 2.6, CYAN, 2)
    spiral(p, 22, 16, 12, 2.0, WHITE, 1, 0.6)


@card("mg_catalizador", "arcane", (22, 14))
def _(p, s):
    glow(p, 22, 14, 14, PURPLE, 0.45)
    S(p, MI + "misc_phial", 6, 0)
    for (x, y, c) in ((8, 6, ORANGE), (36, 6, BLUE), (8, 25, (200, 150, 90)), (36, 25, CYAN)):
        twinkle(p, x, y, 2, c)


# --- warrior magic
@card("wr_herida_abierta", "blood", (22, 12))
def _(p, s):
    glow(p, 22, 12, 16, RED, 0.45)
    slash(p, 22, 34, 24, 218, 322, (90, 0, 10), (60, 0, 6), 3.8)
    slash(p, 22, 34, 23, 222, 318, (255, 120, 110), (255, 120, 110), 0.9)
    for (x, top, L) in ((14, 16, 6), (21, 11, 9), (28, 12, 5)):
        for k in range(L):
            p.put(x, top + k, BLOOD)
        drop(p, x, top + L + 3, 1.6, BLOOD, (255, 140, 140))


@card("wr_vendaje", "earth", (22, 14))
def _(p, s):
    S(p, SC + "blank_paper", 6, 0)
    cross(p, 34, 8, RED, 3)


@card("wr_rugido", "ember", (20, 14))
def _(p, s):
    for r in (8, 12, 16):
        slash(p, 22, 16, r, 320, 40, (255, 220, 170), ORANGE, 1.0, 0.7)
        slash(p, 22, 16, r, 140, 220, (255, 220, 170), ORANGE, 1.0, 0.7)
    S(p, MN + "animals/grizzly_bear", 6, 0, halo=(ORANGE, 0.55))


@card("wr_sangria", "blood", (22, 16))
def _(p, s):
    icon(p, SPX + "sublimation_of_blood_new")


@card("wr_grito_batalla", "ember", (22, 16))
def _(p, s):
    icon(p, SPS + "recall_new")
    sparks(p, 6, (0, 0, 44, 32), ORANGE, s, 0.4)


@card("wr_pacto_hierro", "steel", (22, 14))
def _(p, s):
    S(p, SC + "scroll-grey", 6, 0)
    drop(p, 30, 21, 2.5, BLOOD, (255, 140, 140))


@card("wr_transfusion", "blood", (22, 14))
def _(p, s):
    S(p, PO + "sky_blue", -5, 0)
    S(p, PO + "ruby_new", 17, 0)
    for x in range(17, 27):
        p.put(x, 16, WHITE)
    for k in range(1, 4):
        p.put(27 - k, 16 - k, WHITE)
        p.put(27 - k, 16 + k, WHITE)


@card("wr_sangre_hirviente", "blood", (22, 18))
def _(p, s):
    S(p, EF + "flame_1", 6, 10)
    S(p, PO + "purple_red", 6, -4)
    for (x, y, r) in ((14, 4, 1.5), (26, 2, 1.0), (30, 6, 1.2)):
        bubble(p, x, y, r + 1, (255, 120, 120))


@card("wr_fortaleza_interior", "gold", (22, 12))
def _(p, s):
    glow(p, 22, 13, 16, GOLD, 0.5)
    S(p, DS + "statue_ancient_hero", 6, 0)
    heart(p, 22, 13, 2.4, (240, 70, 70))


@card("wr_regeneracion", "forest", (22, 16))
def _(p, s):
    icon(p, SPX + "regeneration_new")
    sparks(p, 6, (0, 0, 44, 32), GREEN, s, 0.5)


# --- epic
@card("ep_golpe_mortal", "shadow", (22, 14))
def _(p, s):
    glow(p, 22, 14, 17, (150, 60, 200), 0.5)
    S(p, Wp + "scythe_3", 6, 0)
    S(p, CO + "skull", 8, 6)


@card("ep_escudo_impenet", "gold", (22, 14))
def _(p, s):
    glow(p, 22, 14, 18, GOLD, 0.6)
    S(p, SH + "shield_of_reflection", 6, 0, halo=(GOLD, 0.8))
    sparks(p, 8, (0, 0, 44, 32), YELLOW, s, 0.5)


@card("ep_tormenta", "arcane", (22, 8))
def _(p, s):
    bolt(p, 14, 8, 9, 31, s, CYAN)
    bolt(p, 30, 8, 35, 31, s + 1, CYAN)
    bolt(p, 22, 8, 21, 22, s + 2, (150, 160, 255), segs=4)
    cloud(p, 22, 5, 40, 7, (70, 74, 120), s)


@card("ep_poder_oculto", "royal", (22, 16))
def _(p, s):
    glow(p, 22, 16, 18, PURPLE, 0.6)
    S(p, MI + "misc_quad", 6, 0, halo=(PURPLE, 0.7))
    sparks(p, 8, (0, 0, 44, 32), PINK, s, 0.4)


@card("ep_ejecucion", "blood", (22, 14))
def _(p, s):
    glow(p, 22, 14, 18, RED, 0.6)
    S(p, Wp + "executioner_axe_2_new", 6, 0)
    drops(p, [(10, 26, 1.8), (35, 27, 1.4)])


@card("ep_bastion", "gold", (22, 14))
def _(p, s):
    wall(p, 24, [DW + "brick_gray_2", DW + "brick_gray_3"])
    glow(p, 22, 14, 16, GOLD, 0.5)
    S(p, SH + "large_shield_3_new", 6, -1, halo=(GOLD, 0.7))


@card("ep_descarga", "arcane", (22, 16))
def _(p, s):
    icon(p, SPA + "static_discharge_new")


@card("ep_escudo_arcano", "arcane", (22, 14))
def _(p, s):
    ring(p, 22, 15, 15, PURPLE, 0.7)
    ring(p, 22, 15, 13, CYAN, 0.5)
    for k in range(8):
        a = k * math.pi / 4
        twinkle(p, 22 + math.cos(a) * 14, 15 + math.sin(a) * 14, 1, CYAN)
    S(p, SH + "lshield_louise", 6, 0)


@card("ep_lluvia_de_golpes", "steel", (22, 10))
def _(p, s):
    speed(p, 10, (0, 0, 44, 24), STEEL, s, 0.35, vertical=True)
    for (x, y) in ((-8, -6), (2, 0), (12, -9), (22, -2)):
        S(p, EF + "iron_shot_4", x, y)
    burst(p, 9, 27, 4, ORANGE)
    burst(p, 30, 28, 5, ORANGE)


@card("ep_mazo_impecable", "gold", (22, 14))
def _(p, s):
    glow(p, 22, 14, 17, GOLD, 0.6)
    S(p, MI + "misc_deck_legendary_new", 6, 0, halo=(GOLD, 0.8))
    sparks(p, 9, (0, 0, 44, 32), YELLOW, s, 0.5)


@card("ep_tormenta_veneno", "poison", (22, 8))
def _(p, s):
    for i in range(24):
        x = int(hash01(i, 51, s) * 46) - 2
        y = 10 + int(hash01(i, 52, s) * 20)
        line(p, x, y, x - 1, y + 3, TOXIC, 0.75)
    cloud(p, 22, 6, 40, 8, (90, 150, 60), s)
    sparks(p, 6, (0, 14, 44, 32), TOXIC, s + 1, 0.4)


@card("r_ritmo_letal", "shadow", (22, 16))
def _(p, s):
    icon(p, SPN + "song_of_slaying")


@card("r_tormenta_de_acero", "steel", (22, 6))
def _(p, s):
    for (x, y) in ((-6, 0), (5, 6), (16, -2), (27, 5)):
        S(p, Wp + "short_sword_2_new", x, y, rot=90)
    cloud(p, 22, 3, 44, 6, (96, 104, 124), s)


@card("r_rapina", "night", (22, 12))
def _(p, s):
    S(p, GO + "gold_pile_3", 13, 10)
    S(p, CO + "bird", 3, -2, fill=(74, 68, 98), halo=(PURPLE, 0.6))
    sparks(p, 6, (18, 14, 44, 32), GOLD, s, 0.5)


@card("r_maestra_de_dagas", "royal", (22, 16))
def _(p, s):
    glow(p, 22, 16, 14, PINK, 0.55)
    for k in range(8):
        ang = k * 45 - 90 + 22.5
        a = math.radians(ang)
        blade(p, 22 + math.cos(a) * 5, 16 + math.sin(a) * 4, ang, 10, 3)
    twinkle(p, 22, 16, 3, PINK)


@card("r_sombra_gemela", "shadow", (22, 14))
def _(p, s):
    S(p, CO + "person_2", -3, 0, fill=(24, 16, 34), halo=(PURPLE, 0.7))
    S(p, CO + "person_2", 15, 0, flip=True, fill=(70, 46, 110), halo=(CYAN, 0.5))


@card("r_fortuna_audaz", "gold", (22, 14))
def _(p, s):
    glow(p, 22, 14, 16, GOLD, 0.5)
    S(p, AM + "face_1_gold", 6, 0)
    sparks(p, 9, (0, 0, 44, 32), YELLOW, s, 0.5)


@card("r_nada_que_perder", "shadow", (22, 16))
def _(p, s):
    S(p, CO + "hand_3", 6, 0)
    speed(p, 4, (2, 16, 12, 32), STEEL, s, 0.35, vertical=True)
    speed(p, 4, (31, 14, 41, 32), STEEL, s + 1, 0.35, vertical=True)
    minicard(p, 32, 20, (60, 50, 90), 8, 11, 0.75)
    minicard(p, 3, 22, (60, 50, 90), 8, 11, 0.55)


@card("r_cadena_perfecta", "gold", (38, 16))
def _(p, s):
    glow(p, 38, 16, 9, GOLD, 0.7)
    chain(p, 5, 16, 5, 8.5, (200, 200, 214), GOLD)
    twinkle(p, 39, 8, 3, YELLOW)


@card("r_asesina", "shadow", (22, 12))
def _(p, s):
    glow(p, 22, 12, 15, RED, 0.4)
    S(p, BK + "cloak_3", 6, 0, fill=(28, 20, 36), halo=(RED, 0.45))
    for x in (19, 23):
        p.put(x, 7, (255, 60, 50))
    blade(p, 30, 28, -60, 10, 3)


@card("r_mil_cortes", "blood", (22, 16))
def _(p, s):
    for i in range(9):
        x0 = int(hash01(i, 61, s) * 30)
        y0 = 4 + int(hash01(i, 62, s) * 26)
        L = 8 + int(hash01(i, 63, s) * 10)
        up = hash01(i, 64, s) > 0.5
        streak(p, x0, y0, x0 + L, y0 - (L * 0.6 if up else -L * 0.3), WHITE, (255, 120, 120))
    drops(p, [(8, 28, 1.3), (36, 26, 1.1), (24, 30, 1.0)])


@card("mg_combustion", "ember", (22, 16))
def _(p, s):
    icon(p, SPF + "fire_storm_new")


@card("mg_avalancha", "earth", (22, 8))
def _(p, s):
    speed(p, 10, (0, 0, 44, 26), (210, 180, 140), s, 0.35, vertical=True)
    S(p, MI + "misc_stone_new", -6, -6)
    S(p, MI + "misc_stone_old", 16, -2)
    S(p, MI + "misc_stone_new", 4, 6)
    S(p, EF + "stone_0_new", 20, 10)


@card("mg_corazon_igneo", "ember", (22, 16))
def _(p, s):
    glow(p, 22, 16, 17, ORANGE, 0.6)
    S(p, EF + "flame_1", -4, 0)
    S(p, EF + "flame_2", 16, -2)
    heart(p, 22, 16, 8.5, (240, 90, 30), YELLOW, (40, 8, 2))


@card("mg_convergencia", "royal", (22, 16))
def _(p, s):
    glow(p, 22, 16, 9, WHITE, 0.6)
    ring(p, 22, 16, 11, (220, 200, 255), 0.5)
    S(p, EF + "flame_1", -7, -8)
    drop(p, 34, 9, 3, (60, 150, 235))
    S(p, EF + "stone_0_new", -7, 8)
    spiral(p, 34, 24, 5, 1.6, CYAN, 1)


@card("mg_archimago", "royal", (22, 12))
def _(p, s):
    glow(p, 22, 12, 16, PURPLE, 0.5)
    S(p, HG + "cornuthaum", 6, -1, halo=(PURPLE, 0.6))
    sparks(p, 10, (0, 0, 44, 32), (220, 200, 255), s, 0.5)


@card("wr_furia_desatada", "blood", (22, 16))
def _(p, s):
    icon(p, SPN + "berserker_rage_new", mode="normal", key=True)
    for (x0, y0, x1, y1) in ((2, 8, 10, 2), (34, 2, 42, 8), (2, 24, 10, 30), (34, 30, 42, 24)):
        streak(p, x0, y0, x1, y1, WHITE, RED)


@card("wr_ira_carmesi", "blood", (22, 14))
def _(p, s):
    glow(p, 22, 14, 18, RED, 0.6)
    S(p, WA + "spwpn_wrath_of_trog_new", 6, 0, halo=(RED, 0.7))
    sparks(p, 8, (0, 0, 44, 32), ORANGE, s, 0.4)


@card("wr_golpe_vampirico", "blood", (22, 14))
def _(p, s):
    glow(p, 22, 14, 16, (200, 30, 80), 0.5)
    S(p, WA + "spwpn_vampires_tooth", 6, 0)
    drops(p, [(9, 25, 1.6), (36, 24, 1.3), (14, 29, 1.0)])


@card("wr_doblar_escudo", "steel", (22, 14))
def _(p, s):
    glow(p, 26, 16, 14, STEEL, 0.4)
    S(p, SH + "elven_buckler_2", 0, -2, alpha=0.6)
    S(p, SH + "elven_buckler_2", 11, 2, halo=(STEEL, 0.6))


@card("wr_muralla_infranqueable", "gold", (22, 6))
def _(p, s):
    glow(p, 22, 8, 18, GOLD, 0.45)
    wall(p, 10, [DW + "metal_wall", DW + "metal_wall_brown"])
    sparks(p, 6, (0, 0, 44, 10), YELLOW, s, 0.5)


@card("wr_piel_acero", "steel", (22, 14))
def _(p, s):
    S(p, TO + "silver_dragon_scale_mail_new", 6, 0)
    twinkle(p, 16, 9, 3)
    twinkle(p, 28, 20, 2)


@card("wr_berserker", "blood", (22, 14))
def _(p, s):
    glow(p, 22, 14, 17, RED, 0.55)
    icon(p, SPM + "beastly_appendage", mode="normal", key=True)
    sparks(p, 8, (0, 0, 44, 32), ORANGE, s, 0.4)


@card("wr_voluntad_hierro", "stone", (22, 14))
def _(p, s):
    glow(p, 22, 14, 16, STEEL, 0.4)
    S(p, DS + "statue_iron", 6, 0)


# --- starting cards and tokens
@card("espadazo_base", "steel", (22, 14))
def _(p, s):
    S(p, Wp + "long_sword_1_new", 6, 0)
    slash(p, 22, 24, 16, 200, 330, WHITE, STEEL, 2.0)


@card("bloqueo_base", "steel", (22, 14))
def _(p, s):
    S(p, SH + "shield_2_kite", 6, 0)


@card("guardia_ferrea_base", "stone", (22, 14))
def _(p, s):
    S(p, HG + "helmet_1_visored", 6, 0)
    twinkle(p, 15, 8, 2)


@card("proyectil_base", "arcane", (22, 16))
def _(p, s):
    icon(p, SPC + "magic_dart")


@card("chispa_ignea_base", "ember", (22, 16))
def _(p, s):
    icon(p, SPF + "throw_flame_new")


@card("egida_base", "arcane", (22, 14))
def _(p, s):
    glow(p, 22, 14, 15, CYAN, 0.45)
    ring(p, 22, 15, 14, CYAN, 0.5)
    S(p, SH + "shield_1_elven", 6, 0)


@card("punalada_base", "night", (24, 12))
def _(p, s):
    S(p, Wp + "dagger_new", 6, 0)
    burst(p, 27, 9, 4, WHITE, WHITE, 8, 0.39)


@card("esquiva_base", "night", (24, 14))
def _(p, s):
    S(p, CO + "running", -1, 0, fill=(130, 110, 190), alpha=0.3)
    S(p, CO + "running", 4, 0, fill=(140, 120, 200), alpha=0.5)
    S(p, CO + "running", 9, 0)


@card("finta_base", "night", (22, 14))
def _(p, s):
    arc_path(p, 22, 22, 16, 190, 350, STEEL)
    S(p, Wp + "elven_short_sword", 2, 0, fill=(90, 80, 150), alpha=0.35)
    S(p, Wp + "elven_short_sword", 7, 0)


@card("status_herida", "stone", (22, 14))
def _(p, s):
    S(p, BL + "blood_puddle_red_2", 6, 0)
    streak(p, 10, 22, 34, 10, (255, 200, 200), BLOOD)


@card("t_daga_oculta", "shadow", (24, 12))
def _(p, s):
    glow(p, 24, 12, 14, PURPLE, 0.5)
    S(p, Wp + "dagger_6", 6, 0)
    twinkle(p, 30, 6, 3)


# ------------------------------------------------------------------ build, check, write
def build(cid):
    pal, light, fn = R[cid]
    seed = zlib.crc32(cid.encode("utf-8")) & 0xFFFF
    p = Pic()
    backdrop(p, pal, light, seed)
    fn(p, seed)
    return p.image()


def card_ids():
    with open(CARDS_JSON, "r", encoding="utf-8") as fh:
        return list(json.load(fh).keys())


def guid(rel):
    return hashlib.md5(("LastWish/" + rel).encode("utf-8")).hexdigest()


def write_meta(path):
    if os.path.exists(path + ".meta"):
        return
    with open(os.path.join(OUT_DIR, META_TEMPLATE), "r", encoding="utf-8") as fh:
        text = fh.read()
    rel = os.path.relpath(path, ROOT).replace("\\", "/")
    lines = ["guid: " + guid(rel) if ln.startswith("guid: ") else ln for ln in text.splitlines()]
    with open(path + ".meta", "w", encoding="utf-8", newline="\n") as fh:
        fh.write("\n".join(lines) + "\n")


def layout_text(art_ids):
    with open(LAYOUT, "r", encoding="utf-8") as fh:
        layout = json.load(fh)
    layout["art"] = sorted(art_ids)
    return json.dumps(layout, indent=1)


def contact(ids, images, path, names):
    cols = 6
    cw, ch = W * 3 + 8, H * 3 + 20
    rows = (len(ids) + cols - 1) // cols
    sheet = Image.new("RGBA", (cols * cw, rows * ch), (36, 36, 46, 255))
    d = ImageDraw.Draw(sheet)
    for i, cid in enumerate(ids):
        x, y = (i % cols) * cw + 4, (i // cols) * ch + 2
        sheet.alpha_composite(images[cid].resize((W * 3, H * 3), Image.NEAREST), (x, y))
        d.text((x, y + H * 3 + 2), (names.get(cid, cid))[:24], fill=(230, 230, 236, 255))
    sheet.save(path)


def main(argv=None):
    global TILES
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--source-dir", default=DEFAULT_SOURCE)
    ap.add_argument("--only", default="")
    ap.add_argument("--preview", default="")
    ap.add_argument("--check", action="store_true")
    args = ap.parse_args(argv)
    tiles_root = os.path.join(args.source_dir, DCSS)
    if not os.path.isdir(tiles_root):
        sys.stderr.write("no encuentro los tiles de Dungeon Crawl en %s (usa --source-dir)\n" % tiles_root)
        return 2
    TILES = Tiles(tiles_root)

    ids = card_ids()
    missing = [c for c in ids if c not in R and c not in ORIGINAL_ART]
    extra = [c for c in R if c not in ids]
    overlap = [c for c in R if c in ORIGINAL_ART]
    if missing or extra or overlap:
        sys.stderr.write("recetas: faltan %s, sobran %s, pisan arte original %s\n" % (missing, extra, overlap))
        return 1
    chosen = [c for c in ids if c in R]
    if args.only:
        wanted = [s.strip() for s in args.only.split(",") if s.strip()]
        unknown = [c for c in wanted if c not in R]
        if unknown:
            sys.stderr.write("id desconocido o con arte original: %s\n" % unknown)
            return 2
        chosen = wanted

    failed = False
    images = {}
    for cid in chosen:
        try:
            images[cid] = build(cid)
        except FileNotFoundError as e:
            sys.stderr.write("%s: %s\n" % (cid, e))
            return 2

    # every card must look different from every other one
    seen = {}
    for cid, im in images.items():
        h = hashlib.md5(im.tobytes()).hexdigest()
        if h in seen:
            failed = True
            sys.stderr.write("%s y %s son idénticas\n" % (seen[h], cid))
        seen[h] = cid

    art_ids = sorted(set(ORIGINAL_ART) | set(R))
    text = layout_text(art_ids)
    for cid in chosen:
        path = os.path.join(OUT_DIR, cid + ".png")
        if args.check:
            if not os.path.exists(path) or Image.open(path).convert("RGBA").tobytes() != images[cid].tobytes():
                failed = True
                sys.stderr.write("differs: %s\n" % os.path.relpath(path, ROOT))
        else:
            images[cid].save(path)
            write_meta(path)
    if args.check:
        with open(LAYOUT, "r", encoding="utf-8") as fh:
            if fh.read().rstrip("\n") != text:
                failed = True
                sys.stderr.write("differs: %s\n" % os.path.relpath(LAYOUT, ROOT))
    else:
        with open(LAYOUT, "w", encoding="utf-8", newline="\n") as fh:
            fh.write(text)
    if args.preview:
        os.makedirs(args.preview, exist_ok=True)
        with open(CARDS_JSON, "r", encoding="utf-8") as fh:
            names = {k: v["name"] for k, v in json.load(fh).items()}
        for k in range(0, len(chosen), 36):
            part = chosen[k:k + 36]
            contact(part, images, os.path.join(args.preview, "cards_%02d.png" % (k // 36)), names)
    print("%d ilustraciones%s, %d tiles de Dungeon Crawl" % (len(chosen), " comprobadas" if args.check else "", len(TILES.used)))
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
