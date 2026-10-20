#!/usr/bin/env python3
"""Pixel-art sheets of the floor-1 expansion enemies (deterministic, Pillow only).

Each enemy is a small recipe: shaded primitives (lambert light from the top-left, 4x4 Bayer dithering between the tones
of a ramp, hash noise for texture) drawn on a solid body layer that gets a dark 1 px outline, plus an effects layer
(glows, sparks, rings) that may be semi-transparent. Poses are functions of the animation and frame; every action
except death and terminal ones ends on the idle frame 0 pixel for pixel, hurt flashes 72/53/23 % towards white and
death dissolves from the bottom up, like the original generators.

The sheets use exactly the JSON keys of the 19 existing sheets (cell_w, cell_h, columns, anchor, scale, sheet,
animations, events, moves, [terminal], [boss, elite], top_native, idle_bbox_native).

Usage:
  python Tools/GenerateEnemyArt.py                 writes the 17 sheets (+ .meta if missing) in Assets/Resources/Game/Enemies
  python Tools/GenerateEnemyArt.py --only rat,larva
  python Tools/GenerateEnemyArt.py --preview DIR   also writes a labelled x2 contact sheet per enemy in DIR
  python Tools/GenerateEnemyArt.py --styles        prints the EnemyAnimator.Styles points (chest, eyes, body, claw, shadow)
  python Tools/GenerateEnemyArt.py --check         regenerates in memory and compares with the files (exit 1 on difference)
Exit codes: 0 ok, 1 differences or validation failure, 2 bad usage.
"""
import argparse
import hashlib
import io
import json
import math
import os
import sys

try:
    from PIL import Image, ImageDraw
except ImportError:  # pragma: no cover
    sys.stderr.write("Pillow 11 es necesario: python -m pip install pillow==11.0.0\n")
    sys.exit(2)

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT_DIR = os.path.join(ROOT, "Assets", "Resources", "Game", "Enemies")
EXISTING = {"acolyte", "bat", "bomb", "executioner", "eye", "gargoyle", "golem", "hag", "imp", "knight", "mimic", "minotaur",
            "mycelid", "scorpion", "skull", "slime", "weaver", "worm", "wraith"}

BAYER = [[0, 8, 2, 10], [12, 4, 14, 6], [3, 11, 1, 9], [15, 7, 13, 5]]
_L = (-0.52, -0.64, 0.56)
_n = math.sqrt(sum(c * c for c in _L))
LIGHT = tuple(c / _n for c in _L)
HURT_FLASH = (0.72, 0.53, 0.23)


def hash01(x, y, s=0):
    h = (x * 374761393 + y * 668265263 + s * 982451653) & 0xFFFFFFFF
    h = ((h ^ (h >> 13)) * 1274126177) & 0xFFFFFFFF
    return ((h ^ (h >> 16)) & 0xFFFF) / 65535.0


def mix(a, b, t):
    return tuple(int(round(a[i] + (b[i] - a[i]) * t)) for i in range(3))


def ramp(*colors):
    return [tuple(c) for c in colors]


# ------------------------------------------------------------------ frame and primitives
class Frame:
    def __init__(self, w, h):
        self.w, self.h = w, h
        self.body = [None] * (w * h)
        self.fx = [None] * (w * h)  # (r, g, b, a)

    def put(self, x, y, c):
        x, y = int(x), int(y)
        if 0 <= x < self.w and 0 <= y < self.h:
            self.body[y * self.w + x] = c

    def get(self, x, y):
        if 0 <= x < self.w and 0 <= y < self.h:
            return self.body[y * self.w + x]
        return None

    def glow(self, x, y, c, a):
        x, y = int(x), int(y)
        if not (0 <= x < self.w and 0 <= y < self.h) or a <= 0:
            return
        a = min(255, int(a))
        i = y * self.w + x
        old = self.fx[i]
        if old is None:
            self.fx[i] = (c[0], c[1], c[2], a)
        else:
            oa = old[3] / 255.0
            na = a / 255.0
            out = na + oa * (1 - na)
            col = tuple(int((c[k] * na + old[k] * oa * (1 - na)) / max(out, 1e-6)) for k in range(3))
            self.fx[i] = (col[0], col[1], col[2], int(out * 255))


def _tone(rmp, v, x, y, dither=0.9):
    n = len(rmp)
    f = v * (n - 1) + (BAYER[y & 3][x & 3] / 16.0 - 0.47) * dither
    i = int(round(f))
    return rmp[0 if i < 0 else n - 1 if i >= n else i]


def _lit(nx, ny, nz, amb=0.2):
    lam = nx * LIGHT[0] + ny * LIGHT[1] + nz * LIGHT[2]
    if lam < 0:
        lam = 0
    return amb + (1 - amb) * lam


def ellipse(f, cx, cy, rx, ry, rmp, rot=0.0, tex=0.0, seed=0, mod=None, amb=0.2, clip=None):
    """Shaded ellipse (a lit dome). mod(x, y, v) can change the light value (bands, spots)."""
    if rx <= 0 or ry <= 0:
        return
    c, s = math.cos(rot), math.sin(rot)
    r = max(rx, ry) + 1
    for y in range(int(math.floor(cy - r)), int(math.ceil(cy + r)) + 1):
        for x in range(int(math.floor(cx - r)), int(math.ceil(cx + r)) + 1):
            u, w = x + 0.5 - cx, y + 0.5 - cy
            lu, lw = u * c + w * s, -u * s + w * c
            nx, ny = lu / rx, lw / ry
            d = nx * nx + ny * ny
            if d > 1:
                continue
            if clip and not clip(x, y):
                continue
            nz = math.sqrt(1 - d)
            wx, wy = nx * c - ny * s, nx * s + ny * c
            v = _lit(wx * 0.9, wy * 0.9, nz, amb)
            if tex:
                v += (hash01(x, y, seed) - 0.5) * tex
            if mod:
                v = mod(x, y, v)
            f.put(x, y, _tone(rmp, max(0.0, min(1.0, v)), x, y))


def capsule(f, x0, y0, x1, y1, r0, r1, rmp, tex=0.0, seed=0, mod=None, amb=0.25):
    """A shaded limb from (x0, y0) radius r0 to (x1, y1) radius r1."""
    dx, dy = x1 - x0, y1 - y0
    ll = dx * dx + dy * dy
    rr = max(r0, r1) + 1
    for y in range(int(math.floor(min(y0, y1) - rr)), int(math.ceil(max(y0, y1) + rr)) + 1):
        for x in range(int(math.floor(min(x0, x1) - rr)), int(math.ceil(max(x0, x1) + rr)) + 1):
            px, py = x + 0.5 - x0, y + 0.5 - y0
            t = 0.0 if ll == 0 else max(0.0, min(1.0, (px * dx + py * dy) / ll))
            qx, qy = px - t * dx, py - t * dy
            r = r0 + (r1 - r0) * t
            d2 = qx * qx + qy * qy
            if r <= 0 or d2 > r * r:
                continue
            nx, ny = qx / r, qy / r
            nz = math.sqrt(max(0.0, 1 - nx * nx - ny * ny))
            v = _lit(nx * 0.85, ny * 0.85, nz, amb)
            if tex:
                v += (hash01(x, y, seed) - 0.5) * tex
            if mod:
                v = mod(x, y, v)
            f.put(x, y, _tone(rmp, max(0.0, min(1.0, v)), x, y))


def curve(f, pts, radii, rmp, tex=0.0, seed=0, mod=None):
    """A chain of capsules through the points (tails, necks, chains)."""
    for i in range(len(pts) - 1):
        capsule(f, pts[i][0], pts[i][1], pts[i + 1][0], pts[i + 1][1], radii[i], radii[i + 1], rmp, tex, seed, mod)


def polygon(f, pts, rmp, flat=None, tex=0.0, seed=0, mod=None, amb=0.25):
    """Filled polygon, rounded shading from its bounding box (or a flat light value)."""
    xs = [p[0] for p in pts]
    ys = [p[1] for p in pts]
    minx, maxx, miny, maxy = min(xs), max(xs), min(ys), max(ys)
    cx, cy = (minx + maxx) / 2, (miny + maxy) / 2
    hw, hh = max(1.0, (maxx - minx) / 2), max(1.0, (maxy - miny) / 2)
    n = len(pts)
    for y in range(int(math.floor(miny)), int(math.ceil(maxy)) + 1):
        yc = y + 0.5
        nodes = []
        for i in range(n):
            ax, ay = pts[i]
            bx, by = pts[(i + 1) % n]
            if (ay <= yc < by) or (by <= yc < ay):
                nodes.append(ax + (yc - ay) / (by - ay) * (bx - ax))
        nodes.sort()
        for k in range(0, len(nodes) - 1, 2):
            for x in range(int(math.ceil(nodes[k] - 0.5)), int(math.floor(nodes[k + 1] - 0.5)) + 1):
                if flat is not None:
                    v = flat
                else:
                    nx, ny = (x + 0.5 - cx) / hw * 0.7, (yc - cy) / hh * 0.7
                    v = _lit(nx, ny, math.sqrt(max(0.0, 1 - nx * nx - ny * ny)), amb)
                if tex:
                    v += (hash01(x, y, seed) - 0.5) * tex
                if mod:
                    v = mod(x, y, v)
                f.put(x, y, _tone(rmp, max(0.0, min(1.0, v)), x, y))


def line(f, x0, y0, x1, y1, color, fx_alpha=None):
    n = int(max(abs(x1 - x0), abs(y1 - y0))) + 1
    for i in range(n + 1):
        t = i / max(1, n)
        x, y = round(x0 + (x1 - x0) * t), round(y0 + (y1 - y0) * t)
        if fx_alpha is None:
            f.put(x, y, color)
        else:
            f.glow(x, y, color, fx_alpha)


def spot(f, x, y, color, size=1):
    for j in range(size):
        for i in range(size):
            f.put(x + i, y + j, color)


def halo(f, cx, cy, r, color, alpha, core=None):
    """Soft glow on the effects layer."""
    for y in range(int(cy - r - 1), int(cy + r + 2)):
        for x in range(int(cx - r - 1), int(cx + r + 2)):
            d = math.hypot(x + 0.5 - cx, y + 0.5 - cy) / max(r, 0.5)
            if d >= 1:
                continue
            a = alpha * (1 - d) ** 1.6
            if (BAYER[y & 3][x & 3] / 16.0) > (1 - d) * 1.4 and d > 0.55:
                continue
            f.glow(x, y, core if core and d < 0.35 else color, a)


def ring(f, cx, cy, r, color, alpha, width=1.2, arc=(0, math.tau)):
    for y in range(int(cy - r - 2), int(cy + r + 3)):
        for x in range(int(cx - r - 2), int(cx + r + 3)):
            d = math.hypot(x + 0.5 - cx, y + 0.5 - cy)
            if abs(d - r) > width:
                continue
            ang = math.atan2(y + 0.5 - cy, x + 0.5 - cx) % math.tau
            a0, a1 = arc[0] % math.tau, arc[1] % math.tau
            inside = a0 <= ang <= a1 if a0 <= a1 else (ang >= a0 or ang <= a1)
            if arc != (0, math.tau) and not inside:
                continue
            f.glow(x, y, color, alpha * (1 - abs(d - r) / (width + 0.01)))


def sparks(f, cx, cy, n, spread, color, alpha, seed, life=1.0):
    for k in range(n):
        a = hash01(k, 3, seed) * math.tau
        d = spread * (0.3 + 0.7 * hash01(k, 7, seed)) * life
        x, y = cx + math.cos(a) * d, cy + math.sin(a) * d * 0.7
        f.glow(x, y, color, alpha)
        f.glow(x + 1, y, color, alpha * 0.5)


def eye(f, x, y, color, glow_color=None, glow=0.0):
    f.put(x, y, color)
    if glow_color and glow > 0:
        halo(f, x + 0.5, y + 0.5, 3, glow_color, 90 * glow)


# ------------------------------------------------------------------ finishing passes
def outline_color(c):
    k = 0.32
    out = tuple(int(v * k) for v in c)
    while sum(out) >= 58:
        k *= 0.8
        out = tuple(int(v * k) for v in c)
    return out


def finish(f, flash=0.0, dissolve=0.0, reveal=None, seed=1, motes=None):
    """Outline, hurt flash, bottom-up dissolve (death) or reveal (spawn) → RGBA bytes."""
    w, h = f.w, f.h
    body = f.body
    out = list(body)
    for y in range(h):
        for x in range(w):
            if body[y * w + x] is not None:
                continue
            best = None
            for nx, ny in ((x - 1, y), (x + 1, y), (x, y - 1), (x, y + 1)):
                if 0 <= nx < w and 0 <= ny < h and body[ny * w + nx] is not None:
                    best = body[ny * w + nx]
                    break
            if best is not None:
                out[y * w + x] = outline_color(best)
    solid = [c is not None for c in out]
    if solid:
        ys = [i // w for i, s in enumerate(solid) if s]
        top, bottom = (min(ys), max(ys)) if ys else (0, h - 1)
    data = bytearray(w * h * 4)
    fx_scale = 1.0
    if dissolve > 0:
        fx_scale = max(0.0, 1 - dissolve * 1.3)
    if reveal is not None:
        fx_scale = min(fx_scale, reveal)
    for i in range(w * h):
        c = out[i]
        x, y = i % w, i // w
        a = 0
        if c is not None:
            if flash > 0:
                c = mix(c, (255, 255, 255), flash)
            a = 255
            if dissolve > 0 or reveal is not None:
                hgt = (bottom - y) / max(1, bottom - top)  # 0 at the feet, 1 at the top
                jitter = hash01(x, y, seed) * 0.35
                if dissolve > 0 and hgt + jitter < dissolve * 1.4:
                    a = 0
                    if motes is not None and hgt + jitter > dissolve * 1.4 - 0.08 and hash01(x, y, seed + 9) < 0.18:
                        motes.append((x, y, c))
                if reveal is not None and hgt - jitter * 0.5 > reveal * 1.3 - 0.1:
                    a = 0
        if a:
            data[i * 4:i * 4 + 4] = bytes((c[0], c[1], c[2], 255))
        fxc = f.fx[i]
        if fxc is not None and fx_scale < 1:
            fxc = (fxc[0], fxc[1], fxc[2], int(fxc[3] * fx_scale))
        if fxc is not None and fxc[3] > 0:
            if a:
                fa = fxc[3] / 255.0
                cur = data[i * 4:i * 4 + 3]
                data[i * 4:i * 4 + 3] = bytes(int(fxc[k] * fa + cur[k] * (1 - fa)) for k in range(3))
            else:
                data[i * 4:i * 4 + 4] = bytes((fxc[0], fxc[1], fxc[2], fxc[3]))
    return Image.frombytes("RGBA", (w, h), bytes(data))


# ------------------------------------------------------------------ animation timing
def act_curves(i, n, strikes):
    """Wind-up w (0..1), strike s (0..1) and k (0..1) progress of action frame i; the last frame is neutral."""
    if i >= n - 1:
        return 0.0, 0.0, 1.0
    first = strikes[0] if strikes else n // 2
    k = i / (n - 1)
    if not strikes:
        return 0.0, math.sin(math.pi * min(1.0, i / max(1, n - 2))), k
    if i < first:
        return (i + 1) / first, 0.0, k
    best = 0.0
    for s in strikes:
        d = i - s
        if d == 0:
            best = max(best, 1.0)
        elif d == -1:
            best = max(best, 0.35)
        elif d > 0:
            best = max(best, max(0.0, 1 - d / max(2.0, (n - 1 - strikes[-1]) * 0.75)) * (0.75 if d == 1 else 0.5))
    tail = max(0.0, 1 - (i - first) / 2.0) * 0.25
    return tail, best, k


def durations_for(kind, n, strikes):
    if kind == "idle":
        return [105] * n
    if kind == "hurt":
        return [55, 60, 70, 80, 90, 100][:n] + [100] * max(0, n - 6)
    if kind == "death":
        return [70, 90] + [85] * (n - 3) + [120]
    if kind == "spawn":
        return [90] * n
    out = []
    first = strikes[0] if strikes else n // 2
    for i in range(n):
        if i < first - 1:
            out.append(110 + 10 * (i % 2))
        elif i in strikes or i == first - 1:
            out.append(55 + 5 * (i % 2))
        elif i == n - 1:
            out.append(110)
        else:
            out.append(80 + 5 * min(4, i - first))
    return out


# ------------------------------------------------------------------ recipe base
class Recipe:
    id = ""
    cell = (128, 100)
    anchor = (84, 94)
    boss = False
    elite = False
    terminal = []
    # (name, frames, strikes, kind) — kind: idle | action | hurt | death | spawn | terminal
    anims = []
    moves = {}
    points = {}  # chest, eyes, body, claw relative to the anchor (native px)
    seed = 1

    def idle_pose(self, phase):
        return {"t": phase, "b": math.sin(math.tau * phase), "w": 0.0, "s": 0.0, "k": 0.0, "dx": 0, "dy": 0, "anim": "idle"}

    def pose(self, anim, kind, i, n, strikes):
        if kind == "idle":
            return self.idle_pose(i / n)
        p = self.idle_pose(0.0)
        if kind in ("hurt", "death", "spawn"):
            return p
        w, s, k = act_curves(i, n, strikes)
        p.update(anim=anim, w=w, s=s, k=k, i=i, n=n)
        if i >= n - 1 and kind != "terminal":
            return self.idle_pose(0.0)
        return p

    def draw(self, f, p):  # pragma: no cover - overridden
        raise NotImplementedError

    # -- rendering
    def frame(self, p, flash=0.0, dissolve=0.0, reveal=None, ox=0, oy=0, fx=None):
        f = Frame(*self.cell)
        ax, ay = self.anchor
        p = dict(p)
        p["ox"], p["oy"] = ax + p.get("dx", 0) + ox, ay + p.get("dy", 0) + oy
        self.draw(f, p)
        motes = [] if dissolve > 0 else None
        img = finish(f, flash, dissolve, reveal, self.seed, motes)
        if motes:
            px = img.load()
            for (x, y, c) in motes[:60]:
                for k in range(1, 4):
                    yy = y - k * 2 - int(dissolve * 6)
                    if 0 <= yy < img.height:
                        px[x, yy] = (min(255, c[0] + 60), min(255, c[1] + 60), min(255, c[2] + 60), 150 - k * 30)
        if fx:
            fx(img)
        return img

    def render(self):
        rows = []
        for (name, n, strikes, kind) in self.anims:
            frames = []
            if kind == "hurt":
                base = self.idle_pose(0.0)
                offs = [(2, 0), (3, 0), (2, 0), (3, -1), (1, 0), (0, 0)]
                for i in range(n):
                    fl = HURT_FLASH[i] if i < 3 else 0.0
                    dx, dy = offs[min(i, len(offs) - 1)]
                    if i == n - 1:
                        dx, dy = 0, 0
                    frames.append(self.frame(base, flash=fl, ox=dx, oy=dy))
            elif kind == "death":
                base = self.idle_pose(0.0)
                for i in range(n):
                    if i == 0:
                        frames.append(self.frame(base, flash=0.7, ox=2))
                    elif i == 1:
                        frames.append(self.frame(base, flash=0.4, ox=3))
                    else:
                        d = (i - 1) / (n - 2)
                        frames.append(self.frame(base, flash=max(0.0, 0.25 - d * 0.3), dissolve=min(1.0, d * 1.02), ox=3, oy=int(d * 2)))
            elif kind == "spawn":
                base = self.idle_pose(0.0)
                for i in range(n):
                    if i == n - 1:
                        frames.append(self.frame(base))
                    else:
                        r = (i + 1) / (n - 1)
                        frames.append(self.frame(self.spawn_pose(r), reveal=r, oy=int((1 - r) * 6)))
            else:
                for i in range(n):
                    frames.append(self.frame(self.pose(name, kind, i, n, strikes)))
            rows.append((name, n, strikes, kind, frames))
        return rows

    def spawn_pose(self, r):
        return self.idle_pose(0.0)


# ------------------------------------------------------------------ palettes
FLESH = ramp((52, 30, 38), (110, 70, 72), (168, 126, 110), (214, 180, 146), (240, 218, 170), (252, 240, 210))
EGG = ramp((40, 70, 20), (90, 140, 30), (150, 200, 60), (196, 236, 110), (236, 255, 180))
CHITIN_BROWN = ramp((40, 24, 18), (84, 52, 32), (130, 84, 46), (176, 124, 70))
GRUB = ramp((70, 54, 40), (140, 120, 84), (206, 190, 140), (236, 226, 182), (250, 246, 220))
BONE = ramp((70, 60, 52), (130, 116, 98), (190, 176, 150), (226, 214, 186), (248, 242, 222))
HOG = ramp((30, 22, 24), (62, 46, 44), (98, 76, 68), (138, 112, 96), (170, 146, 124))
TOAD = ramp((28, 40, 20), (58, 82, 34), (96, 122, 50), (138, 160, 70), (180, 196, 104), (214, 224, 150))
BELLY = ramp((120, 110, 70), (180, 170, 116), (220, 212, 160), (244, 238, 196))
TONGUE = ramp((90, 20, 40), (170, 50, 80), (226, 100, 130), (250, 160, 180))
CROW = ramp((12, 10, 18), (32, 26, 44), (56, 44, 76), (84, 66, 112), (122, 100, 160))
BEAK = ramp((70, 60, 40), (150, 134, 100), (210, 196, 160), (240, 232, 204))
RATFUR = ramp((34, 28, 30), (70, 58, 56), (106, 90, 84), (142, 124, 112), (176, 160, 146))
PINK = ramp((96, 44, 54), (160, 84, 92), (210, 130, 134), (240, 176, 172))
BEETLE = ramp((16, 30, 34), (30, 66, 70), (50, 112, 112), (84, 168, 156), (150, 220, 200), (220, 250, 236))
BRONZE = ramp((50, 30, 16), (100, 64, 30), (160, 110, 50), (210, 160, 84), (244, 210, 140))
LEGS = ramp((14, 12, 14), (34, 30, 34), (60, 54, 60))
GEL = ramp((60, 16, 60), (110, 30, 106), (160, 54, 150), (206, 92, 196), (236, 150, 230), (252, 214, 248))
GELBONE = ramp((140, 80, 130), (190, 140, 170), (226, 196, 210))
VIPER = ramp((16, 40, 22), (34, 80, 36), (60, 128, 48), (100, 176, 66), (160, 214, 100))
YELLOWBAND = ramp((90, 80, 20), (170, 150, 40), (220, 200, 70), (246, 232, 130))
GOLD = ramp((90, 54, 14), (160, 104, 30), (214, 156, 48), (246, 210, 100), (255, 244, 190))
CLOAK = ramp((40, 8, 14), (84, 18, 28), (130, 30, 40), (172, 52, 58))
IRON = ramp((18, 18, 24), (36, 38, 48), (58, 62, 76), (86, 92, 108), (120, 126, 142), (160, 166, 180))
GRATE = ramp((20, 10, 8), (60, 24, 12), (110, 40, 16))
SPECTRAL = ramp((10, 40, 44), (20, 80, 84), (40, 130, 130), (80, 184, 176), (150, 230, 218), (220, 255, 246))
HAT = ramp((12, 14, 20), (30, 34, 44), (52, 58, 72))
ROBE = ramp((40, 6, 10), (86, 14, 22), (140, 26, 34), (186, 50, 54), (216, 90, 84))
ROBE_DARK = ramp((10, 8, 12), (26, 22, 30), (46, 40, 52), (70, 62, 78))
SKIN = ramp((60, 40, 36), (120, 86, 70), (180, 140, 112), (220, 184, 150))
SERPENT = ramp((8, 30, 22), (16, 62, 42), (30, 104, 62), (56, 150, 84), (110, 196, 120), (176, 230, 170))
AMBER = ramp((110, 60, 14), (176, 110, 30), (226, 164, 60), (250, 210, 120))
COAT = ramp((30, 20, 14), (62, 42, 28), (98, 68, 44), (134, 98, 64), (166, 130, 90))
WOOD = ramp((44, 26, 14), (90, 58, 32), (140, 96, 56), (180, 134, 84))
STEEL = ramp((40, 44, 52), (90, 96, 110), (150, 158, 172), (210, 216, 226), (244, 248, 252))
VEIL = ramp((30, 26, 44), (62, 54, 84), (100, 90, 128), (142, 132, 170), (186, 178, 210), (226, 222, 240))
PALE = ramp((90, 90, 110), (150, 150, 172), (204, 204, 222), (236, 236, 248))
DIRT = ramp((34, 22, 16), (66, 44, 30), (100, 72, 50), (136, 104, 74))
RED_EYE = (255, 70, 60)
TEAL_GLOW = (120, 255, 230)
GREEN_GLOW = (190, 255, 110)
FIRE = (255, 150, 50)
BLUE_TEAR = (140, 210, 255)


def bands(period, depth, axis="x", phase=0.0):
    def mod(x, y, v):
        t = (x if axis == "x" else y) + phase
        return v - depth * (0.5 + 0.5 * math.cos(math.tau * t / period)) ** 6
    return mod


# ------------------------------------------------------------------ the 17 enemies
class Broodmother(Recipe):
    id = "broodmother"; cell = (160, 108); anchor = (112, 102); seed = 11
    anims = [("idle", 12, [], "idle"), ("attack", 10, [4], "action"), ("lay", 10, [6], "action"), ("cast", 9, [], "action"),
             ("hurt", 6, [], "hurt"), ("death", 14, [], "death")]
    moves = {"spit": "attack", "lay": "lay", "feed": "cast", "vengeance": "cast"}
    points = {"chest": (-6, -30), "eyes": (-30, -34), "body": (6, -26), "claw": (-46, -30)}

    def draw(self, f, p):
        ox, oy, b = p["ox"], p["oy"], p["b"]
        a = p["anim"]
        pulse = p["s"] if a == "lay" else 0.0
        head = -p["w"] * 4 + p["s"] * 7 if a == "attack" else 0.0
        for k, lx in enumerate((-18, -6, 6, 18)):  # legs
            capsule(f, ox + lx, oy - 16, ox + lx - 6 - k, oy - 1, 2.2, 1.4, CHITIN_BROWN)
        ellipse(f, ox + 12, oy - 26 + b * 0.6, 30 + pulse * 2, 19 + b * 0.8 + pulse * 2, FLESH, tex=0.12, seed=3,
                mod=bands(9, 0.28, "x", 3))
        for (ex, ey, r) in ((4, -42, 4.2), (13, -44, 5.0), (22, -41, 4.4), (30, -35, 3.8), (17, -36, 3.6), (8, -35, 3.2)):
            ellipse(f, ox + ex, oy + ey + b * 0.6 - pulse * 2, r, r * 0.85, EGG)
            halo(f, ox + ex - 1, oy + ey - 1 + b * 0.6, r + 2, GREEN_GLOW, 50 + 70 * (p["s"] if a == "cast" else 0.2 + 0.1 * b))
        hx = ox - 24 - head
        ellipse(f, hx, oy - 24 + b * 0.3, 12, 10, FLESH, tex=0.1, seed=4)
        polygon(f, [(hx - 9, oy - 20), (hx - 17, oy - 17), (hx - 11, oy - 14)], CHITIN_BROWN)
        polygon(f, [(hx - 8, oy - 15), (hx - 15, oy - 10), (hx - 9, oy - 11)], CHITIN_BROWN)
        for (ex, ey) in ((-6, -29), (-2, -31), (-8, -26)):
            f.put(hx + ex + 3, oy + ey + 2, RED_EYE)
        if a == "attack" and p["s"] > 0.3:
            d = (1 - p["s"]) * 30
            ellipse(f, hx - 22 - d, oy - 18, 4, 3, EGG)
            halo(f, hx - 22 - d, oy - 18, 7, GREEN_GLOW, 120 * p["s"])
        if a == "lay" and p["k"] > 0.55:
            t = min(1.0, (p["k"] - 0.55) / 0.35)
            ellipse(f, ox + 44 + t * 8, oy - 10 + t * 8, 4, 3.4, EGG)
            halo(f, ox + 44 + t * 8, oy - 10 + t * 8, 7, GREEN_GLOW, 110 * (1 - t * 0.6))


class Larva(Recipe):
    id = "larva"; cell = (128, 100); anchor = (84, 94); seed = 12
    anims = [("idle", 12, [], "idle"), ("attack", 9, [3], "action"), ("cast", 9, [], "action"), ("hurt", 6, [], "hurt"),
             ("death", 12, [], "death"), ("spawn", 8, [], "spawn")]
    moves = {"gnaw": "attack", "harden": "cast", "molt": "cast", "vengeance": "cast"}
    points = {"chest": (-4, -14), "eyes": (-24, -18), "body": (2, -12), "claw": (-36, -12)}

    def draw(self, f, p):
        ox, oy, ph = p["ox"], p["oy"], p["t"]
        a = p["anim"]
        lunge = (-p["w"] * 2 + p["s"] * 8) if a == "attack" else 0.0
        segs = 6
        for k in range(segs - 1, -1, -1):
            t = k / (segs - 1)
            x = ox + 20 - t * 38 - lunge * (1 - t * 0.3) * t
            y = oy - 10 - math.sin(math.pi * t) * 6 + math.sin(math.tau * ph + k) * 1.0
            r = 9.5 - abs(t - 0.4) * 4.5
            ellipse(f, x, y, r + 1.5, r, GRUB, tex=0.1, seed=k, mod=bands(5, 0.18, "x", k))
        hx = ox - 22 - lunge
        ellipse(f, hx, oy - 13, 8, 7.5, GRUB)
        polygon(f, [(hx - 5, oy - 12), (hx - 14, oy - 9), (hx - 9, oy - 6), (hx - 5, oy - 8)], CHITIN_BROWN)
        polygon(f, [(hx - 5, oy - 7), (hx - 13, oy - 2), (hx - 8, oy - 1), (hx - 4, oy - 4)], CHITIN_BROWN)
        f.put(hx - 3, oy - 17, (30, 20, 20)); f.put(hx - 2, oy - 17, (30, 20, 20))
        if a == "cast":
            halo(f, ox, oy - 10, 14, (230, 220, 150), 90 * p["s"])
            sparks(f, ox, oy - 10, 8, 14, (250, 240, 200), 200 * p["s"], 5, 0.6 + p["s"])


class Bonehog(Recipe):
    id = "bonehog"; cell = (144, 108); anchor = (96, 102); seed = 13
    anims = [("idle", 12, [], "idle"), ("attack", 10, [4], "action"), ("quills", 10, [3, 5, 7], "action"), ("cast", 9, [], "action"),
             ("hurt", 6, [], "hurt"), ("death", 13, [], "death")]
    moves = {"roll": "attack", "quills": "quills", "bristle": "cast", "vengeance": "cast"}
    points = {"chest": (-6, -20), "eyes": (-22, -24), "body": (2, -20), "claw": (-34, -16)}

    def draw(self, f, p):
        ox, oy, b = p["ox"], p["oy"], p["b"]
        a = p["anim"]
        dash = (-p["s"] * 14 + p["w"] * 3) if a == "attack" else 0.0
        grow = p["s"] * 3 if a == "cast" else 0.0
        cx = ox + 2 + dash
        cy = oy - 20 + b * 0.5
        for k in range(15):  # spikes behind the body (back and top)
            ang = math.radians(200 + k * 11)
            ln = 13 + (k % 3) * 3 + grow + (1.5 if k % 2 else 0)
            bx, by = cx + math.cos(ang) * 18, cy + math.sin(ang) * 13
            tx, ty = cx + math.cos(ang) * (18 + ln), cy + math.sin(ang) * (13 + ln * 0.85)
            if a == "quills" and p["s"] > 0.5 and k % 4 == 1:
                continue
            nx, ny = -math.sin(ang) * 2.2, math.cos(ang) * 2.2
            polygon(f, [(bx - nx, by - ny), (tx, ty), (bx + nx, by + ny)], BONE)
        for k, lx in enumerate((-14, -4, 8, 16)):
            capsule(f, cx + lx, cy + 10, cx + lx - 2, oy - 1, 2.6, 2.0, HOG)
        ellipse(f, cx, cy, 24, 15, HOG, tex=0.14, seed=5)
        ellipse(f, cx - 22, cy + 4, 9, 7, HOG, tex=0.1, seed=6)
        ellipse(f, cx - 30, cy + 6, 3, 2.5, BONE)
        f.put(cx - 33, cy + 6, (20, 14, 14))
        ellipse(f, cx - 20, cy, 3, 2.6, ramp((20, 8, 8), (40, 14, 14)))
        f.put(cx - 21, cy - 1, RED_EYE)
        halo(f, cx - 21, cy - 1, 3, (255, 80, 60), 60)
        if a == "quills":
            for st in (3, 5, 7):
                d = p["i"] - st
                if 0 <= d <= 2:
                    x = cx - 30 - d * 18
                    line(f, x, cy - 6 + st, x - 9, cy - 5 + st, (246, 238, 214), 230)
                    line(f, x + 1, cy - 5 + st, x - 8, cy - 4 + st, (180, 170, 150), 160)


class Toad(Recipe):
    id = "toad"; cell = (160, 108); anchor = (112, 102); seed = 14
    anims = [("idle", 12, [], "idle"), ("attack", 10, [4], "action"), ("tongue", 10, [4], "action"), ("swallow", 11, [], "action"),
             ("cast", 9, [], "action"), ("hurt", 6, [], "hurt"), ("death", 14, [], "death")]
    moves = {"belly": "attack", "tongue": "tongue", "swallow": "swallow", "vengeance": "cast"}
    points = {"chest": (-10, -22), "eyes": (-22, -40), "body": (2, -22), "claw": (-40, -24)}

    def draw(self, f, p):
        ox, oy, b = p["ox"], p["oy"], p["b"]
        a = p["anim"]
        hop = -p["w"] * 10 + p["s"] * 2 if a == "attack" else 0.0
        squash = p["s"] * 3 if a == "attack" else 0.0
        throat = (p["s"] * 4 if a in ("swallow", "cast") else 0.0)
        cx, cy = ox + 2, oy - 21 + hop + b * 0.5
        capsule(f, cx + 24, cy + 6, cx + 30, oy - 2, 6, 4, TOAD)   # back leg
        ellipse(f, cx + 2, cy, 32 + squash, 19 - squash + b * 0.4, TOAD, tex=0.12, seed=7,
                mod=lambda x, y, v: v + (0.25 if hash01(x // 3, y // 3, 9) > 0.86 else 0.0))
        ellipse(f, cx - 12, cy + 8 - throat * 0.3, 16 + throat, 9 + throat, BELLY)
        capsule(f, cx - 18, cy + 10, cx - 24, oy - 1, 3.6, 3.0, TOAD)
        capsule(f, cx - 6, cy + 12, cx - 8, oy - 1, 3.6, 3.0, TOAD)
        for ex in (-20, -6):  # eye bumps
            ellipse(f, cx + ex, cy - 17, 6, 5, TOAD)
            ellipse(f, cx + ex - 1, cy - 18, 3.4, 3, ramp((150, 120, 20), (230, 200, 60), (252, 236, 130)))
            f.put(cx + ex - 2, cy - 18, (10, 8, 6)); f.put(cx + ex - 2, cy - 17, (10, 8, 6))
        mouth_open = p["s"] if a == "tongue" else 0.0
        for x in range(int(cx - 30), int(cx + 4)):
            f.put(x, cy - 2 + int(abs(x - (cx - 14)) / 9) + (1 if mouth_open > 0.3 else 0), (40, 30, 20))
        if a == "tongue" and p["s"] > 0:
            ln = 52 * p["s"]
            capsule(f, cx - 30, cy, cx - 30 - ln, cy - 1, 2.4, 3.6, TONGUE)
        if a == "swallow":
            k = p["k"]
            if 0.2 < k < 0.6:
                halo(f, cx - 32 + (k - 0.2) * 40, cy - 2, 9, (255, 240, 200), 160)
            halo(f, cx - 12, cy + 8, 12, (230, 230, 160), 70 * throat / 4)


class Crow(Recipe):
    id = "crow"; cell = (144, 108); anchor = (96, 102); seed = 15
    anims = [("idle", 12, [], "idle"), ("attack", 11, [4, 6], "action"), ("gouge", 10, [5], "action"), ("cast", 9, [], "action"),
             ("hurt", 6, [], "hurt"), ("death", 13, [], "death")]
    moves = {"peck": "attack", "gouge": "gouge", "caw": "cast", "vengeance": "cast"}
    points = {"chest": (-2, -30), "eyes": (-16, -46), "body": (2, -30), "claw": (-34, -44)}

    def draw(self, f, p):
        ox, oy, b = p["ox"], p["oy"], p["b"]
        a = p["anim"]
        peck = p["s"] * 9 if a == "attack" else 0.0
        lunge = (-p["s"] * 16 + p["w"] * 4) if a == "gouge" else 0.0
        caw = p["s"] if a == "cast" else 0.0
        cx, cy = ox + 4 + lunge, oy - 30 + b * 0.8
        for k, lx in enumerate((-4, 4)):
            line(f, cx + lx, cy + 12, cx + lx - 1, oy - 1, (150, 134, 100))
            line(f, cx + lx - 1, oy - 1, cx + lx - 4, oy - 1, (150, 134, 100))
        polygon(f, [(cx + 10, cy + 2), (cx + 30, cy + 14), (cx + 27, cy + 17), (cx + 22, cy + 13), (cx + 20, cy + 18), (cx + 8, cy + 10)], CROW)
        ellipse(f, cx, cy, 16, 13, CROW, tex=0.1, seed=8)
        wing_up = caw * 14
        polygon(f, [(cx - 6, cy - 6 - wing_up * 0.4), (cx + 18, cy - 4 - wing_up), (cx + 24, cy + 4 - wing_up * 0.6), (cx + 16, cy + 6), (cx + 20, cy + 10),
                    (cx + 8, cy + 9), (cx - 4, cy + 6)], CROW, tex=0.15, seed=9, mod=bands(4, 0.25, "x"))
        hx, hy = cx - 13 - peck, cy - 14 + peck * 0.2
        ellipse(f, hx, hy, 8.5, 7.5, CROW)
        op = caw * 3
        polygon(f, [(hx - 6, hy - 2), (hx - 18, hy + 1 - op * 0.3), (hx - 6, hy + 2)], BEAK)
        if op > 0.5:
            polygon(f, [(hx - 6, hy + 3), (hx - 16, hy + 4 + op), (hx - 6, hy + 5)], BEAK)
        f.put(hx - 3, hy - 3, RED_EYE)
        halo(f, hx - 3, hy - 3, 3, (255, 60, 50), 70)
        if caw > 0:
            for r in (8, 14, 20):
                ring(f, hx - 20, hy + 2, r * (0.6 + caw * 0.5), (200, 160, 255), 120 * caw, arc=(math.pi * 0.7, math.pi * 1.3))


class Rat(Recipe):
    id = "rat"; cell = (128, 100); anchor = (84, 94); seed = 16
    anims = [("idle", 12, [], "idle"), ("attack", 9, [3], "action"), ("cast", 9, [], "action"), ("hurt", 6, [], "hurt"),
             ("death", 12, [], "death"), ("spawn", 8, [], "spawn")]
    moves = {"pack_bite": "attack", "squeak": "cast", "vengeance": "cast"}
    points = {"chest": (-4, -14), "eyes": (-18, -18), "body": (2, -12), "claw": (-30, -14)}

    def draw(self, f, p):
        ox, oy, b = p["ox"], p["oy"], p["b"]
        a = p["anim"]
        lunge = (-p["s"] * 10 + p["w"] * 3) if a == "attack" else 0.0
        rear = p["s"] * 6 if a == "cast" else 0.0
        cx, cy = ox + 4 + lunge, oy - 12 + b * 0.4
        curve(f, [(cx + 14, cy + 4), (cx + 26, cy + 6), (cx + 34, cy + 2 + math.sin(p["t"] * math.tau) * 2), (cx + 40, cy - 4)], [2, 1.6, 1.2, 0.8], PINK)
        for lx in (-10, 8):
            capsule(f, cx + lx, cy + 6, cx + lx - 2, oy - 1, 2.2, 1.6, PINK)
        ellipse(f, cx, cy - rear * 0.4, 17, 10, RATFUR, tex=0.18, seed=10, rot=-rear * 0.05)
        hx, hy = cx - 17, cy - 3 - rear
        polygon(f, [(hx + 4, hy - 6), (hx - 12, hy + 2), (hx + 4, hy + 6)], RATFUR)
        ellipse(f, hx + 1, hy, 6, 5.5, RATFUR, tex=0.14, seed=11)
        ellipse(f, hx + 4, hy - 6, 3.2, 3.6, PINK)
        spot(f, hx - 12, hy + 1, (230, 140, 150), 2)
        f.put(hx - 2, hy - 2, RED_EYE)
        if a == "attack" and p["s"] > 0.5:
            f.put(hx - 9, hy + 4, (250, 246, 230)); f.put(hx - 7, hy + 4, (250, 246, 230))
        if rear > 0:
            for r in (6, 11):
                ring(f, hx - 14, hy, r * (0.7 + p["s"] * 0.5), (255, 230, 200), 110 * p["s"], arc=(math.pi * 0.65, math.pi * 1.35))


class Beetle(Recipe):
    id = "beetle"; cell = (144, 108); anchor = (96, 102); seed = 17
    anims = [("idle", 12, [], "idle"), ("attack", 10, [4], "action"), ("ram", 12, [6], "action"), ("cast", 10, [], "action"),
             ("hurt", 6, [], "hurt"), ("death", 13, [], "death")]
    moves = {"horn": "attack", "ram": "ram", "curl": "cast", "vengeance": "cast"}
    points = {"chest": (-8, -20), "eyes": (-24, -18), "body": (2, -22), "claw": (-40, -24)}

    def draw(self, f, p):
        ox, oy, b = p["ox"], p["oy"], p["b"]
        a = p["anim"]
        thrust = (p["s"] * 7 - p["w"] * 2) if a == "attack" else 0.0
        ram = (-p["s"] * 18 + p["w"] * 4) if a == "ram" else 0.0
        curl = p["s"] if a == "cast" else 0.0
        cx, cy = ox + 2 + ram, oy - 21 + b * 0.4 + curl * 3
        for k, lx in enumerate((-14, -2, 10)):
            capsule(f, cx + lx, cy + 8, cx + lx - 6, oy - 1, 2.2, 1.6, LEGS)
            capsule(f, cx + lx + 4, cy + 8, cx + lx + 9, oy - 1, 2.0, 1.4, LEGS)
        hx = cx - 24 - thrust + curl * 6
        ellipse(f, hx, cy + 4, 9, 7, LEGS)
        polygon(f, [(hx - 2, cy - 2), (hx - 12, cy - 8), (hx - 18, cy - 15), (hx - 21, cy - 22), (hx - 16, cy - 16), (hx - 9, cy - 1), (hx - 2, cy + 5)], BRONZE)
        ellipse(f, hx - 4, cy + 1, 4, 3, BRONZE)
        f.put(hx - 4, cy + 2, (255, 210, 120))
        ellipse(f, cx, cy, 27, 17 + b * 0.3, BEETLE, tex=0.06, seed=12,
                mod=lambda x, y, v: v - (0.55 if abs(x - (cx + 2)) < 0.7 else 0.0) + (0.12 if hash01(x, y // 2, 4) > 0.93 else 0))
        ellipse(f, cx - 6, cy - 9, 7, 3, ramp((150, 220, 200), (230, 255, 240)))
        if a == "ram" and p["s"] > 0.4:
            for k in range(4):
                line(f, cx + 30 + k * 6, cy - 6 + k * 4, cx + 40 + k * 6, cy - 6 + k * 4, (200, 230, 220), 120 * p["s"])
        if curl > 0:
            halo(f, cx, cy, 22, (120, 220, 200), 70 * curl)


class Ooze(Recipe):
    id = "ooze"; cell = (160, 108); anchor = (112, 102); seed = 18
    terminal = ["split"]
    anims = [("idle", 12, [], "idle"), ("attack", 10, [4], "action"), ("cast", 9, [], "action"), ("split", 14, [7], "terminal"),
             ("hurt", 6, [], "hurt"), ("death", 14, [], "death")]
    moves = {"engulf": "attack", "sticky": "attack", "gel": "cast", "split": "split", "vengeance": "cast"}
    points = {"chest": (-4, -24), "eyes": (-12, -32), "body": (0, -22), "claw": (-40, -18)}

    def blob(self, f, cx, cy, rx, ry, bones=True, scale=1.0):
        ground = cy + ry * 0.6
        ellipse(f, cx, cy, rx, ry, GEL, tex=0.1, seed=13, clip=lambda x, y: y <= ground + 1)
        if bones:
            capsule(f, cx - 8 * scale, cy + 4 * scale, cx + 6 * scale, cy + 8 * scale, 1.6 * scale, 1.6 * scale, GELBONE)
            ellipse(f, cx + 8 * scale, cy - 2 * scale, 5 * scale, 4.4 * scale, GELBONE)
            f.put(cx + 6 * scale, cy - 2 * scale, (90, 30, 90)); f.put(cx + 9 * scale, cy - 2 * scale, (90, 30, 90))
        ellipse(f, cx - rx * 0.45, cy - ry * 0.45, 2.2 * scale, 2.6 * scale, ramp((30, 6, 30), (60, 14, 60)))
        ellipse(f, cx - rx * 0.15, cy - ry * 0.5, 2.2 * scale, 2.6 * scale, ramp((30, 6, 30), (60, 14, 60)))
        halo(f, cx - rx * 0.35, cy - ry * 0.6, 4 * scale, (255, 230, 255), 90)

    def draw(self, f, p):
        ox, oy, b = p["ox"], p["oy"], p["b"]
        a = p["anim"]
        if a == "split":
            k = p["k"]
            if k < 0.5:
                st = k / 0.5
                self.blob(f, ox, oy - 20, 30 + st * 14, 22 - st * 6)
                if st > 0.6:
                    for y in range(int(oy - 32), int(oy - 6)):
                        if hash01(0, y, 3) > 0.4:
                            f.body[min(f.w * f.h - 1, max(0, y * f.w + int(ox)))] = None
            else:
                t = (k - 0.5) / 0.5
                for side in (-1, 1):
                    self.blob(f, ox + side * (14 + t * 16), oy - 12 - (1 - t) * 4, 15 - t * 3, 12 - t * 3, bones=side > 0, scale=0.6)
                    sparks(f, ox, oy - 14, 14, 26 * t + 6, (240, 140, 230), 200 * (1 - t), 21 + side, 1)
            return
        lunge = (-p["s"] * 10 + p["w"] * 2) if a == "attack" else 0.0
        jig = p["s"] * 2 if a == "cast" else 0.0
        self.blob(f, ox + lunge * 0.4, oy - 22 - b * 0.6, 30 + lunge * -0.25 + b * 0.8 + jig, 23 + b * 0.8 - jig)
        if a == "attack" and p["s"] > 0:
            capsule(f, ox - 22, oy - 18, ox - 22 + lunge * 2.4, oy - 20, 6, 4, GEL)
        if a == "cast":
            halo(f, ox, oy - 24, 26, (230, 120, 220), 60 * p["s"])


class OozeSmall(Ooze):
    id = "ooze_small"; cell = (128, 100); anchor = (84, 94); seed = 19
    terminal = []
    anims = [("idle", 12, [], "idle"), ("attack", 9, [3], "action"), ("cast", 9, [], "action"), ("hurt", 6, [], "hurt"),
             ("death", 12, [], "death"), ("spawn", 8, [], "spawn")]
    moves = {"nibble": "attack", "glob": "attack", "vengeance": "cast"}
    points = {"chest": (-2, -12), "eyes": (-8, -16), "body": (0, -11), "claw": (-24, -10)}

    def draw(self, f, p):
        ox, oy, b = p["ox"], p["oy"], p["b"]
        a = p["anim"]
        lunge = (-p["s"] * 8 + p["w"] * 2) if a == "attack" else 0.0
        jig = p["s"] * 2 if a == "cast" else 0.0
        cx, cy = ox + lunge * 0.5, oy - 11 - b * 0.5
        ground = oy - 1
        ellipse(f, cx, cy, 16 + b * 0.5 + jig, 12 + b * 0.5 - jig, GEL, tex=0.1, seed=14, clip=lambda x, y: y <= ground)
        capsule(f, cx - 2, cy + 3, cx + 7, cy + 5, 1.2, 1.2, GELBONE)
        ellipse(f, cx - 7, cy - 5, 1.6, 2, ramp((30, 6, 30), (60, 14, 60)))
        ellipse(f, cx - 2, cy - 6, 1.6, 2, ramp((30, 6, 30), (60, 14, 60)))
        halo(f, cx - 6, cy - 7, 3, (255, 230, 255), 90)
        if a == "attack" and p["s"] > 0.3:
            ellipse(f, cx - 18 + lunge, cy, 3, 3, GEL)


class Viper(Recipe):
    id = "viper"; cell = (128, 100); anchor = (84, 94); seed = 20
    anims = [("idle", 12, [], "idle"), ("attack", 9, [3], "action"), ("cast", 9, [], "action"), ("hurt", 6, [], "hurt"),
             ("death", 12, [], "death"), ("spawn", 8, [], "spawn")]
    moves = {"venom_fang": "attack", "lurk": "cast", "vengeance": "cast"}
    points = {"chest": (-6, -20), "eyes": (-16, -34), "body": (2, -14), "claw": (-30, -30)}

    def draw(self, f, p):
        ox, oy, b, ph = p["ox"], p["oy"], p["b"], p["t"]
        a = p["anim"]
        strike = (p["s"] * 14 - p["w"] * 4) if a == "attack" else 0.0
        coil = p["s"] if a == "cast" else 0.0
        stripe = lambda x, y, v: v - (0.0 if (x + y) % 7 > 1 else 0.3)
        ellipse(f, ox + 4, oy - 5, 18 - coil * 2, 5, VIPER, tex=0.08, seed=15, mod=stripe)
        ellipse(f, ox + 2, oy - 10, 14 - coil * 2, 4.5, VIPER, tex=0.08, seed=16, mod=stripe)
        sway = math.sin(math.tau * ph) * 1.5
        hx, hy = ox - 14 - strike + sway, oy - 32 + strike * 0.5 + coil * 4
        curve(f, [(ox + 4, oy - 12), (ox + 6, oy - 20), (ox - 2, oy - 28 + coil * 3), (hx + 6, hy + 2)], [4.5, 4, 3.6, 3.2], VIPER, mod=stripe)
        ellipse(f, hx, hy, 6.5, 4.5, VIPER)
        ellipse(f, hx + 1, hy + 2.5, 4.5, 1.6, YELLOWBAND)
        f.put(hx - 2, hy - 2, (255, 220, 60)); f.put(hx - 2, hy - 1, (20, 10, 6))
        if a == "attack" and p["s"] > 0.4:
            f.put(hx - 6, hy + 2, (250, 250, 240)); f.put(hx - 6, hy + 3, (250, 250, 240))
            halo(f, hx - 8, hy + 3, 4, (160, 255, 90), 140 * p["s"])
        else:
            line(f, hx - 7, hy + 1, hx - 11, hy + 1, (220, 40, 60), 230)
        if coil > 0:
            halo(f, ox + 2, oy - 10, 16, (150, 255, 100), 60 * coil)


class RatKing(Recipe):
    id = "ratking"; cell = (208, 120); anchor = (142, 115); boss = True; elite = True; seed = 21
    anims = [("idle", 12, [], "idle"), ("attack", 10, [4], "action"), ("tail", 12, [4, 6, 8], "action"), ("call", 11, [6], "action"),
             ("cast", 10, [], "action"), ("hurt", 6, [], "hurt"), ("death", 15, [], "death")]
    moves = {"royal_bite": "attack", "tail_whip": "tail", "call_swarm": "call", "plague_fury": "call", "court_feast": "cast", "rally": "cast",
             "vengeance": "cast"}
    points = {"chest": (-8, -38), "eyes": (-30, -52), "body": (0, -34), "claw": (-56, -44)}

    def draw(self, f, p):
        ox, oy, b, ph = p["ox"], p["oy"], p["b"], p["t"]
        a = p["anim"]
        lunge = (-p["s"] * 12 + p["w"] * 3) if a == "attack" else 0.0
        raise_ = p["s"] if a in ("call", "cast") else 0.0
        whip = p["s"] if a == "tail" else 0.0
        for k in range(5):  # knotted tails
            ang = k * 1.3 + ph * math.tau * 0.2
            pts = [(ox + 10, oy - 6), (ox + 26 + k * 3, oy - 4 - k), (ox + 34 + math.cos(ang) * 6, oy - 14 - k * 3), (ox + 40 + k * 2 + whip * 8, oy - 24 - k * 2 - whip * 10)]
            curve(f, pts, [2.2, 1.8, 1.4, 1.0], PINK)
        for (rx, ry, s) in ((-34, -8, 0.7), (26, -8, 0.75), (-16, -5, 0.65), (10, -4, 0.7)):  # the court of rats
            ellipse(f, ox + rx, oy + ry, 11 * s, 7 * s, RATFUR, tex=0.18, seed=17 + rx)
            ellipse(f, ox + rx - 9 * s, oy + ry - 1, 4.5 * s, 4 * s, RATFUR)
            f.put(ox + rx - 11 * s, oy + ry - 2, RED_EYE)
        cx, cy = ox + 2 + lunge * 0.3, oy - 36 + b * 0.8
        ellipse(f, cx, cy, 24, 22, RATFUR, tex=0.2, seed=18)
        polygon(f, [(cx - 22, cy - 12), (cx + 20, cy - 16), (cx + 28, cy + 20), (cx + 6, cy + 14), (cx - 4, cy + 22), (cx - 24, cy + 10)], CLOAK, tex=0.12, seed=19,
                mod=bands(5, 0.2, "y"))
        hx, hy = cx - 24 + lunge, cy - 14 + lunge * 0.2
        polygon(f, [(hx + 8, hy - 8), (hx - 14, hy + 3), (hx + 8, hy + 9)], RATFUR)
        ellipse(f, hx + 4, hy, 9, 8, RATFUR, tex=0.14, seed=20)
        ellipse(f, hx + 9, hy - 9, 3.6, 4.4, PINK)
        spot(f, hx - 14, hy + 2, (230, 140, 150), 2)
        f.put(hx, hy - 3, RED_EYE); f.put(hx + 1, hy - 3, RED_EYE)
        halo(f, hx + 1, hy - 3, 3, (255, 60, 40), 70)
        if a == "attack" and p["s"] > 0.5:
            f.put(hx - 10, hy + 6, (255, 220, 90)); f.put(hx - 8, hy + 6, (255, 220, 90))
        polygon(f, [(hx - 2, hy - 8), (hx, hy - 16), (hx + 3, hy - 11), (hx + 6, hy - 18), (hx + 9, hy - 11), (hx + 12, hy - 16), (hx + 13, hy - 7)], GOLD)
        f.put(hx + 6, hy - 12, (230, 40, 60))
        sx, sy = cx - 30, cy + 6 - raise_ * 16
        capsule(f, sx + 4, sy + 22, sx, sy - 6, 1.6, 1.6, BONE)
        ellipse(f, sx, sy - 9, 4, 3.6, BONE)
        f.put(sx - 2, sy - 9, (40, 30, 30)); f.put(sx + 1, sy - 9, (40, 30, 30))
        if raise_ > 0:
            halo(f, sx, sy - 9, 10, (255, 220, 120), 120 * raise_)
            if a == "call":
                for r in (8, 15, 22):
                    ring(f, sx, sy - 9, r * (0.5 + raise_ * 0.6), (255, 220, 160), 100 * raise_)
        if whip > 0.3:
            for k in range(3):
                ring(f, ox + 34, oy - 30, 18 + k * 6, (255, 190, 200), 120 * whip, arc=(math.pi * 1.1, math.pi * 1.7))


class Colossus(Recipe):
    id = "colossus"; cell = (208, 120); anchor = (142, 115); boss = True; elite = True; seed = 22
    anims = [("idle", 12, [], "idle"), ("attack", 10, [4], "action"), ("hammer", 14, [8], "action"), ("stoke", 12, [], "action"),
             ("stun", 10, [], "action"), ("cast", 10, [], "action"), ("hurt", 6, [], "hurt"), ("death", 15, [], "death")]
    moves = {"iron_step": "attack", "pile_driver": "hammer", "stoke": "stoke", "overheat": "stoke", "stunned": "stun", "vengeance": "cast"}
    points = {"chest": (-4, -50), "eyes": (-14, -78), "body": (0, -46), "claw": (-60, -30)}

    def draw(self, f, p):
        ox, oy, b = p["ox"], p["oy"], p["b"]
        a = p["anim"]
        step = (-p["s"] * 8 + p["w"] * 2) if a == "attack" else 0.0
        heat = 0.25 + 0.05 * b + (p["s"] * 0.75 if a == "stoke" else 0.0)
        wob = math.sin(p.get("k", 0) * math.pi * 4) * 3 * p["s"] if a == "stun" else 0.0
        cx, cy = ox + 2 + step * 0.4 + wob, oy - 48 + b * 0.6
        capsule(f, cx + 10, cy + 18, cx + 12, oy - 4, 7, 6, IRON, tex=0.08, seed=21)
        capsule(f, cx - 10 + step, cy + 18, cx - 12 + step, oy - 4, 7, 6, IRON, tex=0.08, seed=22)
        ellipse(f, cx + 12, oy - 3, 9, 3.5, IRON); ellipse(f, cx - 13 + step, oy - 3, 9, 3.5, IRON)
        capsule(f, cx + 22, cy - 12, cx + 26, cy + 10, 6, 5.5, IRON, tex=0.08, seed=23)   # back arm
        ellipse(f, cx, cy, 25, 24, IRON, tex=0.1, seed=24, mod=lambda x, y, v: v - (0.3 if (y - int(cy)) % 9 == 0 else 0))
        ellipse(f, cx - 4, cy + 2, 11, 9, GRATE)
        for gx in range(-9, 10, 4):
            line(f, cx - 4 + gx, cy - 6, cx - 4 + gx, cy + 10, (30, 22, 20))
        core = mix((255, 120, 30), (255, 250, 230), min(1.0, heat))
        ellipse(f, cx - 4, cy + 2, 7, 6, ramp((140, 40, 10), (220, 90, 20), (255, 160, 50), (255, 220, 120)))
        halo(f, cx - 4, cy + 2, 10 + heat * 8, core, 140 + 115 * heat, core=(255, 255, 230) if heat > 0.7 else None)
        hx, hy = cx - 8 + wob, cy - 30
        ellipse(f, hx, hy, 9, 8, IRON, tex=0.08, seed=25)
        line(f, hx - 6, hy - 1, hx + 1, hy - 1, (255, 170, 70) if a != "stun" else (90, 70, 60))
        halo(f, hx - 3, hy - 1, 5, (255, 140, 40), 90 if a != "stun" else 10)
        # direction of the hammer head from the fist (screen angles, y down): resting low in front, raised overhead, slammed
        if a == "hammer":
            theta = 115 + 150 * p["w"] if p["i"] < 8 else 115 + 30 * p["s"]
        elif a == "attack":
            theta = 115 + 20 * p["s"]
        else:
            theta = 115
        th = math.radians(theta)
        sxp, syp = cx - 20, cy - 4
        hxp, hyp = sxp + math.cos(th) * 38, syp + math.sin(th) * 38
        capsule(f, cx - 10, cy - 10, sxp, syp, 6.5, 6, IRON, tex=0.08, seed=26)
        capsule(f, sxp, syp, hxp, hyp, 2.4, 2.4, WOOD)
        nx, ny = -math.sin(th) * 12, math.cos(th) * 12
        dx_, dy_ = math.cos(th) * 10, math.sin(th) * 10
        polygon(f, [(hxp - nx, hyp - ny), (hxp + nx, hyp + ny), (hxp + nx + dx_, hyp + ny + dy_), (hxp - nx + dx_, hyp - ny + dy_)], IRON, tex=0.1, seed=27)
        if a == "hammer" and p["i"] in (8, 9):
            sparks(f, hxp, oy - 4, 22, 22, (255, 200, 90), 230, 31 + p["i"], 1)
        if a == "stoke":
            for k in range(3):
                halo(f, cx + 8 + k * 4, cy - 30 - p["s"] * 10 - k * 4, 4 + k, (230, 230, 240), 70 * p["s"])
        if a == "stun":
            for k in range(3):
                t = p["k"] * math.tau + k * 2.1
                f.glow(hx + math.cos(t) * 10, hy - 12 + math.sin(t) * 3, (255, 240, 120), 220 * p["s"] + 30)
        if a == "cast":
            for k in range(4):
                halo(f, cx - 20 + k * 14, cy - 18 - p["s"] * 8, 5, (220, 230, 240), 80 * p["s"])


class Duelist(Recipe):
    id = "duelist"; cell = (208, 120); anchor = (142, 115); boss = True; elite = True; seed = 23
    anims = [("idle", 12, [], "idle"), ("attack", 9, [3], "action"), ("flurry", 10, [3, 6], "action"), ("fleche", 12, [6], "action"),
             ("cast", 10, [], "action"), ("hurt", 6, [], "hurt"), ("death", 15, [], "death")]
    moves = {"lunge": "attack", "feint": "flurry", "fleche": "fleche", "en_garde": "cast", "second_wind": "cast", "vengeance": "cast"}
    points = {"chest": (-2, -56), "eyes": (-6, -76), "body": (0, -50), "claw": (-60, -54)}

    def draw(self, f, p):
        ox, oy, b = p["ox"], p["oy"], p["b"]
        a = p["anim"]
        thrust = p["s"] if a in ("attack", "flurry", "fleche") else 0.0
        dash = (-p["s"] * 24 + p["w"] * 4) if a == "fleche" else (-p["s"] * 6 if a == "attack" else 0.0)
        guard = p["s"] if a == "cast" else 0.0
        cx, cy = ox + dash, oy - 50 + b * 1.2
        capsule(f, cx + 2, cy + 18, cx + 10, oy - 14, 3.5, 2.5, SPECTRAL)
        capsule(f, cx - 4, cy + 18, cx - 12 - thrust * 6, oy - 14, 3.5, 2.5, SPECTRAL)
        polygon(f, [(cx - 12, cy - 14), (cx + 12, cy - 14), (cx + 16, cy + 24), (cx + 6, cy + 18), (cx - 2, cy + 26), (cx - 16, cy + 20)], SPECTRAL, tex=0.1, seed=28)
        line(f, cx - 1, cy - 12, cx - 1, cy + 18, (220, 255, 246))
        hx, hy = cx - 2, cy - 22
        ellipse(f, hx, hy, 7, 7.5, SPECTRAL)
        f.put(hx - 3, hy - 1, (255, 255, 255)); f.put(hx - 1, hy - 1, (255, 255, 255))
        halo(f, hx - 2, hy - 1, 4, TEAL_GLOW, 80)
        polygon(f, [(hx - 16, hy - 6), (hx - 4, hy - 10), (hx + 2, hy - 18), (hx + 8, hy - 10), (hx + 16, hy - 6), (hx, hy - 4)], HAT)
        capsule(f, cx + 10, cy - 10, cx + 20, cy + 2, 2.5, 2, SPECTRAL)
        if guard > 0:
            sx, sy = cx - 14, cy - 8
            tx, ty = sx - 4, sy - 30 * (0.6 + 0.4 * guard)
        else:
            reach = 10 + thrust * 18
            sx, sy = cx - 10 - reach * 0.5, cy - 6
            tx, ty = sx - 30 - thrust * 10, sy + 2
        capsule(f, cx - 8, cy - 10, sx, sy, 2.5, 2, SPECTRAL)
        ellipse(f, sx, sy, 3, 3, GOLD)
        line(f, sx, sy, tx, ty, (230, 255, 250))
        line(f, sx + (1 if guard > 0 else 0), sy + (0 if guard > 0 else 1), tx + (1 if guard > 0 else 0), ty + (0 if guard > 0 else 1), (120, 200, 200))
        if thrust > 0.6:
            halo(f, tx, ty, 6, (200, 255, 250), 200 * thrust)
        if guard > 0:
            halo(f, tx, ty, 6 + guard * 4, (220, 255, 250), 200 * guard)


class Inquisitor(Recipe):
    id = "inquisitor"; cell = (224, 124); anchor = (150, 116); boss = True; seed = 24
    anims = [("idle", 16, [], "idle"), ("attack", 10, [3, 5, 7], "action"), ("judgment", 12, [7], "action"), ("pyre", 14, [8], "action"),
             ("confiscate", 12, [], "action"), ("cast", 10, [], "action"), ("hurt", 6, [], "hurt"), ("death", 15, [], "death")]
    moves = {"sentence": "attack", "verdict": "attack", "judgment": "judgment", "pyre": "pyre", "confiscate": "confiscate", "edict": "cast",
             "penance": "cast", "expiation": "cast", "auto_de_fe": "cast", "vengeance": "cast"}
    points = {"chest": (-6, -56), "eyes": (-10, -82), "body": (0, -48), "claw": (-50, -50)}

    def draw(self, f, p):
        ox, oy, b, ph = p["ox"], p["oy"], p["b"], p["t"]
        a = p["anim"]
        cx, cy = ox + 2, oy - 46 + b * 0.8
        polygon(f, [(cx - 14, cy - 22), (cx + 14, cy - 22), (cx + 26, oy - 1), (cx - 28, oy - 1)], ROBE, tex=0.1, seed=29, mod=bands(7, 0.25, "x", 2))
        polygon(f, [(cx - 6, cy - 20), (cx + 6, cy - 20), (cx + 8, oy - 1), (cx - 8, oy - 1)], ROBE_DARK)
        for y in range(int(cy - 16), int(oy - 2), 5):
            f.put(cx - 1, y, (200, 160, 60))
        polygon(f, [(cx - 16, cy - 24), (cx + 16, cy - 24), (cx + 10, cy - 14), (cx - 10, cy - 14)], ROBE_DARK)
        hx, hy = cx - 2, cy - 32
        ellipse(f, hx, hy, 8, 9, ROBE_DARK)
        f.put(hx - 4, hy, (255, 200, 80)); f.put(hx - 1, hy, (255, 200, 80))
        halo(f, hx - 2, hy, 5, (255, 160, 40), 100)
        polygon(f, [(hx - 8, hy - 6), (hx - 4, hy - 26), (hx + 1, hy - 30), (hx + 5, hy - 26), (hx + 9, hy - 6)], ramp((120, 100, 80), (200, 180, 140), (240, 228, 196)))
        ellipse(f, hx, hy - 16, 3, 2, ramp((90, 20, 20), (200, 40, 40)))
        f.put(hx - 1, hy - 16, (20, 10, 10))
        # censer arm (front): swings in sentence / judgment
        if a == "attack":
            ang = math.radians(150 + 60 * p["s"])
        elif a == "judgment":
            ang = math.radians(150 + 80 * p["s"] - 30 * p["w"])
        else:
            ang = math.radians(150 + math.sin(math.tau * ph) * 6)
        shx, shy = cx - 12, cy - 18
        elx, ely = shx - 8, shy + 10
        capsule(f, shx, shy, elx, ely, 4, 3.5, ROBE)
        chx, chy = elx + math.cos(ang) * 26, ely + math.sin(ang) * 26
        for k in range(1, 9):
            t = k / 9
            f.put(elx + (chx - elx) * t, ely + (chy - ely) * t, (170, 160, 150))
        ellipse(f, chx, chy, 5, 6, GOLD)
        halo(f, chx, chy, 6, FIRE, 120)
        for k in range(4):
            yy = (ph * 30 + k * 7) % 28
            halo(f, chx + math.sin(k + ph * math.tau) * 3, chy - 6 - yy, 3 + yy * 0.12, (120, 110, 110), 70 * (1 - yy / 28))
        # tome (back hand)
        tx, ty = cx + 18, cy - 4 - (p["s"] * 8 if a in ("cast", "pyre") else 0)
        capsule(f, cx + 12, cy - 18, tx, ty, 4, 3.5, ROBE)
        polygon(f, [(tx - 6, ty - 4), (tx + 6, ty - 6), (tx + 7, ty + 3), (tx - 5, ty + 5)], ramp((60, 30, 20), (110, 60, 30), (150, 90, 40)))
        fl = 0.6 + 0.4 * (p["s"] if a in ("cast", "pyre") else 0.2)
        for k in range(5):
            halo(f, tx - 4 + k * 2, ty - 7 - (k % 2) * 3, 3.5 * fl, FIRE, 150 * fl, core=(255, 240, 160))
        if a == "confiscate":
            s = p["s"]
            capsule(f, shx, shy, shx - 20 - 14 * s, shy + 2, 3.5, 3, ROBE)
            halo(f, shx - 22 - 14 * s, shy + 2, 8 + s * 6, (190, 120, 255), 200 * s)
        if a == "pyre" and p["i"] >= 7:
            t = min(1.0, (p["i"] - 7) / 3)
            for k in range(14):
                x = cx - 70 + math.sin(k * 1.7) * 6
                y = oy - 6 - k * 7 * t
                halo(f, x, y, 7 - k * 0.3, FIRE, 220 * (1 - k / 16), core=(255, 240, 180))


class Serpent(Recipe):
    id = "serpent"; cell = (224, 124); anchor = (150, 116); boss = True; seed = 25
    anims = [("idle", 16, [], "idle"), ("attack", 10, [4], "action"), ("tail", 12, [4, 6, 8], "action"), ("constrict", 12, [6], "action"),
             ("molt", 14, [], "action"), ("cast", 10, [], "action"), ("hurt", 6, [], "hurt"), ("death", 15, [], "death")]
    moves = {"fangs": "attack", "death_bite": "attack", "tail": "tail", "constrict": "constrict", "molt": "molt", "new_skin": "molt",
             "brood": "cast", "brood_frenzy": "cast", "hiss": "cast", "vengeance": "cast"}
    points = {"chest": (-14, -52), "eyes": (-34, -74), "body": (0, -30), "claw": (-58, -68)}

    def draw(self, f, p):
        ox, oy, b, ph = p["ox"], p["oy"], p["b"], p["t"]
        a = p["anim"]
        strike = (p["s"] * 18 - p["w"] * 6) if a == "attack" else 0.0
        squeeze = p["s"] if a == "constrict" else 0.0
        hood = p["s"] if a in ("cast", "constrict") else 0.0
        scales = lambda x, y, v: v - (0.22 if (x + 2 * y) % 6 == 0 else 0) + (0.1 if (x - y) % 6 == 0 else 0)
        whip = p["s"] if a == "tail" else 0.0
        curve(f, [(ox + 30, oy - 6), (ox + 44, oy - 10), (ox + 50 + whip * 6, oy - 22 - whip * 12), (ox + 44 + whip * 10, oy - 36 - whip * 14)], [6, 5, 3.5, 2], SERPENT, mod=scales)
        ellipse(f, ox + 2, oy - 8, 40 - squeeze * 3, 9, SERPENT, mod=scales, tex=0.06, seed=30)
        ellipse(f, ox - 2, oy - 18, 32 - squeeze * 3, 8, SERPENT, mod=scales, tex=0.06, seed=31)
        ellipse(f, ox + 2, oy - 27 + b * 0.3, 24 - squeeze * 2, 7, SERPENT, mod=scales, tex=0.06, seed=32)
        sway = math.sin(math.tau * ph) * 2
        hx, hy = ox - 30 - strike + sway, oy - 70 + strike * 0.4 + b * 0.8
        pts = [(ox + 2, oy - 30), (ox + 8, oy - 44), (ox - 4, oy - 58 + squeeze * 4), (hx + 10, hy + 6)]
        curve(f, pts, [9, 8, 7, 6], SERPENT, mod=scales)
        curve(f, [(ox - 2, oy - 32), (ox + 3, oy - 44), (ox - 8, oy - 57 + squeeze * 4), (hx + 6, hy + 8)], [4, 3.6, 3.2, 2.8], AMBER)
        hw = 13 + hood * 6
        polygon(f, [(hx + 6, hy - 12), (hx + 6 + hw, hy - 2), (hx + 10 + hw * 0.6, hy + 14), (hx + 8, hy + 10)], SERPENT, mod=scales)
        ellipse(f, hx, hy, 10, 7, SERPENT, tex=0.06, seed=33)
        ellipse(f, hx - 1, hy + 4, 7, 2, AMBER)
        f.put(hx - 4, hy - 3, (255, 210, 60)); f.put(hx - 3, hy - 3, (255, 210, 60)); f.put(hx - 4, hy - 2, (20, 10, 6))
        halo(f, hx - 4, hy - 3, 4, (255, 210, 60), 90)
        if a == "attack" and p["s"] > 0.4:
            f.put(hx - 9, hy + 3, (250, 250, 240)); f.put(hx - 9, hy + 4, (250, 250, 240))
            halo(f, hx - 12, hy + 4, 6, (160, 255, 90), 160 * p["s"])
        else:
            line(f, hx - 10, hy + 2, hx - 15, hy + 1, (220, 40, 60), 230)
        if a == "molt":
            k = p["k"]
            for j in range(12):
                t = (k * 1.4 - j * 0.06)
                if 0 < t < 1:
                    x = ox - 30 + hash01(j, 1, 4) * 70
                    y = oy - 10 - hash01(j, 2, 4) * 60 - t * 20
                    halo(f, x, y, 3, (250, 246, 230), 230 * (1 - t))
                    f.glow(x + 1, y + 1, (200, 196, 176), 200 * (1 - t))
        if whip > 0.3:
            ring(f, ox + 48, oy - 36, 20, (200, 255, 200), 120 * whip, arc=(math.pi * 1.2, math.pi * 1.8))


class Gravedigger(Recipe):
    id = "gravedigger"; cell = (192, 124); anchor = (124, 116); boss = True; seed = 26
    anims = [("idle", 14, [], "idle"), ("attack", 10, [4], "action"), ("requiem", 10, [3, 6], "action"), ("dig", 12, [], "action"),
             ("cast", 10, [], "action"), ("stun", 10, [], "action"), ("hurt", 6, [], "hurt"), ("death", 15, [], "death")]
    moves = {"shovel": "attack", "shovel_heavy": "attack", "slab": "attack", "requiem": "requiem", "dig_in": "dig", "dig_grave": "dig",
             "exhume": "dig", "grief": "cast", "stunned": "stun", "vengeance": "cast"}
    points = {"chest": (-6, -46), "eyes": (-16, -68), "body": (0, -40), "claw": (-50, -40)}

    def draw(self, f, p):
        ox, oy, b, ph = p["ox"], p["oy"], p["b"], p["t"]
        a = p["anim"]
        wob = math.sin(p.get("k", 0) * math.pi * 4) * 3 * p["s"] if a == "stun" else 0.0
        cx, cy = ox + 4 + wob, oy - 42 + b * 0.8
        capsule(f, cx + 8, cy + 16, cx + 10, oy - 3, 5, 4.5, COAT)
        capsule(f, cx - 6, cy + 16, cx - 10, oy - 3, 5, 4.5, COAT)
        ellipse(f, cx + 10, oy - 2, 6, 2.5, LEGS); ellipse(f, cx - 12, oy - 2, 6, 2.5, LEGS)
        polygon(f, [(cx - 20, cy - 16), (cx + 6, cy - 28), (cx + 22, cy - 14), (cx + 24, cy + 22), (cx - 22, cy + 22)], COAT, tex=0.14, seed=34)
        polygon(f, [(cx - 22, cy + 14), (cx + 24, cy + 14), (cx + 26, cy + 26), (cx - 24, cy + 26)], COAT, tex=0.14, seed=35, mod=bands(5, 0.3, "x"))
        hx, hy = cx - 14, cy - 24
        ellipse(f, hx, hy, 8, 8, SKIN, tex=0.1, seed=36)
        f.put(hx - 4, hy - 1, (255, 220, 120)); f.put(hx - 1, hy - 1, (255, 220, 120))
        ellipse(f, hx, hy - 7, 16, 3.5, HAT)
        polygon(f, [(hx - 8, hy - 8), (hx - 6, hy - 18), (hx + 7, hy - 18), (hx + 8, hy - 8)], HAT)
        # lantern on a hook at the hip
        lx, ly = cx + 18, cy + 4 + math.sin(math.tau * ph) * 1.5
        line(f, lx, ly - 8, lx, ly - 4, (90, 80, 70))
        polygon(f, [(lx - 4, ly - 4), (lx + 4, ly - 4), (lx + 3, ly + 5), (lx - 3, ly + 5)], ramp((40, 30, 20), (90, 70, 40)))
        lamp = 0.6 + 0.3 * math.sin(math.tau * ph * 2) * (1 if a != "cast" else 0) + (p["s"] * 0.4 if a == "cast" else 0)
        halo(f, lx, ly, 8 + lamp * 4, (255, 200, 90), 160 * lamp, core=(255, 250, 200))
        # shovel direction (screen angles, y down): held upright, swung down to the left, or plunged into the ground
        if a in ("attack", "requiem"):
            ang = math.radians(-115 + 25 * p["w"] - 85 * p["s"])
        elif a == "dig":
            ang = math.radians(-115 - 145 * p["s"])
        else:
            ang = math.radians(-115)
        gx, gy = cx - 10, cy - 6
        if a == "dig":
            gy += p["s"] * 12
        capsule(f, cx - 4, cy - 14, gx, gy, 4.5, 4, COAT)
        ex, ey = gx + math.cos(ang) * 46, gy + math.sin(ang) * 46
        sx, sy = gx - math.cos(ang) * 12, gy - math.sin(ang) * 12
        capsule(f, sx, sy, ex, ey, 1.6, 1.6, WOOD)
        dx_, dy_ = math.cos(ang), math.sin(ang)
        bx, by = ex, ey
        polygon(f, [(bx - dy_ * 5, by + dx_ * 5), (bx + dy_ * 5, by - dx_ * 5), (bx + dx_ * 11 + dy_ * 3, by + dy_ * 11 - dx_ * 3),
                    (bx + dx_ * 13, by + dy_ * 13), (bx + dx_ * 11 - dy_ * 3, by + dy_ * 11 + dx_ * 3)], STEEL)
        if a == "dig" and p["s"] > 0.4:
            for k in range(10):
                t = p["s"]
                x = bx - 6 + hash01(k, 1, 7) * 16
                y = oy - 4 - hash01(k, 2, 7) * 18 * t
                f.glow(x, y, (120, 92, 66), 230)
                f.glow(x + 1, y, (80, 60, 44), 200)
        if a == "stun":
            for k in range(3):
                t = p["k"] * math.tau + k * 2.1
                f.glow(hx + math.cos(t) * 12, hy - 22 + math.sin(t) * 3, (255, 240, 120), 220 * p["s"] + 30)


class Mourner(Recipe):
    id = "mourner"; cell = (192, 124); anchor = (124, 116); boss = True; seed = 27
    anims = [("idle", 14, [], "idle"), ("attack", 10, [3, 6], "action"), ("wail", 11, [4, 7], "action"), ("last", 13, [7], "action"),
             ("cast", 10, [], "action"), ("hurt", 6, [], "hurt"), ("death", 15, [], "death"), ("spawn", 12, [], "spawn")]
    moves = {"requiem": "attack", "banshee": "attack", "wail": "wail", "last_wail": "last", "lament": "cast", "curse": "cast", "soul_drain": "cast",
             "vengeance": "cast"}
    points = {"chest": (-4, -54), "eyes": (-8, -74), "body": (0, -50), "claw": (-40, -56)}

    def draw(self, f, p):
        ox, oy, b, ph = p["ox"], p["oy"], p["b"], p["t"]
        a = p["anim"]
        float_y = -12 + b * 3
        cx, cy = ox + 2, oy - 50 + float_y * 0.6 + 6
        claw = p["s"] if a == "attack" else 0.0
        scream = p["s"] if a in ("wail", "last") else 0.0
        hem = []
        for k in range(9):
            x = cx - 24 + k * 6
            hem.append((x, oy - 12 + float_y + (4 if k % 2 else 0) + math.sin(math.tau * ph + k) * 1.5))
        polygon(f, [(cx - 10, cy - 24), (cx + 10, cy - 24), (cx + 26, hem[-1][1])] + hem[::-1] + [(cx - 26, hem[0][1])], VEIL, tex=0.12, seed=37,
                mod=bands(6, 0.22, "x", ph * 6))
        hx, hy = cx - 2, cy - 30
        ellipse(f, hx, hy, 9, 10, VEIL)
        ellipse(f, hx - 2, hy + 1, 5.5, 7, PALE)
        open_ = 1 + int(scream * 3)
        f.put(hx - 4, hy - 1, (20, 16, 30)); f.put(hx - 1, hy - 1, (20, 16, 30))
        for y in range(open_):
            f.put(hx - 2, hy + 4 + y, (30, 20, 40))
        tear = (ph * 2) % 1.0
        halo(f, hx - 4, hy + 1 + tear * 6, 2.5, BLUE_TEAR, 220, core=(240, 250, 255))
        halo(f, hx - 1, hy + 2 + ((tear + 0.5) % 1) * 6, 2.5, BLUE_TEAR, 200, core=(240, 250, 255))
        up = scream * 14 + (p["s"] * 10 if a == "cast" else 0)
        capsule(f, cx - 8, cy - 18, cx - 18 - claw * 14, cy - 8 - up, 2.6, 2, VEIL)
        capsule(f, cx + 8, cy - 18, cx + 16, cy - 4 - up, 2.6, 2, VEIL)
        for k in range(3):
            line(f, cx - 18 - claw * 14, cy - 8 - up, cx - 23 - claw * 16 - k, cy - 12 - up + k * 3, (230, 226, 245))
        if scream > 0:
            for r in (10, 18, 26):
                ring(f, hx - 10, hy + 4, r * (0.5 + scream * 0.6), (170, 210, 255), 130 * scream, arc=(math.pi * 0.6, math.pi * 1.4))
        if a == "cast":
            halo(f, cx, cy - 10, 20, (170, 130, 255), 80 * p["s"])
        if claw > 0.6:
            for k in range(3):
                line(f, cx - 40 + k * 3, cy - 18 + k * 4, cx - 30 + k * 3, cy - 4 + k * 4, (200, 220, 255), 200 * claw)

    def spawn_pose(self, r):
        return self.idle_pose(0.0)


RECIPES = [Broodmother, Larva, Bonehog, Toad, Crow, Rat, Beetle, Ooze, OozeSmall, Viper, RatKing, Colossus, Duelist, Inquisitor, Serpent, Gravedigger, Mourner]


# ------------------------------------------------------------------ sheet assembly and validation
def lum(img):
    px = img.getdata()
    total, n = 0.0, 0
    for r, g, b, a in px:
        if a == 255:
            total += 0.299 * r + 0.587 * g + 0.114 * b
            n += 1
    return total / max(1, n), n


def build(recipe):
    rows = recipe.render()
    cw, ch = recipe.cell
    cols = max(n for (_, n, _, _, _) in rows)
    sheet = Image.new("RGBA", (cols * cw, len(rows) * ch), (0, 0, 0, 0))
    animations, events = {}, {}
    for r, (name, n, strikes, kind, frames) in enumerate(rows):
        for i, fr in enumerate(frames):
            sheet.paste(fr, (i * cw, r * ch))
        animations[name] = {"row": r, "frames": n, "durations_ms": durations_for(kind if kind in ("idle", "hurt", "death", "spawn") else "action", n, strikes),
                            "loop": kind == "idle"}
        if strikes:
            events[name] = {"strikes": list(strikes)}
    idle0 = rows[0][4][0]
    bbox = idle0.getchannel("A").point(lambda a: 255 if a == 255 else 0).getbbox()
    meta = {"cell_w": cw, "cell_h": ch, "columns": cols, "anchor": list(recipe.anchor), "scale": 2, "sheet": recipe.id + "_sheet.png",
            "animations": animations, "events": events, "moves": dict(recipe.moves)}
    if recipe.terminal:
        meta["terminal"] = list(recipe.terminal)
    if recipe.boss:
        meta["boss"] = True
    if recipe.elite:
        meta["elite"] = True
    meta["top_native"] = bbox[1]
    meta["idle_bbox_native"] = list(bbox)
    return sheet, meta, rows


def validate(recipe, sheet, meta, rows):
    problems = []
    if sheet.width != meta["columns"] * meta["cell_w"] or sheet.width > 8192:
        problems.append("width")
    for need in ("idle", "attack", "hurt", "cast", "death"):
        if need not in meta["animations"]:
            problems.append("missing animation " + need)
    for name, a in meta["animations"].items():
        if len(a["durations_ms"]) != a["frames"]:
            problems.append("durations " + name)
    for name, e in meta["events"].items():
        if any(s >= meta["animations"][name]["frames"] for s in e["strikes"]):
            problems.append("strike out of range " + name)
    for mv, an in meta["moves"].items():
        if an not in meta["animations"]:
            problems.append("move " + mv + " -> " + an)
    idle0 = rows[0][4][0]
    base, opaque = lum(idle0)
    semi = sum(1 for (_, _, _, a) in idle0.getdata() if 0 < a < 255)
    if opaque == 0 or semi > opaque * 0.03:
        problems.append("idle semi-transparent %d / %d" % (semi, opaque))
    hurt = next(r for r in rows if r[3] == "hurt")[4]
    for i, target in enumerate(HURT_FLASH):
        lh, _ = lum(hurt[i])
        got = (lh - base) / max(1e-6, 255 - base)
        if abs(got - target) > 0.10:
            problems.append("hurt flash %d = %.2f" % (i, got))
    w, h = idle0.size
    alpha = idle0.getchannel("A").load()
    rgb = idle0.load()
    edge = dark = 0
    for y in range(h):
        for x in range(w):
            if alpha[x, y] != 255:
                continue
            if any(not (0 <= nx < w and 0 <= ny < h) or alpha[nx, ny] != 255 for nx, ny in ((x - 1, y), (x + 1, y), (x, y - 1), (x, y + 1))):
                edge += 1
                if sum(rgb[x, y][:3]) < 60:
                    dark += 1
    if edge == 0 or dark < edge * 0.8:
        problems.append("outline %d / %d" % (dark, edge))
    idle_bytes = idle0.tobytes()
    for (name, n, strikes, kind, frames) in rows:
        if kind in ("death", "terminal", "idle"):
            continue
        if frames[-1].tobytes() != idle_bytes:
            problems.append(name + " does not end on idle frame 0")
    return problems


def png_bytes(img):
    buf = io.BytesIO()
    img.save(buf, format="PNG", optimize=False)
    return buf.getvalue()


def json_text(meta):
    return json.dumps(meta, indent=1, ensure_ascii=False) + "\n"


def guid(rel):
    return hashlib.md5(("LastWish/" + rel).encode("utf-8")).hexdigest()


def write_meta(path, template):
    if os.path.exists(path + ".meta"):
        return
    with open(os.path.join(OUT_DIR, template), "r", encoding="utf-8") as fh:
        text = fh.read()
    rel = os.path.relpath(path, ROOT).replace("\\", "/")
    lines = text.splitlines()
    lines = ["guid: " + guid(rel) if ln.startswith("guid: ") else ln for ln in lines]
    with open(path + ".meta", "w", encoding="utf-8", newline="\n") as fh:
        fh.write("\n".join(lines) + "\n")


def contact(recipe, rows, path):
    cw, ch = recipe.cell
    cols = max(n for (_, n, _, _, _) in rows)
    pad = 14
    img = Image.new("RGBA", (cols * cw * 2, len(rows) * (ch * 2 + pad)), (36, 36, 46, 255))
    d = ImageDraw.Draw(img)
    for r, (name, n, strikes, kind, frames) in enumerate(rows):
        y = r * (ch * 2 + pad)
        d.text((4, y + 1), "%s %s (%d) strikes %s" % (recipe.id, name, n, strikes), fill=(220, 220, 230, 255))
        for i, fr in enumerate(frames):
            big = fr.resize((cw * 2, ch * 2), Image.NEAREST)
            img.alpha_composite(big, (i * cw * 2, y + pad))
    img.save(path)


def styles_line(recipe, meta):
    ax, ay = recipe.anchor
    x0, y0, x1, y1 = meta["idle_bbox_native"]
    shadow = int(max(90, min(260, round(1.6 * (x1 - x0) / 10.0) * 10)))
    pts = {k: (v[0] * 2, v[1] * 2) for k, v in recipe.points.items()}
    return '["%s"] = Chest = (%d, %d), Eyes = (%d, %d), Body = (%d, %d), Claw = (%d, %d), ShadowW = %d' % (
        recipe.id, pts["chest"][0], pts["chest"][1], pts["eyes"][0], pts["eyes"][1], pts["body"][0], pts["body"][1], pts["claw"][0], pts["claw"][1], shadow)


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--only", default="")
    ap.add_argument("--preview", default="")
    ap.add_argument("--styles", action="store_true")
    ap.add_argument("--check", action="store_true")
    args = ap.parse_args(argv)
    known = {r.id: r for r in RECIPES}
    chosen = [r for r in RECIPES]
    if args.only:
        ids = [s.strip() for s in args.only.split(",") if s.strip()]
        for i in ids:
            if i in EXISTING:
                sys.stderr.write("%s es una hoja original: no se regenera\n" % i)
                return 2
            if i not in known:
                sys.stderr.write("id desconocido: %s\n" % i)
                return 2
        chosen = [known[i] for i in ids]
    failed = False
    for cls in chosen:
        recipe = cls()
        sheet, meta, rows = build(recipe)
        problems = validate(recipe, sheet, meta, rows)
        if problems:
            failed = True
            sys.stderr.write("%s: %s\n" % (recipe.id, "; ".join(problems)))
        png_path = os.path.join(OUT_DIR, recipe.id + "_sheet.png")
        json_path = os.path.join(OUT_DIR, recipe.id + "_sheet.json")
        data, text = png_bytes(sheet), json_text(meta)
        if args.check:
            for path, content in ((png_path, data), (json_path, text.encode("utf-8"))):
                if not os.path.exists(path) or open(path, "rb").read() != content:
                    failed = True
                    sys.stderr.write("differs: %s\n" % os.path.relpath(path, ROOT))
        elif not args.styles:
            with open(png_path, "wb") as fh:
                fh.write(data)
            with open(json_path, "w", encoding="utf-8", newline="\n") as fh:
                fh.write(text)
            write_meta(png_path, "bat_sheet.png.meta")
            write_meta(json_path, "bat_sheet.json.meta")
        if args.styles:
            print(styles_line(recipe, meta))
        if args.preview:
            os.makedirs(args.preview, exist_ok=True)
            contact(recipe, rows, os.path.join(args.preview, recipe.id + ".png"))
        if not args.styles:
            print("%s: %dx%d, %d rows%s" % (recipe.id, sheet.width, sheet.height, len(rows), " (PROBLEMS)" if problems else ""))
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
