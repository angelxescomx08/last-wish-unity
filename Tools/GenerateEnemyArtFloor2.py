#!/usr/bin/env python3
"""Pixel-art sheets of the floor-2 expansion enemies (deterministic, Pillow only).

Same technique, kit and JSON format as Tools/GenerateEnemyArt.py (it is imported, never changed): shaded primitives with
light from the top-left, 4x4 Bayer dithering, a dark 1 px outline on the body layer, a semi-transparent effects layer,
idle loops, actions that end on idle frame 0, hurt flashing 72/53/23 % towards white and a bottom-up dissolve on death.

Usage:
  python Tools/GenerateEnemyArtFloor2.py                 writes the 15 sheets (+ .meta if missing) in Assets/Resources/Game/Enemies
  python Tools/GenerateEnemyArtFloor2.py --only shade,leech
  python Tools/GenerateEnemyArtFloor2.py --preview DIR   also writes a labelled x2 contact sheet per enemy in DIR
  python Tools/GenerateEnemyArtFloor2.py --styles        prints the EnemyAnimator.Styles points (chest, eyes, body, claw, shadow)
  python Tools/GenerateEnemyArtFloor2.py --check         regenerates in memory and compares with the files (exit 1 on difference)
Exit codes: 0 ok, 1 differences or validation failure, 2 bad usage.
"""
import argparse
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import GenerateEnemyArt as kit  # noqa: E402  (the floor-1 generator: primitives, finishing, assembly, validation)
from GenerateEnemyArt import (Recipe, ellipse, capsule, curve, polygon, line, spot, halo, ring, sparks, ramp, mix, hash01,  # noqa: E402
                              bands, BONE, WOOD, GOLD, STEEL, IRON, FIRE)

ROOT, OUT_DIR = kit.ROOT, kit.OUT_DIR
FLOOR1 = {r.id for r in kit.RECIPES}

# ------------------------------------------------------------------ palettes
STONE = ramp((24, 28, 36), (46, 52, 66), (72, 80, 96), (102, 110, 128), (138, 146, 164), (178, 186, 200))
RUNE = (120, 230, 255)
RUNE_INK = ramp((40, 120, 150), (90, 200, 230), (170, 245, 255))
ZROBE = ramp((14, 8, 22), (32, 18, 50), (56, 30, 84), (86, 50, 118), (118, 76, 152))
SASH = ramp((70, 10, 20), (130, 24, 34), (186, 46, 50), (220, 90, 80))
PALESKIN = ramp((90, 80, 92), (148, 136, 140), (198, 184, 178), (230, 218, 208))
SHADE = ramp((4, 2, 8), (14, 7, 22), (28, 14, 40), (46, 24, 62), (70, 40, 90))
MAGENTA = (255, 80, 170)
LEECH = ramp((28, 8, 14), (60, 16, 26), (98, 28, 38), (142, 50, 56), (188, 88, 86), (228, 146, 136))
LEECH_BAND = ramp((90, 60, 30), (150, 110, 50), (200, 160, 80))
TEETH = (240, 232, 214)
TROLL = ramp((24, 30, 22), (44, 56, 38), (70, 86, 56), (100, 116, 76), (134, 146, 98), (172, 180, 128))
LOIN = ramp((48, 28, 18), (86, 56, 32), (126, 88, 50), (160, 120, 72))
HARL_A = ramp((56, 10, 48), (104, 22, 86), (164, 48, 136), (214, 96, 184), (246, 166, 226))
HARL_B = ramp((10, 38, 48), (18, 76, 86), (36, 124, 134), (84, 184, 184), (164, 236, 226))
MASK = ramp((132, 132, 152), (194, 194, 210), (232, 232, 242), (252, 252, 255))
LAVENDER = ramp((54, 46, 84), (100, 88, 140), (150, 136, 192), (200, 190, 232), (238, 234, 255))
RUST = ramp((58, 28, 18), (106, 54, 30), (154, 88, 48), (196, 128, 78))
GLASS = ramp((36, 56, 76), (74, 104, 134), (122, 162, 194), (172, 210, 232), (220, 242, 252), (250, 255, 255))
DARKSTEEL = ramp((8, 8, 12), (22, 22, 30), (38, 38, 50), (60, 60, 76), (88, 88, 106), (120, 120, 138))
TARNISH = ramp((66, 46, 18), (114, 86, 38), (164, 126, 58), (206, 166, 88))
TABARD = ramp((70, 66, 62), (118, 112, 104), (166, 158, 144), (204, 196, 180))
GOWN = ramp((36, 4, 12), (74, 10, 22), (122, 18, 34), (168, 34, 50), (206, 66, 76))
CAPE_OUT = ramp((6, 4, 8), (16, 10, 18), (30, 20, 32), (48, 34, 50))
HAIR = ramp((8, 6, 10), (20, 16, 24), (38, 30, 44))
VAMPSKIN = ramp((116, 106, 128), (172, 162, 182), (218, 210, 224), (242, 238, 248))
INDIGO = ramp((8, 6, 20), (16, 12, 38), (30, 22, 62), (48, 36, 92), (74, 58, 124))
LICH = ramp((8, 12, 10), (16, 28, 20), (28, 44, 32), (42, 66, 46), (62, 92, 66))
SKULL = ramp((86, 78, 64), (148, 138, 116), (204, 194, 168), (236, 228, 204), (250, 246, 230))
HYDRA = ramp((8, 28, 28), (14, 52, 50), (24, 84, 76), (42, 120, 102), (78, 160, 128), (128, 198, 158))
HYDRA_BELLY = ramp((104, 96, 48), (164, 150, 78), (210, 196, 118), (238, 228, 168))
WATER = ramp((6, 18, 28), (10, 36, 52), (18, 60, 80), (34, 90, 112), (70, 134, 156))
DARK_INK = (14, 10, 16)
GREEN_FIRE = (120, 255, 150)
VIOLET = (170, 110, 255)
CRIMSON = (255, 60, 80)
YELLOW_EYE = (255, 220, 80)
WHITE = (250, 250, 255)


def tri(f, a, b, c, rmp, flat=None):
    polygon(f, [a, b, c], rmp, flat=flat)


# ------------------------------------------------------------------ normals
class Sentinel(Recipe):
    id = "sentinel"; cell = (160, 116); anchor = (108, 110); seed = 41
    anims = [("idle", 12, [], "idle"), ("attack", 10, [4], "action"), ("burst", 11, [4, 7], "action"), ("cast", 10, [], "action"),
             ("hurt", 6, [], "hurt"), ("death", 14, [], "death")]
    moves = {"rune_maul": "attack", "rune_burst": "burst", "ward_rune": "cast", "vengeance": "cast"}
    points = {"chest": (-2, -44), "eyes": (-4, -70), "body": (0, -40), "claw": (-52, -12)}

    def draw(self, f, p):
        ox, oy, b = p["ox"], p["oy"], p["b"]
        a = p["anim"]
        glow = 0.35 + 0.1 * b + (0.65 * p["s"] if a in ("cast", "burst") else 0.0)
        cx, cy = ox, oy - 40
        # pillar legs
        polygon(f, [(cx - 15, cy + 12), (cx - 4, cy + 12), (cx - 3, oy - 1), (cx - 17, oy - 1)], STONE, tex=0.12, seed=1)
        polygon(f, [(cx + 4, cy + 12), (cx + 15, cy + 12), (cx + 17, oy - 1), (cx + 3, oy - 1)], STONE, tex=0.12, seed=2)
        # torso: a carved block wider at the shoulders
        polygon(f, [(cx - 22, cy - 20), (cx + 22, cy - 20), (cx + 16, cy + 14), (cx - 16, cy + 14)], STONE, tex=0.14, seed=3,
                mod=lambda x, y, v: v - (0.22 if (y - int(cy)) % 11 == 0 else 0.0))
        polygon(f, [(cx - 26, cy - 24), (cx - 14, cy - 26), (cx - 12, cy - 16), (cx - 24, cy - 14)], STONE, tex=0.1, seed=4)   # pauldrons
        polygon(f, [(cx + 14, cy - 26), (cx + 26, cy - 24), (cx + 24, cy - 14), (cx + 12, cy - 16)], STONE, tex=0.1, seed=5)
        # rune channels on the chest
        for (x0, y0, x1, y1) in ((-8, -12, 8, -12), (0, -16, 0, 6), (-6, 0, 6, 0), (-8, -12, -6, 0), (8, -12, 6, 0)):
            line(f, cx + x0, cy + y0, cx + x1, cy + y1, mix((40, 120, 150), RUNE, min(1.0, glow)))
        halo(f, cx, cy - 4, 10 + glow * 6, RUNE, 70 + 90 * glow)
        # floating head with one rune eye
        hy = cy - 33 + b * 1.2
        polygon(f, [(cx - 9, hy - 7), (cx + 9, hy - 7), (cx + 8, hy + 6), (cx - 8, hy + 6)], STONE, tex=0.1, seed=6)
        line(f, cx - 6, hy, cx + 1, hy, RUNE)
        halo(f, cx - 3, hy, 5, RUNE, 120)
        # floating fists (front one strikes)
        fx_, fy_ = cx - 32, cy + 2 + b * 1.5
        if a == "attack":
            fx_ = cx - 32 - 10 * p["s"]
            fy_ = cy + 2 - 26 * p["w"] + 34 * p["s"]
        ellipse(f, fx_, fy_, 8, 7, STONE, tex=0.1, seed=7)
        line(f, fx_ - 4, fy_ - 2, fx_ + 3, fy_ - 2, mix((40, 120, 150), RUNE, glow))
        ellipse(f, cx + 31, cy + b * 1.5, 7, 6.5, STONE, tex=0.1, seed=8)
        if a == "attack" and p["i"] in (4, 5):
            sparks(f, fx_ - 4, oy - 4, 18, 22, (200, 240, 255), 220, 9 + p["i"], 1)
            ring(f, fx_ - 4, oy - 4, 10 + 6 * (p["i"] - 4), RUNE, 160)
        if a == "burst":
            for st in (4, 7):
                d = p["i"] - st
                if 0 <= d <= 2:
                    x = cx - 18 - d * 22
                    line(f, x, cy - 4, x - 18, cy - 5, (220, 250, 255), 240)
                    halo(f, x - 18, cy - 5, 6, RUNE, 200)
        if a == "cast":
            ring(f, cx, cy - 10, 30 + 6 * p["s"], RUNE, 170 * p["s"])
            ring(f, cx, cy - 10, 22 + 4 * p["s"], (200, 250, 255), 110 * p["s"], width=0.8)


class Zealot(Recipe):
    id = "zealot"; cell = (144, 108); anchor = (96, 102); seed = 42
    anims = [("idle", 12, [], "idle"), ("attack", 9, [3], "action"), ("slash", 11, [3, 6], "action"), ("cast", 10, [], "action"),
             ("hurt", 6, [], "hurt"), ("death", 13, [], "death"), ("spawn", 8, [], "spawn")]
    moves = {"ritual_dagger": "attack", "blood_slash": "slash", "dark_rite": "cast", "vengeance": "cast"}
    points = {"chest": (-4, -30), "eyes": (-12, -46), "body": (0, -28), "claw": (-36, -30)}

    def draw(self, f, p):
        ox, oy, b, ph = p["ox"], p["oy"], p["b"], p["t"]
        a = p["anim"]
        lunge = (-p["s"] * 10 + p["w"] * 3) if a == "attack" else 0.0
        cx, cy = ox + lunge * 0.5, oy - 30 + b * 0.6
        hem = []
        for k in range(7):
            x = cx - 15 + k * 5
            hem.append((x, oy - 1 - (2 if k % 2 else 0) + math.sin(math.tau * ph + k) * 0.6))
        polygon(f, [(cx - 8, cy - 16), (cx + 9, cy - 16), (cx + 16, hem[-1][1])] + hem[::-1] + [(cx - 16, hem[0][1])], ZROBE, tex=0.1, seed=11,
                mod=bands(6, 0.2, "x", 1))
        polygon(f, [(cx - 9, cy - 2), (cx + 10, cy - 4), (cx + 10, cy + 1), (cx - 9, cy + 3)], SASH)
        polygon(f, [(cx + 2, cy + 1), (cx + 6, cy + 1), (cx + 8, cy + 14), (cx + 4, cy + 14)], SASH)
        # hood leaning forward
        hx, hy = cx - 5 + lunge * 0.3, cy - 22
        ellipse(f, hx + 2, hy, 10, 9, ZROBE, tex=0.08, seed=12)
        polygon(f, [(hx + 6, hy - 8), (hx + 14, hy - 2), (hx + 9, hy + 2)], ZROBE)
        ellipse(f, hx - 2, hy + 2, 5.5, 6, ramp((6, 2, 8), (14, 6, 18)))
        for ex in (-4, -1):
            f.put(hx + ex, hy + 1, (255, 60, 60))
        halo(f, hx - 2, hy + 1, 4, (255, 60, 60), 70)
        # arms and the curved dagger
        rite = p["s"] if a == "cast" else 0.0
        if a == "slash":
            ang = math.radians(200 - 140 * p["s"]) if p["i"] < 6 else math.radians(60 + 140 * p["s"])
        elif a == "attack":
            ang = math.radians(185 - 10 * p["s"])
        else:
            ang = math.radians(250 + 40 * rite) if rite > 0 else math.radians(205 + math.sin(math.tau * ph) * 4)
        shx, shy = cx - 6, cy - 12
        hx2, hy2 = shx + math.cos(ang) * 12 + lunge * 0.4, shy + math.sin(ang) * 12
        capsule(f, shx, shy, hx2, hy2, 3, 2.6, ZROBE)
        ellipse(f, hx2, hy2, 2.4, 2.4, PALESKIN)
        tx, ty = hx2 + math.cos(ang) * 11, hy2 + math.sin(ang) * 11
        line(f, hx2, hy2, tx, ty, (220, 224, 236))
        line(f, hx2 + 1, hy2 + 1, tx + 1, ty, (140, 146, 160))
        if a in ("attack", "slash") and p["s"] > 0.5:
            ring(f, hx2, hy2, 13, (255, 120, 120), 160 * p["s"], arc=(ang - 0.7, ang + 0.7))
        capsule(f, cx + 7, cy - 12, cx + 12, cy - 2, 2.8, 2.4, ZROBE)
        if rite > 0:
            for k in range(5):
                t = ph * math.tau + k * 1.25
                halo(f, cx + math.cos(t) * 16, cy - 10 + math.sin(t) * 6 - rite * 4, 4, (170, 80, 255), 170 * rite, core=(240, 200, 255))
            halo(f, tx, ty, 6, (200, 90, 255), 200 * rite)

    def spawn_pose(self, r):
        return self.idle_pose(0.0)


class Shade(Recipe):
    id = "shade"; cell = (144, 108); anchor = (96, 102); seed = 43
    anims = [("idle", 12, [], "idle"), ("attack", 10, [4], "action"), ("rend", 11, [3, 6], "action"), ("cast", 10, [], "action"),
             ("hurt", 6, [], "hurt"), ("death", 14, [], "death")]
    moves = {"shadow_claw": "attack", "rend": "rend", "grudge_grow": "cast", "vengeance": "cast"}
    points = {"chest": (0, -36), "eyes": (-8, -52), "body": (0, -36), "claw": (-40, -36)}

    def draw(self, f, p):
        ox, oy, b, ph = p["ox"], p["oy"], p["b"], p["t"]
        a = p["anim"]
        fl = -6 + b * 2.5
        swipe = p["s"] if a in ("attack", "rend") else 0.0
        lean = (-p["s"] * 8 + p["w"] * 3) if a == "attack" else 0.0
        cx, cy = ox + lean * 0.5, oy - 40 + fl
        # tendrils under the body
        for k in range(5):
            x0 = cx - 10 + k * 5
            sw = math.sin(math.tau * ph + k * 1.3) * 3
            curve(f, [(x0, cy + 10), (x0 + sw, cy + 18), (x0 - sw * 0.6 + 1, cy + 25), (x0 + sw + 2, cy + 31)], [3.2, 2.4, 1.6, 0.8], SHADE)
        # inky teardrop body
        polygon(f, [(cx - 4, cy - 26), (cx + 8, cy - 22), (cx + 15, cy - 6), (cx + 13, cy + 8), (cx + 4, cy + 14), (cx - 7, cy + 13),
                    (cx - 15, cy + 4), (cx - 15, cy - 10), (cx - 10, cy - 22)], SHADE, tex=0.14, seed=13, mod=bands(5, 0.18, "y", ph * 5))
        # face
        fx_, fy_ = cx - 6, cy - 14
        for (ex, ey) in ((-3, 0), (2, -1)):
            line(f, fx_ + ex - 1, fy_ + ey + 1, fx_ + ex + 1, fy_ + ey, MAGENTA)
        halo(f, fx_, fy_, 6, MAGENTA, 90)
        for k in range(6):   # jagged grin
            f.put(fx_ - 4 + k, fy_ + 6 + (k % 2), (230, 120, 200))
        # grudge ember
        grow = 0.4 + 0.1 * b + (0.6 * p["s"] if a == "cast" else 0.0)
        halo(f, cx + 2, cy, 6 + grow * 6, (255, 60, 140), 90 + 120 * grow, core=(255, 200, 230))
        # long arms with claws
        for side, base in ((-1, (cx - 12, cy - 8)), (1, (cx + 12, cy - 6))):
            if side < 0:
                reach = 14 + swipe * 18
                hx2, hy2 = base[0] - reach, base[1] + 6 - swipe * 10
                if a == "rend" and p["i"] >= 6:
                    hy2 = base[1] - 6 + swipe * 14
            else:
                hx2, hy2 = base[0] + 8, base[1] + 12 + math.sin(math.tau * ph) * 2
            curve(f, [base, ((base[0] + hx2) / 2, (base[1] + hy2) / 2 - 3), (hx2, hy2)], [2.6, 2.0, 1.4], SHADE)
            for k in range(3):
                line(f, hx2, hy2, hx2 + side * 2 - 4 + k * 3 * (1 if side > 0 else -1) * 0.3 - 3, hy2 + 4 + k, (190, 150, 220))
        if swipe > 0.6:
            for k in range(3):
                line(f, cx - 44 + k * 4, cy - 18 + k * 5, cx - 30 + k * 4, cy - 2 + k * 5, (255, 120, 200), 210 * swipe)
        if a == "cast":
            for r in (14, 22, 30):
                ring(f, cx, cy, r * (0.6 + p["s"] * 0.5), (220, 60, 160), 120 * p["s"])


class Leech(Recipe):
    id = "leech"; cell = (160, 108); anchor = (112, 102); seed = 44
    anims = [("idle", 12, [], "idle"), ("attack", 10, [4], "action"), ("drain", 12, [5], "action"), ("cast", 9, [], "action"),
             ("hurt", 6, [], "hurt"), ("death", 14, [], "death")]
    moves = {"suction_cup": "attack", "drain": "drain", "clot": "cast", "vengeance": "cast"}
    points = {"chest": (-10, -20), "eyes": (-36, -34), "body": (4, -16), "claw": (-50, -30)}

    def draw(self, f, p):
        ox, oy, b, ph = p["ox"], p["oy"], p["b"], p["t"]
        a = p["anim"]
        reach = (p["s"] * 14 - p["w"] * 4) if a in ("attack", "drain") else 0.0
        squeeze = p["s"] if a == "cast" else 0.0
        pulse = p["k"] if a == "drain" else -1.0
        segs = 9
        pts = []
        for k in range(segs):
            t = k / (segs - 1)   # 0 = tail (right), 1 = head (left)
            x = ox + 30 - t * 58 - reach * t * t
            y = oy - 9 - (t ** 2.2) * 22 + math.sin(math.tau * ph + t * 4) * 1.2 + squeeze * 3 * t
            pts.append((x, y))
        for k in range(segs):
            t = k / (segs - 1)
            r = 9 + math.sin(math.pi * t) * 4 - squeeze * 1.5
            if pulse >= 0 and abs(pulse - (1 - t)) < 0.15:
                r += 2.5
            x, y = pts[k]
            ellipse(f, x, y, r + 1, r * 0.9, LEECH, tex=0.12, seed=14 + k, mod=bands(4, 0.25, "x", k * 2))
            if k % 2 == 0:
                capsule(f, x - 1, y - r * 0.6, x + 1, y + r * 0.5, 1, 1, LEECH_BAND)
        hx, hy = pts[-1]
        ellipse(f, hx - 5, hy, 6, 8, LEECH, tex=0.08, seed=30)
        ellipse(f, hx - 8, hy, 3, 5.5, ramp((20, 4, 8), (50, 10, 18)))
        for k in range(8):
            ang = k / 8 * math.tau
            f.put(hx - 8 + math.cos(ang) * 3.5, hy + math.sin(ang) * 5.5, TEETH)
        for k in range(4):
            halo(f, pts[k * 2][0] - 3, pts[k * 2][1] - 7, 2.2, (255, 220, 220), 120)   # glossy spots
        if a == "drain" and p["s"] > 0.3:
            halo(f, hx - 14, hy, 10, CRIMSON, 200 * p["s"])
            for k in range(4):
                t = (p["k"] * 2 + k * 0.25) % 1.0
                x, y = pts[int((1 - t) * (segs - 1))]
                halo(f, x, y - 2, 3, (255, 80, 90), 180)
        if a == "attack" and p["s"] > 0.6:
            sparks(f, hx - 12, hy, 10, 12, (255, 120, 120), 200, 31, 1)
        if a == "cast":
            halo(f, ox, oy - 14, 26, (160, 40, 60), 80 * squeeze)


class Troll(Recipe):
    id = "troll"; cell = (160, 116); anchor = (108, 110); seed = 45
    anims = [("idle", 12, [], "idle"), ("attack", 11, [5], "action"), ("claws", 11, [3, 6], "action"), ("cast", 10, [], "action"),
             ("hurt", 6, [], "hurt"), ("death", 15, [], "death")]
    moves = {"club": "attack", "double_claw": "claws", "lick_wounds": "cast", "vengeance": "cast"}
    points = {"chest": (-6, -40), "eyes": (-26, -54), "body": (0, -38), "claw": (-56, -20)}

    def draw(self, f, p):
        ox, oy, b, ph = p["ox"], p["oy"], p["b"], p["t"]
        a = p["anim"]
        cx, cy = ox + 2, oy - 40 + b * 0.8
        lick = p["s"] if a == "cast" else 0.0
        capsule(f, cx + 10, cy + 16, cx + 12, oy - 4, 7, 6, TROLL, tex=0.1, seed=1)
        capsule(f, cx - 8, cy + 16, cx - 12, oy - 4, 7, 6, TROLL, tex=0.1, seed=2)
        ellipse(f, cx + 13, oy - 3, 8, 3, TROLL); ellipse(f, cx - 13, oy - 3, 8, 3, TROLL)
        capsule(f, cx + 18, cy - 10, cx + 24, cy + 18, 6, 5, TROLL, tex=0.1, seed=3)   # back arm
        ellipse(f, cx + 2, cy, 24, 22, TROLL, tex=0.16, seed=4, rot=-0.2,
                mod=lambda x, y, v: v + (0.22 if hash01(x // 2, y // 2, 5) > 0.9 else 0.0))
        ellipse(f, cx - 6, cy + 8, 13, 10, ramp((92, 106, 70), (130, 140, 96), (164, 170, 124)))
        polygon(f, [(cx - 16, cy + 12), (cx + 14, cy + 12), (cx + 10, cy + 24), (cx - 2, cy + 20), (cx - 14, cy + 24)], LOIN, tex=0.1, seed=5)
        # head thrust forward and low
        hx, hy = cx - 22 - lick * 3, cy - 14 + lick * 6
        ellipse(f, hx, hy, 11, 9, TROLL, tex=0.12, seed=6)
        ellipse(f, hx - 9, hy + 2, 5, 4, TROLL)                       # nose
        polygon(f, [(hx - 6, hy + 6), (hx - 8, hy + 2), (hx - 4, hy + 4)], ramp((200, 196, 170), (240, 236, 214)))
        polygon(f, [(hx + 1, hy + 6), (hx - 1, hy + 2), (hx + 3, hy + 4)], ramp((200, 196, 170), (240, 236, 214)))
        f.put(hx - 4, hy - 3, YELLOW_EYE); f.put(hx + 1, hy - 3, YELLOW_EYE)
        polygon(f, [(hx + 6, hy - 8), (hx + 13, hy - 12), (hx + 9, hy - 4)], TROLL)  # ear
        # front arm with the club
        if a == "attack":
            ang = math.radians(110 + 150 * p["w"]) if p["i"] < 5 else math.radians(110 + 30 * p["s"])
        else:
            ang = math.radians(112 + math.sin(math.tau * ph) * 3)
        shx, shy = cx - 14, cy - 10
        fxp, fyp = shx - 6, shy + 16
        if a == "claws":
            fxp, fyp = shx - 18 - 10 * p["s"], shy + 8 - (6 * p["s"] if p["i"] < 6 else -4 * p["s"])
        capsule(f, shx, shy, fxp, fyp, 6, 5, TROLL, tex=0.1, seed=7)
        if a == "claws":
            for k in range(3):
                line(f, fxp - 2, fyp - 2 + k * 2, fxp - 8, fyp - 4 + k * 3, (230, 226, 200))
            if p["s"] > 0.6:
                for k in range(3):
                    line(f, fxp - 18 + k * 4, fyp - 12 + k * 5, fxp - 6 + k * 4, fyp + 2 + k * 5, (200, 255, 200), 200 * p["s"])
            tipx, tipy = cx + 30, cy + 20   # the club rests on the back side while it claws
            capsule(f, cx + 22, cy + 8, tipx, tipy, 2.6, 4.5, WOOD)
        else:
            tipx, tipy = fxp + math.cos(ang) * 34, fyp + math.sin(ang) * 34
            capsule(f, fxp, fyp, tipx, tipy, 2.6, 6, WOOD, tex=0.1, seed=8)
            for k in range(3):
                t = 0.6 + k * 0.13
                spot(f, fxp + (tipx - fxp) * t, fyp + (tipy - fyp) * t - 3, (210, 200, 180), 1)
            if a == "attack" and p["i"] in (5, 6):
                sparks(f, tipx, oy - 4, 20, 22, (220, 210, 170), 220, 41 + p["i"], 1)
        if lick > 0:
            for k in range(6):
                t = ph * math.tau + k
                halo(f, cx - 4 + math.cos(t) * 18, cy - 4 + math.sin(t) * 12, 3, (140, 255, 140), 170 * lick, core=(220, 255, 220))


class Illusionist(Recipe):
    id = "illusionist"; cell = (144, 108); anchor = (96, 102); seed = 46
    anims = [("idle", 12, [], "idle"), ("attack", 10, [4], "action"), ("flash", 10, [4], "action"), ("split", 11, [6], "action"),
             ("cast", 10, [], "action"), ("hurt", 6, [], "hurt"), ("death", 14, [], "death")]
    moves = {"prism_ray": "attack", "blinding_flash": "flash", "mirror_split": "split", "hypnotic_glow": "cast", "vengeance": "cast"}
    points = {"chest": (-2, -32), "eyes": (-6, -52), "body": (0, -30), "claw": (-30, -36)}

    def body(self, f, cx, cy, oy, ph, pal_a, pal_b, mask, hat_tip=True, ox_shift=0):
        def diamonds(x, y, v):
            u, w = (x - cx) + (y - cy), (x - cx) - (y - cy)
            return v - (0.32 if (u // 6 + w // 6) % 2 else 0.0)
        hem = [(cx - 14 + k * 4.6, oy - 1 - (2 if k % 2 else 0) + math.sin(math.tau * ph + k) * 0.5) for k in range(7)]
        polygon(f, [(cx - 6, cy - 16), (cx + 7, cy - 16), (cx + 14, hem[-1][1])] + hem[::-1] + [(cx - 14, hem[0][1])], pal_a, tex=0.06, seed=15, mod=diamonds)
        polygon(f, [(cx - 7, cy - 18), (cx + 8, cy - 18), (cx + 5, cy - 12), (cx - 4, cy - 12)], pal_b)   # collar
        hx, hy = cx - 2, cy - 24
        ellipse(f, hx, hy, 6.5, 7.5, mask)
        f.put(hx - 3, hy - 1, DARK_INK); f.put(hx, hy - 1, DARK_INK); f.put(hx - 3, hy + 2, (120, 40, 140)); f.put(hx - 3, hy + 3, (120, 40, 140))
        polygon(f, [(hx - 7, hy - 5), (hx + 7, hy - 5), (hx + 6, hy - 9), (hx + 2, hy - 24), (hx - 1, hy - 20), (hx - 6, hy - 9)], pal_b, tex=0.06, seed=16)
        if hat_tip:
            tri(f, (hx + 1, hy - 28), (hx + 4, hy - 24), (hx - 1, hy - 23), ramp((140, 220, 255), (240, 250, 255)))
        return hx, hy

    def draw(self, f, p):
        ox, oy, b, ph = p["ox"], p["oy"], p["b"], p["t"]
        a = p["anim"]
        cx, cy = ox, oy - 32 + b * 0.9
        hx, hy = self.body(f, cx, cy, oy, ph, HARL_A, HARL_B, MASK)
        halo(f, hx + 2, hy - 28, 4, (180, 240, 255), 120)
        cast = p["s"] if a in ("attack", "flash", "split", "cast") else 0.0
        handx, handy = cx - 12 - cast * 8, cy - 8 - cast * 6
        capsule(f, cx - 4, cy - 12, handx, handy, 2.4, 2, HARL_B)
        ellipse(f, handx, handy, 2.2, 2.2, MASK)
        capsule(f, cx + 6, cy - 12, cx + 12, cy - 2, 2.4, 2, HARL_B)
        for k in range(3):   # orbiting prisms
            t = ph * math.tau + k * math.tau / 3
            px_, py_ = cx + math.cos(t) * 18, cy - 14 + math.sin(t) * 6
            tri(f, (px_, py_ - 3), (px_ + 2.5, py_ + 2), (px_ - 2.5, py_ + 2), ramp((90, 160, 220), (200, 240, 255), (250, 255, 255)))
        if a == "attack" and p["s"] > 0.2:
            cols = [(255, 90, 90), (255, 220, 90), (90, 255, 140), (90, 180, 255), (200, 110, 255)]
            for k, c in enumerate(cols):
                line(f, handx - 2, handy - 2 + k, handx - 40 - 10 * p["s"], handy - 4 + k * 2, c, 220 * p["s"])
        if a == "flash":
            halo(f, handx - 4, handy, 6 + 16 * p["s"], WHITE, 230 * p["s"], core=WHITE)
        if a == "split" and p["k"] > 0.3:
            t = min(1.0, (p["k"] - 0.3) / 0.6)
            for k in range(10):
                halo(f, cx - 20 - t * 18 + math.sin(k) * 6, cy - 20 + k * 4, 4, (220, 180, 255), 140 * (1 - t * 0.5))
        if a == "cast":
            for r in (8, 14, 20):
                ring(f, handx - 4, handy, r * (0.5 + p["s"] * 0.6), (255, 140, 230), 150 * p["s"])


class Illusion(Illusionist):
    id = "illusion"; cell = (128, 100); anchor = (84, 94); seed = 47
    anims = [("idle", 12, [], "idle"), ("attack", 9, [3], "action"), ("cast", 9, [], "action"), ("hurt", 6, [], "hurt"),
             ("death", 12, [], "death"), ("spawn", 8, [], "spawn")]
    moves = {"phantom_strike": "attack", "blink": "cast", "vengeance": "cast"}
    points = {"chest": (-2, -28), "eyes": (-6, -46), "body": (0, -26), "claw": (-28, -30)}

    def draw(self, f, p):
        ox, oy, b, ph = p["ox"], p["oy"], p["b"], p["t"]
        a = p["anim"]
        lunge = (-p["s"] * 9 + p["w"] * 2) if a == "attack" else 0.0
        cx, cy = ox + lunge * 0.5, oy - 28 + b * 1.1
        hx, hy = self.body(f, cx, cy, oy, ph, LAVENDER, LAVENDER, MASK, hat_tip=False)
        handx, handy = cx - 12 - (p["s"] * 10 if a == "attack" else 0), cy - 8
        capsule(f, cx - 4, cy - 12, handx, handy, 2.2, 1.8, LAVENDER)
        for k in range(5):
            t = ph * math.tau + k * 1.26
            f.glow(cx + math.cos(t) * 13, cy - 10 + math.sin(t * 2) * 12, (240, 220, 255), 150)
        if a == "attack" and p["s"] > 0.5:
            ring(f, handx - 4, handy, 8, (230, 200, 255), 200 * p["s"], arc=(math.pi * 0.6, math.pi * 1.4))
        if a == "cast":
            halo(f, cx, cy - 10, 18, (230, 210, 255), 150 * p["s"])
            sparks(f, cx, cy - 10, 12, 20, WHITE, 220 * p["s"], 51, 0.6 + p["s"])

    def spawn_pose(self, r):
        return self.idle_pose(0.0)


class Skeleton(Recipe):
    id = "skeleton"; cell = (128, 100); anchor = (84, 94); seed = 48
    anims = [("idle", 12, [], "idle"), ("attack", 9, [3], "action"), ("bash", 9, [3], "action"), ("cast", 9, [], "action"),
             ("hurt", 6, [], "hurt"), ("death", 12, [], "death"), ("spawn", 8, [], "spawn")]
    moves = {"rusty_slash": "attack", "shield_bash": "bash", "vengeance": "cast"}
    points = {"chest": (-2, -30), "eyes": (-8, -46), "body": (0, -28), "claw": (-30, -30)}

    def draw(self, f, p):
        ox, oy, b, ph = p["ox"], p["oy"], p["b"], p["t"]
        a = p["anim"]
        bash = (-p["s"] * 8 + p["w"] * 2) if a == "bash" else 0.0
        cx, cy = ox + bash * 0.6, oy - 30 + b * 0.5
        for lx in (-5, 5):   # legs
            capsule(f, cx + lx, cy + 10, cx + lx - 1, cy + 20, 1.6, 1.4, BONE)
            capsule(f, cx + lx - 1, cy + 20, cx + lx - 2, oy - 2, 1.5, 1.3, BONE)
            ellipse(f, cx + lx - 4, oy - 2, 3.5, 1.6, BONE)
        ellipse(f, cx, cy + 8, 5, 3, BONE)   # pelvis
        line(f, cx, cy - 12, cx, cy + 6, (226, 214, 186))
        for k in range(4):   # ribs
            y = cy - 10 + k * 4
            capsule(f, cx - 6 + k * 0.5, y, cx + 6 - k * 0.5, y + 1, 1.2, 1.2, BONE)
        # skull
        hx, hy = cx - 2, cy - 20
        ellipse(f, hx, hy, 6.5, 6, BONE)
        polygon(f, [(hx - 5, hy + 3), (hx + 3, hy + 3), (hx + 2, hy + 7), (hx - 4, hy + 7)], BONE)
        f.put(hx - 3, hy, (20, 30, 20)); f.put(hx, hy, (20, 30, 20))
        f.put(hx - 3, hy - 1, (120, 255, 150)); f.put(hx, hy - 1, (120, 255, 150))
        halo(f, hx - 2, hy, 4, GREEN_FIRE, 60)
        for k in range(3):
            f.put(hx - 3 + k * 2, hy + 5, (60, 50, 40))
        # back arm with a round shield
        shield_x = cx + 10 + (bash * -1.6 if a == "bash" else 0)
        if a == "bash":
            shield_x = cx - 10 - p["s"] * 8
        capsule(f, cx + 6, cy - 9, shield_x, cy - 2, 1.5, 1.4, BONE)
        ellipse(f, shield_x, cy - 1, 7, 8.5, WOOD, tex=0.12, seed=17)
        ellipse(f, shield_x, cy - 1, 2.4, 2.4, RUST)
        # front arm with a rusty sword
        ang = math.radians(250 - 150 * p["s"]) if a == "attack" else math.radians(200 + math.sin(math.tau * ph) * 4)
        shx, shy = cx - 5, cy - 10
        hx2, hy2 = shx + math.cos(ang) * 9, shy + math.sin(ang) * 9 + 4
        capsule(f, shx, shy, hx2, hy2, 1.5, 1.4, BONE)
        tx, ty = hx2 + math.cos(ang) * 18, hy2 + math.sin(ang) * 18
        line(f, hx2, hy2, tx, ty, (176, 120, 80))
        line(f, hx2 + 1, hy2, tx + 1, ty, (120, 74, 44))
        line(f, hx2 - 2, hy2 + 2, hx2 + 2, hy2 - 2, (90, 70, 50))
        if a == "attack" and p["s"] > 0.5:
            ring(f, hx2, hy2, 18, (255, 220, 170), 170 * p["s"], arc=(ang - 0.8, ang + 0.6))
        if a == "cast":
            halo(f, cx, cy - 6, 14, GREEN_FIRE, 120 * p["s"])

    def spawn_pose(self, r):
        return self.idle_pose(0.0)


class Shard(Recipe):
    id = "shard"; cell = (128, 100); anchor = (84, 94); seed = 49
    anims = [("idle", 12, [], "idle"), ("attack", 9, [3], "action"), ("cast", 9, [], "action"), ("hurt", 6, [], "hurt"),
             ("death", 12, [], "death"), ("spawn", 8, [], "spawn")]
    moves = {"cutting_glint": "attack", "refract": "cast", "vengeance": "cast"}
    points = {"chest": (0, -30), "eyes": (-4, -40), "body": (0, -30), "claw": (-26, -30)}

    def draw(self, f, p):
        ox, oy, b, ph = p["ox"], p["oy"], p["b"], p["t"]
        a = p["anim"]
        dash = (-p["s"] * 14 + p["w"] * 3) if a == "attack" else 0.0
        tilt = (p["s"] * 0.5 if a == "attack" else 0.0) + math.sin(math.tau * ph) * 0.08
        cx, cy = ox + dash, oy - 30 + b * 3
        pts = [(-2, -20), (6, -10), (5, 4), (8, 14), (-1, 19), (-7, 8), (-6, -6)]
        c, s = math.cos(tilt), math.sin(tilt)
        rp = [(cx + x * c - y * s, cy + x * s + y * c) for (x, y) in pts]
        polygon(f, rp, GLASS, tex=0.05, seed=18, mod=lambda x, y, v: v + (0.35 if (x - y) % 9 in (0, 1) else 0.0) - (0.2 if (x + y) % 13 == 0 else 0.0))
        line(f, rp[0][0], rp[0][1] + 2, rp[2][0] - 2, rp[2][1], (250, 255, 255))   # bright edge
        f.put(cx - 2, cy - 2, (190, 120, 255)); f.put(cx - 1, cy - 2, (190, 120, 255))   # its reflected eye
        halo(f, cx - 1, cy - 2, 4, VIOLET, 90)
        glint = (ph * 3) % 1.0
        halo(f, cx - 4 + glint * 8, cy - 10 + glint * 16, 3, WHITE, 140, core=WHITE)
        if a == "attack" and p["s"] > 0.5:
            line(f, cx - 10, cy - 6, cx - 30, cy - 8, (220, 245, 255), 220 * p["s"])
            line(f, cx - 10, cy + 2, cx - 26, cy + 4, (200, 230, 255), 180 * p["s"])
        if a == "cast":
            for k, col in enumerate([(255, 100, 100), (255, 230, 100), (100, 255, 150), (100, 180, 255), (210, 120, 255)]):
                t = k / 5 * math.tau + ph * math.tau
                line(f, cx, cy, cx + math.cos(t) * 18 * p["s"], cy + math.sin(t) * 12 * p["s"], col, 200 * p["s"])

    def spawn_pose(self, r):
        return self.idle_pose(0.0)


# ------------------------------------------------------------------ elites
class Paladin(Recipe):
    id = "paladin"; cell = (208, 120); anchor = (142, 115); boss = True; elite = True; seed = 50
    anims = [("idle", 12, [], "idle"), ("attack", 11, [5], "action"), ("condemn", 11, [3, 6], "action"), ("fury", 12, [3, 6, 9], "action"),
             ("oath", 12, [], "action"), ("cast", 10, [], "action"), ("hurt", 6, [], "hurt"), ("death", 15, [], "death")]
    moves = {"judgment_hammer": "attack", "condemn": "condemn", "unholy_fury": "fury", "oath_broken": "oath", "shield_of_faith": "cast",
             "profane_roar": "cast", "vengeance": "cast"}
    points = {"chest": (-4, -50), "eyes": (-10, -74), "body": (0, -46), "claw": (-58, -30)}

    def draw(self, f, p):
        ox, oy, b, ph = p["ox"], p["oy"], p["b"], p["t"]
        a = p["anim"]
        step = (-p["s"] * 6 + p["w"] * 2) if a in ("attack", "condemn", "fury") else 0.0
        cx, cy = ox + 2 + step * 0.5, oy - 48 + b * 0.6
        rage = p["s"] if a == "oath" else 0.0
        capsule(f, cx + 8, cy + 18, cx + 10, oy - 4, 5.5, 5, DARKSTEEL, tex=0.08, seed=1)
        capsule(f, cx - 8 + step, cy + 18, cx - 12 + step, oy - 4, 5.5, 5, DARKSTEEL, tex=0.08, seed=2)
        ellipse(f, cx + 11, oy - 3, 7, 3, DARKSTEEL); ellipse(f, cx - 13 + step, oy - 3, 7, 3, DARKSTEEL)
        # kite shield on the back arm (raised in front for Escudo de Fe)
        guard = p["s"] if a == "cast" else 0.0
        sx, sy = cx + 22 - guard * 40, cy - 2 - guard * 4
        polygon(f, [(sx - 10, sy - 14), (sx + 10, sy - 14), (sx + 9, sy + 4), (sx, sy + 16), (sx - 9, sy + 4)], DARKSTEEL, tex=0.08, seed=3)
        polygon(f, [(sx - 7, sy - 11), (sx + 7, sy - 11), (sx + 6, sy + 2), (sx, sy + 11), (sx - 6, sy + 2)], TARNISH, tex=0.1, seed=4)
        line(f, sx, sy - 9, sx, sy + 8, (120, 30, 30)); line(f, sx - 5, sy - 3, sx + 5, sy - 3, (120, 30, 30))
        # torso armour and the torn tabard
        ellipse(f, cx, cy - 4, 18, 20, DARKSTEEL, tex=0.1, seed=5)
        polygon(f, [(cx - 11, cy - 12), (cx + 11, cy - 12), (cx + 13, cy + 22), (cx + 6, cy + 18), (cx + 1, cy + 24), (cx - 6, cy + 18), (cx - 13, cy + 23)],
                TABARD, tex=0.12, seed=6)
        ellipse(f, cx, cy, 5, 5, TARNISH)
        for k in range(3):
            spot(f, cx - 6 + k * 5, cy + 12 + (k % 2) * 3, (110, 24, 28), 2)   # old blood
        polygon(f, [(cx - 22, cy - 22), (cx - 8, cy - 26), (cx - 8, cy - 14), (cx - 20, cy - 12)], DARKSTEEL, tex=0.08, seed=7)
        polygon(f, [(cx + 8, cy - 26), (cx + 22, cy - 22), (cx + 20, cy - 12), (cx + 8, cy - 14)], DARKSTEEL, tex=0.08, seed=8)
        line(f, cx - 21, cy - 21, cx - 9, cy - 25, (200, 160, 80)); line(f, cx + 9, cy - 25, cx + 21, cy - 21, (200, 160, 80))
        # horned great helm
        hx, hy = cx - 3, cy - 32
        polygon(f, [(hx - 9, hy - 8), (hx + 8, hy - 8), (hx + 9, hy + 8), (hx - 10, hy + 8)], DARKSTEEL, tex=0.08, seed=9)
        line(f, hx - 9, hy - 8, hx + 8, hy - 8, (190, 150, 70))
        polygon(f, [(hx - 9, hy - 6), (hx - 16, hy - 16), (hx - 12, hy - 6)], TARNISH)
        polygon(f, [(hx + 7, hy - 6), (hx + 14, hy - 17), (hx + 11, hy - 6)], TARNISH)
        eyec = mix((255, 180, 60), (255, 40, 30), min(1.0, rage + (0.0 if a != "fury" else 0.8)))
        line(f, hx - 7, hy, hx + 4, hy, eyec)
        halo(f, hx - 2, hy, 6, eyec, 110 + 100 * rage)
        # front arm with the war hammer
        if a == "attack":
            theta = 115 + 150 * p["w"] if p["i"] < 5 else 115 + 30 * p["s"]
        elif a in ("condemn", "fury"):
            theta = 140 + 90 * p["w"] - 60 * p["s"]
        else:
            theta = 118 + math.sin(math.tau * ph) * 3
        th = math.radians(theta)
        shx, shy = cx - 14, cy - 14
        hxp, hyp = shx - 8, shy + 10
        capsule(f, shx, shy, hxp, hyp, 5, 4.5, DARKSTEEL, tex=0.08, seed=10)
        ex, ey = hxp + math.cos(th) * 36, hyp + math.sin(th) * 36
        capsule(f, hxp, hyp, ex, ey, 1.8, 1.8, WOOD)
        nx, ny = -math.sin(th) * 9, math.cos(th) * 9
        dx_, dy_ = math.cos(th) * 9, math.sin(th) * 9
        polygon(f, [(ex - nx, ey - ny), (ex + nx, ey + ny), (ex + nx + dx_, ey + ny + dy_), (ex - nx + dx_, ey - ny + dy_)], DARKSTEEL, tex=0.1, seed=11)
        line(f, ex - nx * 0.8, ey - ny * 0.8, ex + nx * 0.8, ey + ny * 0.8, (200, 160, 80))
        if a == "attack" and p["i"] in (5, 6):
            sparks(f, ex, oy - 4, 22, 24, (255, 220, 120), 230, 51 + p["i"], 1)
        if a in ("condemn", "fury") and p["s"] > 0.6:
            ring(f, hxp, hyp, 36, (255, 120, 80) if a == "fury" else (255, 220, 140), 170 * p["s"], arc=(th - 0.5, th + 0.5))
        if guard > 0:
            halo(f, sx, sy, 16 + guard * 6, (255, 220, 120), 150 * guard)
        if rage > 0:
            for k in range(10):
                t = ph * math.tau + k * 0.63
                halo(f, cx + math.cos(t) * 22, cy - 6 - (k * 5 + p["k"] * 20) % 40, 4, (255, 60, 30), 190 * rage, core=(255, 200, 120))


class Countess(Recipe):
    id = "countess"; cell = (208, 120); anchor = (142, 115); boss = True; elite = True; seed = 51
    anims = [("idle", 12, [], "idle"), ("attack", 10, [4], "action"), ("cuts", 11, [3, 6], "action"), ("bite", 11, [5], "action"),
             ("hunger", 12, [], "action"), ("cast", 10, [], "action"), ("hurt", 6, [], "hurt"), ("death", 15, [], "death")]
    moves = {"crimson_kiss": "attack", "bleeding_cut": "cuts", "crimson_bite": "bite", "eternal_hunger": "hunger", "call_colony": "hunger",
             "mist_veil": "cast", "vengeance": "cast"}
    points = {"chest": (-2, -52), "eyes": (-8, -76), "body": (0, -44), "claw": (-50, -54)}

    def draw(self, f, p):
        ox, oy, b, ph = p["ox"], p["oy"], p["b"], p["t"]
        a = p["anim"]
        lunge = (-p["s"] * 12 + p["w"] * 3) if a in ("attack", "bite") else 0.0
        spread = p["s"] if a == "hunger" else 0.0
        cx, cy = ox + lunge * 0.4, oy - 50 + b * 0.8
        # cape: black outside, crimson inside; spreads like wings when hungry
        wing = 12 + spread * 22
        polygon(f, [(cx + 4, cy - 24), (cx + 10 + wing, cy - 30 - spread * 10), (cx + 18 + wing * 0.6, cy + 4), (cx + 14, oy - 2), (cx + 2, oy - 2)],
                CAPE_OUT, tex=0.08, seed=1)
        polygon(f, [(cx + 4, cy - 22), (cx + 8 + wing * 0.8, cy - 26 - spread * 8), (cx + 12 + wing * 0.5, cy + 2), (cx + 8, cy + 30)], GOWN, tex=0.1, seed=2)
        # gown (bell) and corset
        hem = [(cx - 22 + k * 6, oy - 1 - (2 if k % 2 else 0)) for k in range(8)]
        polygon(f, [(cx - 7, cy - 8), (cx + 8, cy - 8), (cx + 22, hem[-1][1])] + hem[::-1] + [(cx - 24, hem[0][1])], GOWN, tex=0.1, seed=3,
                mod=bands(8, 0.22, "x", 3))
        polygon(f, [(cx - 7, cy - 22), (cx + 7, cy - 22), (cx + 8, cy - 6), (cx - 8, cy - 6)], CAPE_OUT, tex=0.06, seed=4)
        for k in range(4):
            f.put(cx - 1, cy - 20 + k * 4, (200, 40, 60))
        # high collar
        polygon(f, [(cx - 10, cy - 24), (cx - 14, cy - 40), (cx - 4, cy - 28)], CAPE_OUT)
        polygon(f, [(cx + 10, cy - 24), (cx + 14, cy - 40), (cx + 4, cy - 28)], CAPE_OUT)
        # head
        hx, hy = cx - 2 + lunge * 0.4, cy - 32
        ellipse(f, hx + 1, hy - 3, 8, 7, HAIR)
        ellipse(f, hx + 3, hy - 9, 5, 4, HAIR)
        ellipse(f, hx - 1, hy + 1, 5.5, 6.5, VAMPSKIN)
        f.put(hx - 3, hy, CRIMSON); f.put(hx, hy, CRIMSON)
        halo(f, hx - 2, hy, 4, CRIMSON, 90)
        f.put(hx - 2, hy + 4, (150, 20, 40)); f.put(hx - 1, hy + 4, (150, 20, 40))
        if a in ("attack", "bite") and p["s"] > 0.4:
            f.put(hx - 3, hy + 5, WHITE); f.put(hx - 1, hy + 5, WHITE)
        # arms with long nails
        if a == "cuts":
            reach = p["s"]
            hx2, hy2 = cx - 22 - reach * 12, cy - 14 + (8 * reach if p["i"] < 6 else -8 * reach)
        elif a == "attack":
            hx2, hy2 = cx - 18 - p["s"] * 10, cy - 18
        elif a == "cast":
            hx2, hy2 = cx - 14, cy - 26 - p["s"] * 6
        else:
            hx2, hy2 = cx - 12, cy - 6 + math.sin(math.tau * ph) * 1.5
        capsule(f, cx - 6, cy - 18, hx2, hy2, 2.2, 1.8, VAMPSKIN)
        for k in range(3):
            line(f, hx2, hy2, hx2 - 4, hy2 - 1 + k * 1.5, (120, 10, 30))
        capsule(f, cx + 6, cy - 18, cx + 10, cy - 4, 2.2, 1.8, VAMPSKIN)
        if a == "cuts" and p["s"] > 0.6:
            for k in range(3):
                line(f, hx2 - 16 + k * 4, hy2 - 10 + k * 4, hx2 - 4 + k * 4, hy2 + 4 + k * 4, (255, 60, 90), 220 * p["s"])
        if a in ("attack", "bite") and p["s"] > 0.6:
            halo(f, hx - 14, hy + 4, 8, CRIMSON, 200 * p["s"])
        if a == "cast":
            for k in range(8):
                t = ph * math.tau + k * 0.8
                halo(f, cx + math.cos(t) * 26, cy + math.sin(t) * 10 + 10, 7, (180, 170, 190), 110 * p["s"])
        if spread > 0:
            for k in range(4):
                t = p["k"] * 2 + k * 0.4
                bx, by = cx - 10 - (t % 1.0) * 50, cy - 30 - math.sin(t * 7) * 8 - k * 4
                polygon(f, [(bx - 5, by - 2), (bx, by + 1), (bx + 5, by - 2), (bx, by + 3)], CAPE_OUT)
                f.put(bx, by + 1, CRIMSON)


class HighPriest(Recipe):
    id = "highpriest"; cell = (208, 120); anchor = (142, 115); boss = True; elite = True; seed = 52
    anims = [("idle", 12, [], "idle"), ("attack", 10, [4], "action"), ("flames", 11, [4, 7], "action"), ("call", 11, [6], "action"),
             ("offering", 12, [7], "action"), ("cast", 10, [], "action"), ("hurt", 6, [], "hurt"), ("death", 15, [], "death")]
    moves = {"ceremonial_dagger": "attack", "abyss_punish": "attack", "abyss_flames": "flames", "abyss_call": "call", "dark_offering": "offering",
             "abyss_blessing": "cast", "ascension": "cast", "vengeance": "cast"}
    points = {"chest": (-4, -54), "eyes": (-8, -84), "body": (0, -48), "claw": (-48, -50)}

    def draw(self, f, p):
        ox, oy, b, ph = p["ox"], p["oy"], p["b"], p["t"]
        a = p["anim"]
        cx, cy = ox + 2, oy - 50 + b * 0.8
        power = p["s"] if a in ("flames", "call", "offering", "cast") else 0.0
        polygon(f, [(cx - 12, cy - 22), (cx + 13, cy - 22), (cx + 24, oy - 1), (cx - 26, oy - 1)], INDIGO, tex=0.1, seed=1, mod=bands(7, 0.25, "x", 4))
        for side in (-1, 1):
            line(f, cx + side * 12, cy - 20, cx + side * 24, oy - 2, VIOLET)
        polygon(f, [(cx - 5, cy - 20), (cx + 5, cy - 20), (cx + 7, oy - 1), (cx - 7, oy - 1)], ramp((40, 20, 70), (70, 40, 110), (110, 70, 160)))
        polygon(f, [(cx - 16, cy - 26), (cx + 16, cy - 26), (cx + 11, cy - 16), (cx - 11, cy - 16)], INDIGO)
        # golden mask with one vertical eye, tall headdress
        hx, hy = cx - 2, cy - 34
        polygon(f, [(hx - 7, hy - 8), (hx + 7, hy - 8), (hx + 6, hy + 8), (hx, hy + 11), (hx - 6, hy + 8)], GOLD, tex=0.06, seed=2)
        line(f, hx - 1, hy - 4, hx - 1, hy + 3, (60, 10, 80))
        f.put(hx - 1, hy - 1, (230, 120, 255))
        halo(f, hx - 1, hy - 1, 4 + power * 3, VIOLET, 100 + 100 * power)
        polygon(f, [(hx - 8, hy - 8), (hx - 5, hy - 30), (hx + 1, hy - 36), (hx + 6, hy - 30), (hx + 9, hy - 8)], INDIGO, tex=0.06, seed=3)
        line(f, hx - 5, hy - 28, hx + 6, hy - 28, (200, 160, 80))
        # staff (back hand) with a dark orb
        sx = cx + 20
        top = cy - 40 - (power * 6 if a in ("call", "cast") else 0)
        capsule(f, cx + 12, cy - 18, sx, cy - 12, 3.5, 3, INDIGO)
        line(f, sx, top, sx, oy - 2, (110, 76, 46))
        line(f, sx + 1, top, sx + 1, oy - 2, (70, 46, 26))
        ellipse(f, sx, top - 4, 5, 5, ramp((10, 4, 20), (40, 14, 70), (90, 40, 140)))
        halo(f, sx, top - 4, 7 + power * 6, VIOLET, 130 + 110 * power, core=(240, 200, 255))
        # ceremonial dagger (front hand)
        if a == "attack":
            hx2, hy2 = cx - 18 - p["s"] * 14, cy - 12
        elif a == "offering":
            hx2, hy2 = cx - 16, cy - 24 - p["s"] * 6
        else:
            hx2, hy2 = cx - 14, cy - 8 + math.sin(math.tau * ph) * 1.5
        capsule(f, cx - 8, cy - 18, hx2, hy2, 3.2, 2.8, INDIGO)
        ellipse(f, hx2, hy2, 2.4, 2.4, PALESKIN)
        line(f, hx2 - 1, hy2, hx2 - 12, hy2 - 2, (230, 220, 240))
        line(f, hx2 - 1, hy2 + 1, hx2 - 11, hy2 - 1, (150, 140, 170))
        if a == "flames":
            for st in (4, 7):
                d = p["i"] - st
                if 0 <= d <= 2:
                    x = sx - 20 - d * 26
                    halo(f, x, top - 2 + d * 6, 7, VIOLET, 230, core=(255, 220, 255))
        if a == "call":
            ring(f, cx - 50, oy - 4, 10 + 12 * p["s"], VIOLET, 200 * p["s"])
            ring(f, cx - 50, oy - 4, 6 + 8 * p["s"], (240, 200, 255), 140 * p["s"], width=0.8)
        if a == "offering" and p["s"] > 0.2:
            for k in range(8):
                t = k / 8
                halo(f, cx - 60 + t * 50, oy - 20 - math.sin(t * math.pi) * 30, 3.5, (200, 40, 80), 200 * p["s"], core=(255, 160, 190))
        if a == "cast":
            for k in range(6):
                t = ph * math.tau + k
                halo(f, cx + math.cos(t) * 24, cy - 10 + math.sin(t) * 8 - p["s"] * 8, 4, VIOLET, 170 * p["s"])


# ------------------------------------------------------------------ bosses
class Necromancer(Recipe):
    id = "necromancer"; cell = (224, 124); anchor = (150, 116); boss = True; seed = 53
    anims = [("idle", 16, [], "idle"), ("attack", 10, [4], "action"), ("harvest", 12, [7], "action"), ("raise", 12, [6], "action"),
             ("dance", 12, [], "action"), ("cast", 10, [], "action"), ("hurt", 6, [], "hurt"), ("death", 15, [], "death")]
    moves = {"necrotic_touch": "attack", "soul_harvest": "harvest", "raise_bones": "raise", "danse_macabre": "dance", "bone_wall": "cast",
             "bone_order": "cast", "vengeance": "cast"}
    points = {"chest": (-6, -56), "eyes": (-10, -84), "body": (0, -48), "claw": (-50, -54)}

    def draw(self, f, p):
        ox, oy, b, ph = p["ox"], p["oy"], p["b"], p["t"]
        a = p["anim"]
        fl = -4 + b * 2
        cx, cy = ox + 2, oy - 50 + fl
        power = p["s"] if a in ("harvest", "raise", "dance", "cast") else 0.0
        hem = [(cx - 26 + k * 6.5, oy - 4 + fl * 0.5 + (4 if k % 2 else 0) + math.sin(math.tau * ph + k) * 1.4) for k in range(9)]
        polygon(f, [(cx - 12, cy - 22), (cx + 13, cy - 22), (cx + 26, hem[-1][1])] + hem[::-1] + [(cx - 26, hem[0][1])], LICH, tex=0.14, seed=1,
                mod=bands(6, 0.24, "x", ph * 6))
        for k in range(4):   # bone ornaments on the robe
            capsule(f, cx - 9 + k * 6, cy - 6 + (k % 2) * 4, cx - 7 + k * 6, cy + 2 + (k % 2) * 4, 1.2, 1.2, SKULL)
        polygon(f, [(cx - 16, cy - 26), (cx + 16, cy - 26), (cx + 10, cy - 14), (cx - 10, cy - 14)], LICH)
        # hood and skull face with green flames in the sockets
        hx, hy = cx - 2, cy - 34
        ellipse(f, hx + 2, hy - 1, 11, 11, LICH, tex=0.08, seed=2)
        ellipse(f, hx - 1, hy + 1, 6.5, 7, SKULL)
        polygon(f, [(hx - 5, hy + 5), (hx + 3, hy + 5), (hx + 2, hy + 9), (hx - 4, hy + 9)], SKULL)
        for ex in (-4, 0):
            f.put(hx + ex, hy, (10, 30, 14)); f.put(hx + ex + 1, hy, (10, 30, 14))
            halo(f, hx + ex + 0.5, hy - 1, 3 + power * 2, GREEN_FIRE, 140 + 80 * power, core=(220, 255, 220))
        for k in range(3):
            f.put(hx - 3 + k * 2, hy + 7, (50, 44, 36))
        for k in range(5):   # bone crown
            tri(f, (hx - 7 + k * 3.5, hy - 9), (hx - 5.5 + k * 3.5, hy - 15 - (k % 2) * 3), (hx - 4 + k * 3.5, hy - 9), SKULL)
        # staff with a skull and green fire (back hand)
        sx, top = cx + 22, cy - 44 - (power * 6 if a in ("raise", "dance") else 0)
        capsule(f, cx + 12, cy - 18, sx, cy - 10, 3.5, 3, LICH)
        line(f, sx, top + 6, sx, oy - 2, (90, 80, 60)); line(f, sx + 1, top + 6, sx + 1, oy - 2, (60, 52, 38))
        ellipse(f, sx, top, 5, 4.5, SKULL)
        f.put(sx - 2, top, (10, 30, 14)); f.put(sx + 1, top, (10, 30, 14))
        for k in range(4):
            halo(f, sx + math.sin(ph * math.tau * 2 + k) * 2, top - 6 - k * 3, 4 - k * 0.6, GREEN_FIRE, 170 - k * 30, core=(230, 255, 230))
        # bony front hand
        if a == "attack":
            hx2, hy2 = cx - 20 - p["s"] * 14, cy - 14
        elif a in ("raise", "dance", "cast"):
            hx2, hy2 = cx - 18, cy - 28 - power * 6
        else:
            hx2, hy2 = cx - 14, cy - 8 + math.sin(math.tau * ph) * 1.5
        capsule(f, cx - 8, cy - 18, hx2, hy2, 3.5, 3, LICH)
        for k in range(3):
            line(f, hx2, hy2, hx2 - 4, hy2 - 2 + k * 2, (220, 212, 186))
        if a == "attack" and p["s"] > 0.3:
            halo(f, hx2 - 8, hy2, 7 + p["s"] * 4, GREEN_FIRE, 220 * p["s"], core=(230, 255, 230))
        for k in range(4):   # orbiting bone fragments
            t = ph * math.tau + k * math.tau / 4 + (p["k"] * 6 if a == "dance" else 0)
            bx, by = cx + math.cos(t) * (30 + power * 6), cy - 6 + math.sin(t) * 8
            capsule(f, bx - 2, by - 1, bx + 2, by + 1, 1.2, 1.2, SKULL)
        if a == "harvest":
            for k in range(6):
                t = (p["k"] + k / 6) % 1.0
                halo(f, cx - 60 + t * 40, cy - 30 + math.sin(t * 9) * 10, 3, GREEN_FIRE, 180)
            if p["i"] >= 7:
                d = min(1.0, (p["i"] - 7) / 3)
                x = cx - 30 - d * 60
                ellipse(f, x, cy - 10, 6, 5, SKULL)
                halo(f, x, cy - 10, 10, GREEN_FIRE, 220 * (1 - d * 0.5))
        if a == "raise":
            ring(f, cx - 60, oy - 4, 10 + 14 * p["s"], GREEN_FIRE, 200 * p["s"])
            for k in range(3):
                bx = cx - 70 + k * 10
                capsule(f, bx, oy - 2, bx + 1, oy - 2 - p["s"] * 10, 1.4, 1.2, SKULL)
        if a == "cast":
            for k in range(5):
                bx = cx - 60 + k * 8
                capsule(f, bx, oy - 2, bx + 2, oy - 4 - p["s"] * (12 + (k % 2) * 6), 2, 1.6, SKULL)
        if a == "dance":
            for r in (20, 30, 40):
                ring(f, cx, cy - 6, r * (0.6 + p["s"] * 0.5), GREEN_FIRE, 120 * p["s"])


class Hydra(Recipe):
    id = "hydra"; cell = (240, 124); anchor = (160, 116); boss = True; seed = 54
    anims = [("idle", 16, [], "idle"), ("attack", 12, [3, 5, 7], "action"), ("breath", 12, [6], "action"), ("submerge", 12, [], "action"),
             ("grow", 12, [], "action"), ("cast", 10, [], "action"), ("hurt", 6, [], "hurt"), ("death", 15, [], "death")]
    moves = {"many_bites": "attack", "acid_breath": "breath", "submerge": "submerge", "new_heads": "grow", "hydra_fury": "grow", "vengeance": "cast"}
    points = {"chest": (-10, -40), "eyes": (-40, -76), "body": (0, -30), "claw": (-70, -50)}
    # neck bases (x offset from the anchor), head rest positions, size; the first three are the old heads
    NECKS = [(-14, (-46, -60), 1.15), (2, (-30, -80), 1.0), (16, (-6, -70), 0.95), (-26, (-62, -40), 0.85), (24, (14, -54), 0.8)]

    def head(self, f, x, y, s, mouth=0.0, acid=False):
        ellipse(f, x + 3 * s, y, 7 * s, 5.5 * s, HYDRA, tex=0.1, seed=int(x))
        polygon(f, [(x - 2 * s, y - 3 * s), (x - 11 * s, y - 1 * s), (x - 11 * s, y + 1 * s), (x - 2 * s, y + 2 * s)], HYDRA)   # snout
        polygon(f, [(x - 1 * s, y + 2 * s), (x - 10 * s, y + 2 * s + mouth * 5 * s), (x - 1 * s, y + 4 * s)], HYDRA_BELLY)   # jaw
        tri(f, (x + 5 * s, y - 4 * s), (x + 12 * s, y - 9 * s), (x + 8 * s, y - 2 * s), HYDRA)   # horn fin
        f.put(x - 1, y - 2 * s, (255, 150, 40))
        halo(f, x - 1, y - 2 * s, 2.5, (255, 140, 40), 90)
        if mouth > 0.3:
            for k in range(3):
                f.put(x - 4 * s - k * 2, y + 2 * s, TEETH)
            if acid:
                halo(f, x - 14 * s, y + 3 * s, 6, (170, 255, 90), 220 * mouth, core=(230, 255, 180))

    def draw(self, f, p):
        ox, oy, b, ph = p["ox"], p["oy"], p["b"], p["t"]
        a = p["anim"]
        sink = 14 * p["s"] if a == "submerge" else 0.0
        grow = p["s"] if a == "grow" else 0.0
        cx, cy = ox, oy - 22 + b * 0.8 + sink
        water_y = oy - 12
        # body hump behind the water
        ellipse(f, cx + 6, cy, 34, 18, HYDRA, tex=0.12, seed=1, clip=lambda x, y: y <= water_y + 1,
                mod=lambda x, y, v: v - (0.25 if (x + y * 2) % 7 == 0 else 0.0))
        tail = [(cx + 36, cy + 4), (cx + 50, cy - 6 + math.sin(math.tau * ph) * 3), (cx + 60, cy - 12)]
        curve(f, tail, [6, 4, 2], HYDRA)
        heads = 3 + (2 if grow > 0.5 else 0)
        idle_heads = 3
        n = heads if a == "grow" else idle_heads
        for k, (bx, (hx, hy), s) in enumerate(self.NECKS[:n] if a != "grow" else self.NECKS[:3]):
            sway = math.sin(math.tau * ph + k * 1.7) * 2.5
            hx, hy = ox + hx + sway, oy + hy + b * 1.2 + sink
            mouth = 0.0
            if a == "attack":
                st = (3, 5, 7)[k % 3]
                d = p["i"] - st
                if d in (-1, 0, 1):
                    lunge = (1.0 if d == 0 else 0.5) * 18
                    hx -= lunge; hy += lunge * 0.4
                    mouth = 1.0 if d == 0 else 0.5
            if a == "breath" and k == 1:
                mouth = p["s"]
                hx -= 6 * p["s"]
            base = (ox + bx, cy - 8)
            mid = ((base[0] + hx) / 2 + 6, (base[1] + hy) / 2 + 4)
            curve(f, [base, mid, (hx + 6 * s, hy + 2)], [7 * s, 5 * s, 4 * s], HYDRA, tex=0.08, seed=10 + k)
            curve(f, [(base[0] - 3, base[1] + 2), (mid[0] - 3, mid[1] + 2), (hx + 3 * s, hy + 4)], [3 * s, 2.4 * s, 2 * s], HYDRA_BELLY)
            self.head(f, hx, hy, s, mouth, acid=(a == "breath"))
        if a == "grow":
            for k, (bx, (hx, hy), s) in enumerate(self.NECKS[3:]):
                t = grow
                hx2, hy2 = ox + bx + (hx - bx) * t, cy - 8 + (oy + hy - (cy - 8)) * t
                base = (ox + bx, cy - 8)
                curve(f, [base, ((base[0] + hx2) / 2, (base[1] + hy2) / 2), (hx2, hy2)], [6 * s * t + 1, 4 * s * t + 1, 3 * s * t + 1], HYDRA)
                if t > 0.4:
                    self.head(f, hx2, hy2, s * t)
                halo(f, hx2, hy2, 8, (170, 255, 200), 160 * t)
        # water surface with ripples, drawn last over the body
        for x in range(int(ox - 80), int(ox + 74)):
            yy = water_y + int(math.sin(x * 0.35 + ph * math.tau) * 1.2)
            for y in range(yy, oy):
                v = 0.25 + 0.5 * (y - yy) / max(1, oy - yy)
                f.put(x, y, kit._tone(WATER, 1 - v, x, y))
            if (x + int(ph * 16)) % 9 == 0:
                f.put(x, yy, (150, 210, 220))
        if a == "breath" and p["s"] > 0.3:
            for k in range(8):
                t = k / 8
                halo(f, ox - 70 - t * 50, oy - 70 + t * 30, 5 + t * 4, (160, 255, 80), 200 * p["s"] * (1 - t * 0.5))
        if a == "submerge":
            for k in range(6):
                halo(f, ox - 50 + k * 18, water_y - 3 - p["s"] * 4, 5, (180, 230, 240), 150 * p["s"])
        if a == "cast":
            halo(f, ox, oy - 50, 30, (140, 255, 200), 100 * p["s"])


class MirrorQueen(Recipe):
    id = "mirrorqueen"; cell = (224, 124); anchor = (150, 116); boss = True; seed = 55
    anims = [("idle", 16, [], "idle"), ("attack", 10, [3, 6], "action"), ("ray", 12, [7], "action"), ("shatter", 12, [6], "action"),
             ("mirror", 12, [], "action"), ("cast", 10, [], "action"), ("hurt", 6, [], "hurt"), ("death", 15, [], "death")]
    moves = {"prism_cut": "attack", "mirror_ray": "ray", "shatter": "shatter", "cursed_mirror": "mirror", "prism_glow": "cast",
             "thousand_reflections": "cast", "vengeance": "cast"}
    points = {"chest": (-4, -56), "eyes": (-8, -82), "body": (0, -50), "claw": (-48, -60)}

    def draw(self, f, p):
        ox, oy, b, ph = p["ox"], p["oy"], p["b"], p["t"]
        a = p["anim"]
        cx, cy = ox + 2, oy - 52 + b * 0.8
        power = p["s"] if a in ("ray", "shatter", "mirror", "cast") else 0.0
        hem = [(cx - 26 + k * 6.5, oy - 1 - (2 if k % 2 else 0)) for k in range(9)]
        polygon(f, [(cx - 8, cy - 10), (cx + 9, cy - 10), (cx + 26, hem[-1][1])] + hem[::-1] + [(cx - 26, hem[0][1])], GLASS, tex=0.04, seed=1,
                mod=lambda x, y, v: v - (0.28 if (x - int(cx)) % 7 == 0 else 0.0) + (0.22 if (x * 2 + y) % 23 == 0 else 0.0))
        polygon(f, [(cx - 8, cy - 26), (cx + 8, cy - 26), (cx + 9, cy - 8), (cx - 9, cy - 8)], GLASS, tex=0.05, seed=2,
                mod=lambda x, y, v: v + (0.3 if (x + y) % 6 == 0 else 0.0))
        ellipse(f, cx, cy - 18, 3.5, 3.5, ramp((90, 40, 140), (170, 110, 240), (230, 200, 255)))
        halo(f, cx, cy - 18, 5 + power * 4, VIOLET, 110 + 90 * power)
        # shoulders of crystal
        tri(f, (cx - 9, cy - 26), (cx - 20, cy - 30), (cx - 12, cy - 18), GLASS)
        tri(f, (cx + 9, cy - 26), (cx + 20, cy - 30), (cx + 12, cy - 18), GLASS)
        # porcelain face and a crown of crystal spikes
        hx, hy = cx - 2, cy - 34
        ellipse(f, hx + 1, hy - 1, 7.5, 8.5, ramp((40, 40, 70), (80, 80, 120), (130, 130, 170)))   # dark hair veil
        ellipse(f, hx - 1, hy + 1, 5.5, 6.5, MASK)
        f.put(hx - 3, hy, (150, 90, 230)); f.put(hx, hy, (150, 90, 230))
        f.put(hx - 2, hy + 4, (190, 120, 160))
        for k in range(5):
            h = 9 + (4 if k == 2 else 0) + (k % 2) * 2
            tri(f, (hx - 7 + k * 3.5, hy - 7), (hx - 5.5 + k * 3.5, hy - 7 - h), (hx - 4 + k * 3.5, hy - 7), GLASS)
        halo(f, hx, hy - 16, 4, (220, 240, 255), 120)
        # hand mirror (front hand)
        if a == "attack":
            mx, my = cx - 22 - p["s"] * 8, cy - 18
        elif a in ("ray", "mirror"):
            mx, my = cx - 24, cy - 26 - power * 4
        else:
            mx, my = cx - 16, cy - 10 + math.sin(math.tau * ph) * 1.5
        capsule(f, cx - 7, cy - 22, mx + 3, my + 6, 2.2, 1.8, MASK)
        line(f, mx + 2, my + 4, mx + 5, my + 12, (180, 140, 60))
        ellipse(f, mx, my, 6, 7.5, GOLD)
        ellipse(f, mx, my, 4.2, 5.8, ramp((40, 30, 70), (90, 80, 140), (170, 170, 220), (230, 240, 255)) if a != "mirror" else ramp((20, 4, 30), (60, 10, 80), (120, 30, 150)))
        capsule(f, cx + 7, cy - 22, cx + 12, cy - 8, 2.2, 1.8, MASK)
        for k in range(4):   # floating shards
            t = ph * math.tau + k * math.tau / 4
            sx, sy = cx + math.cos(t) * (30 + power * 8), cy - 14 + math.sin(t) * 10
            tri(f, (sx, sy - 4), (sx + 2.5, sy + 3), (sx - 2.5, sy + 2), GLASS)
        if a == "attack":
            for st in (3, 6):
                d = p["i"] - st
                if 0 <= d <= 2:
                    x = cx - 30 - d * 24
                    tri(f, (x, cy - 22 + st), (x - 8, cy - 20 + st), (x, cy - 18 + st), GLASS)
                    line(f, x + 2, cy - 20 + st, x + 18, cy - 20 + st, (220, 245, 255), 180)
        if a == "ray" and p["i"] >= 6:
            t = min(1.0, (p["i"] - 6) / 3)
            line(f, mx - 6, my, mx - 6 - 90 * t, my + 10 * t, (250, 255, 255), 250)
            line(f, mx - 6, my + 1, mx - 6 - 90 * t, my + 11 * t, (170, 220, 255), 200)
            halo(f, mx - 6, my, 8, WHITE, 220)
        if a == "shatter" and p["i"] >= 5:
            t = min(1.0, (p["i"] - 5) / 4)
            for k in range(8):
                ang = k / 8 * math.tau
                sx, sy = cx + math.cos(ang) * (12 + 40 * t), cy - 14 + math.sin(ang) * (8 + 20 * t)
                tri(f, (sx, sy - 3), (sx + 2, sy + 2), (sx - 2, sy + 2), GLASS)
            sparks(f, cx, cy - 14, 18, 30 * t + 6, (220, 245, 255), 200 * (1 - t * 0.5), 61, 1)
        if a == "mirror":
            for r in (6, 10, 14):
                ring(f, mx, my, r * (0.6 + power * 0.6), (170, 60, 220), 170 * power)
        if a == "cast":
            for k, col in enumerate([(255, 110, 110), (255, 230, 110), (110, 255, 160), (110, 190, 255), (220, 130, 255)]):
                t = k / 5 * math.tau + ph * math.tau
                halo(f, cx + math.cos(t) * 28, cy - 16 + math.sin(t) * 10, 4, col, 190 * power)


RECIPES = [Sentinel, Zealot, Shade, Leech, Troll, Illusionist, Illusion, Skeleton, Shard, Paladin, Countess, HighPriest, Necromancer, Hydra, MirrorQueen]


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--only", default="")
    ap.add_argument("--preview", default="")
    ap.add_argument("--styles", action="store_true")
    ap.add_argument("--check", action="store_true")
    args = ap.parse_args(argv)
    known = {r.id: r for r in RECIPES}
    chosen = list(RECIPES)
    if args.only:
        ids = [s.strip() for s in args.only.split(",") if s.strip()]
        for i in ids:
            if i in kit.EXISTING or i in FLOOR1:
                sys.stderr.write("%s no es una hoja del piso 2: no se regenera aquí\n" % i)
                return 2
            if i not in known:
                sys.stderr.write("id desconocido: %s\n" % i)
                return 2
        chosen = [known[i] for i in ids]
    failed = False
    for cls in chosen:
        recipe = cls()
        sheet, meta, rows = kit.build(recipe)
        problems = kit.validate(recipe, sheet, meta, rows)
        if problems:
            failed = True
            sys.stderr.write("%s: %s\n" % (recipe.id, "; ".join(problems)))
        png_path = os.path.join(OUT_DIR, recipe.id + "_sheet.png")
        json_path = os.path.join(OUT_DIR, recipe.id + "_sheet.json")
        data, text = kit.png_bytes(sheet), kit.json_text(meta)
        if args.check:
            for path, content in ((png_path, data), (json_path, text.encode("utf-8"))):
                current = open(path, "rb").read() if os.path.exists(path) else None
                if current is not None and path.endswith(".json"):
                    current = current.replace(b"\r\n", b"\n")   # a Windows checkout may store the JSON with CRLF
                if current != content:
                    failed = True
                    sys.stderr.write("differs: %s\n" % os.path.relpath(path, ROOT))
        elif not args.styles:
            with open(png_path, "wb") as fh:
                fh.write(data)
            with open(json_path, "w", encoding="utf-8", newline="\n") as fh:
                fh.write(text)
            kit.write_meta(png_path, "bat_sheet.png.meta")
            kit.write_meta(json_path, "bat_sheet.json.meta")
        if args.styles:
            print(kit.styles_line(recipe, meta))
        if args.preview:
            os.makedirs(args.preview, exist_ok=True)
            kit.contact(recipe, rows, os.path.join(args.preview, recipe.id + ".png"))
        if not args.styles:
            print("%s: %dx%d, %d rows%s" % (recipe.id, sheet.width, sheet.height, len(rows), " (PROBLEMS)" if problems else ""))
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
