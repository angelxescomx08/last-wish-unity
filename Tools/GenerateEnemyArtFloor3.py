#!/usr/bin/env python3
"""Pixel-art sheets of the floor-3 expansion enemies (deterministic, Pillow only).

Same technique, kit and JSON format as Tools/GenerateEnemyArt.py (it is imported, never changed): shaded primitives with
light from the top-left, 4x4 Bayer dithering, a dark 1 px outline on the body layer, a semi-transparent effects layer,
idle loops, actions that end on idle frame 0, hurt flashing 72/53/23 % towards white and a bottom-up dissolve on death.
The floor-3 theme is a frozen citadel: ice, frost steel, void magic and brass clockwork.

Usage:
  python Tools/GenerateEnemyArtFloor3.py                 writes the 15 sheets (+ .meta if missing) in Assets/Resources/Game/Enemies
  python Tools/GenerateEnemyArtFloor3.py --only bannerman,automaton
  python Tools/GenerateEnemyArtFloor3.py --preview DIR   also writes a labelled x2 contact sheet per enemy in DIR
  python Tools/GenerateEnemyArtFloor3.py --styles        prints the EnemyAnimator.Styles points (chest, eyes, body, claw, shadow)
  python Tools/GenerateEnemyArtFloor3.py --check         regenerates in memory and compares with the files (exit 1 on difference)
Exit codes: 0 ok, 1 differences or validation failure, 2 bad usage.
"""
import argparse
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import GenerateEnemyArt as kit  # noqa: E402  (the floor-1 generator: primitives, finishing, assembly, validation)
import GenerateEnemyArtFloor2 as floor2  # noqa: E402  (only to refuse its ids)
from GenerateEnemyArt import (Recipe, ellipse, capsule, curve, polygon, line, spot, halo, ring, sparks, ramp, mix,  # noqa: E402
                              bands, BONE, WOOD, STEEL)

ROOT, OUT_DIR = kit.ROOT, kit.OUT_DIR
OTHER_FLOORS = {r.id for r in kit.RECIPES} | {r.id for r in floor2.RECIPES}

# ------------------------------------------------------------------ palettes
ICE = ramp((20, 40, 66), (40, 78, 112), (70, 124, 160), (116, 176, 206), (170, 220, 240), (226, 248, 255))
FROSTSTEEL = ramp((22, 30, 44), (44, 58, 80), (72, 92, 120), (110, 134, 164), (158, 182, 208), (210, 226, 242))
SNOWFUR = ramp((70, 74, 86), (122, 128, 140), (170, 176, 186), (210, 214, 222), (238, 240, 246))
NAVY = ramp((10, 16, 40), (20, 34, 76), (36, 56, 116), (60, 86, 156), (96, 124, 190))
BANNER = ramp((60, 14, 22), (110, 26, 36), (164, 44, 52), (206, 76, 76), (236, 128, 116))
BRASS = ramp((60, 36, 14), (110, 72, 28), (166, 116, 46), (214, 166, 78), (246, 214, 136))
COPPER = ramp((50, 22, 14), (100, 48, 28), (156, 82, 46), (204, 124, 76), (232, 170, 120))
HIDE = ramp((34, 22, 16), (66, 44, 30), (104, 72, 48), (140, 104, 72), (176, 140, 104))
VOIDROBE = ramp((8, 4, 16), (20, 10, 36), (38, 20, 64), (60, 34, 96), (88, 56, 130))
VOIDCORE = ramp((10, 2, 20), (40, 10, 70), (100, 40, 160), (170, 110, 230))
BARBSKIN = ramp((80, 60, 58), (140, 110, 100), (196, 164, 146), (230, 206, 186))
WENDISKIN = ramp((24, 24, 28), (50, 48, 54), (84, 80, 86), (122, 116, 118), (164, 158, 154))
DRAGON = ramp((26, 40, 64), (48, 76, 108), (84, 126, 160), (132, 178, 206), (186, 224, 240), (236, 250, 255))
DRAGON_BELLY = ramp((70, 80, 100), (130, 146, 170), (190, 204, 222), (232, 240, 248))
CLOCKFACE = ramp((120, 110, 84), (180, 170, 140), (222, 214, 188), (246, 240, 222))
DARKWOOD = ramp((20, 12, 8), (44, 28, 18), (72, 48, 30), (104, 72, 46), (138, 100, 66))
LICHROBE = ramp((10, 18, 34), (20, 36, 62), (34, 60, 96), (56, 92, 136), (90, 132, 176))
SKULL = ramp((86, 92, 104), (146, 154, 166), (200, 208, 218), (232, 238, 246), (250, 252, 255))
SAND = ramp((120, 84, 30), (186, 140, 60), (230, 196, 110), (250, 232, 170))
WISP = ramp((60, 110, 150), (110, 170, 210), (170, 220, 245), (220, 246, 255), (248, 254, 255))
FROST = (170, 230, 255)
FROST_CORE = (235, 250, 255)
VOID = (180, 90, 255)
VOID_CORE = (240, 210, 255)
EMBER = (255, 180, 80)
RED_EYE = (255, 70, 60)
ICE_EYE = (140, 230, 255)
WHITE = (250, 250, 255)
INK = (12, 10, 16)


def tri(f, a, b, c, rmp, flat=None):
    polygon(f, [a, b, c], rmp, flat=flat)


def snowflakes(f, cx, cy, w, h, n, t, seed, alpha=150):
    """Falling motes of snow on the effects layer."""
    for k in range(n):
        x = cx + (kit.hash01(k, 1, seed) - 0.5) * w
        y = cy + ((kit.hash01(k, 2, seed) + t) % 1.0 - 0.5) * h
        f.glow(x, y, WHITE, alpha)


def flag(f, x0, y0, w, h, t, rmp, emblem=None, wave=1.0):
    """A waving banner hanging to the left of its pole at (x0, y0)."""
    pts_top, pts_bot = [], []
    for k in range(7):
        u = k / 6
        dy = math.sin(math.tau * (t + u * 0.7)) * 2.2 * u * wave
        pts_top.append((x0 - u * w, y0 + dy))
        pts_bot.append((x0 - u * w, y0 + h + dy + (u * 2 if k % 2 else 0)))
    polygon(f, pts_top + pts_bot[::-1], rmp, tex=0.08, seed=7, mod=bands(5, 0.18, "x", t * 10))
    if emblem:
        ex, ey = x0 - w * 0.5, y0 + h * 0.45 + math.sin(math.tau * (t + 0.35)) * 1.6 * wave
        ellipse(f, ex, ey, 3.2, 3.2, emblem)


# ------------------------------------------------------------------ normals
class Bannerman(Recipe):
    id = "bannerman"; cell = (144, 112); anchor = (96, 106); seed = 61
    anims = [("idle", 12, [], "idle"), ("attack", 10, [4], "action"), ("charge", 11, [3, 6], "action"), ("cast", 10, [], "action"),
             ("hurt", 6, [], "hurt"), ("death", 14, [], "death")]
    moves = {"pole_strike": "attack", "banner_charge": "charge", "raise_banner": "cast", "vengeance": "cast"}
    points = {"chest": (-2, -34), "eyes": (-8, -52), "body": (0, -32), "claw": (-34, -40)}

    def draw(self, f, p):
        ox, oy, b, ph = p["ox"], p["oy"], p["b"], p["t"]
        a = p["anim"]
        lunge = (-p["s"] * 10 + p["w"] * 3) if a in ("attack", "charge") else 0.0
        lift = p["s"] if a == "cast" else 0.0
        cx, cy = ox + 4 + lunge * 0.5, oy - 32 + b * 0.5
        # legs and boots
        capsule(f, cx + 5, cy + 12, cx + 6, oy - 3, 3.4, 3, FROSTSTEEL, tex=0.08, seed=1)
        capsule(f, cx - 5 + lunge * 0.3, cy + 12, cx - 7 + lunge * 0.4, oy - 3, 3.4, 3, FROSTSTEEL, tex=0.08, seed=2)
        ellipse(f, cx + 7, oy - 2, 5, 2.4, FROSTSTEEL); ellipse(f, cx - 8 + lunge * 0.4, oy - 2, 5, 2.4, FROSTSTEEL)
        # back arm and the pole of the pendón
        pole_x = cx + 13 + lunge * 0.2
        top = cy - 58 - lift * 6
        if a in ("attack", "charge"):
            ang = math.radians(-90 - 70 * p["s"] + 20 * p["w"])
            bx, by = cx - 4, cy - 6
            tx, ty = bx + math.cos(ang) * 58, by + math.sin(ang) * 58
            capsule(f, cx + 6, cy - 14, bx, by, 2.6, 2.4, FROSTSTEEL)
            line(f, bx - (tx - bx) * 0.25, by - (ty - by) * 0.25, tx, ty, (150, 110, 64))
            line(f, bx - (tx - bx) * 0.25 + 1, by - (ty - by) * 0.25, tx + 1, ty, (96, 66, 36))
            tri(f, (tx - 3, ty), (tx + math.cos(ang) * 8, ty + math.sin(ang) * 8), (tx + 3, ty), STEEL)
            flag(f, tx + 1, ty + 4, 18, 13, ph + p["k"], BANNER, emblem=ICE, wave=1.6)
            if p["s"] > 0.6:
                ring(f, bx, by, 50, (220, 240, 255), 170 * p["s"], arc=(ang - 0.5, ang + 0.3))
        else:
            capsule(f, cx + 6, cy - 14, pole_x, cy - 6 - lift * 8, 2.6, 2.4, FROSTSTEEL)
            line(f, pole_x, top, pole_x, oy - 2, (150, 110, 64))
            line(f, pole_x + 1, top, pole_x + 1, oy - 2, (96, 66, 36))
            tri(f, (pole_x - 2, top), (pole_x + 0.5, top - 7), (pole_x + 3, top), STEEL)
            flag(f, pole_x - 1, top + 3, 22, 15, ph, BANNER, emblem=ICE, wave=1.0 + lift)
        # torso: plate and a navy tabard
        ellipse(f, cx, cy - 6, 11, 13, FROSTSTEEL, tex=0.1, seed=3)
        polygon(f, [(cx - 7, cy - 10), (cx + 7, cy - 10), (cx + 8, cy + 14), (cx, cy + 17), (cx - 8, cy + 14)], NAVY, tex=0.1, seed=4)
        ellipse(f, cx, cy + 2, 3, 3, ICE)
        polygon(f, [(cx - 14, cy - 16), (cx - 5, cy - 18), (cx - 5, cy - 10), (cx - 13, cy - 9)], FROSTSTEEL)
        polygon(f, [(cx + 5, cy - 18), (cx + 14, cy - 16), (cx + 13, cy - 9), (cx + 5, cy - 10)], FROSTSTEEL)
        # helm with a white plume
        hx, hy = cx - 2, cy - 24
        ellipse(f, hx, hy, 7, 7.5, FROSTSTEEL, tex=0.06, seed=5)
        line(f, hx - 6, hy, hx + 2, hy, (20, 30, 50))
        f.put(hx - 4, hy, ICE_EYE); f.put(hx - 1, hy, ICE_EYE)
        curve(f, [(hx + 1, hy - 7), (hx + 6, hy - 11), (hx + 12, hy - 9 + b), (hx + 15, hy - 4 + b)], [2.2, 2.6, 2.2, 1.4], SNOWFUR)
        # front fist
        if a not in ("attack", "charge"):
            capsule(f, cx - 7, cy - 14, cx - 12, cy - 2 + math.sin(math.tau * ph), 2.6, 2.4, FROSTSTEEL)
        if lift > 0:
            halo(f, pole_x - 10, top + 10, 12 + lift * 6, (255, 200, 150), 140 * lift)
            for k in range(6):
                t = ph * math.tau + k
                f.glow(pole_x - 20 + math.cos(t) * 16, top + 10 + math.sin(t) * 8, WHITE, 200 * lift)


class Barbarian(Recipe):
    id = "barbarian"; cell = (160, 116); anchor = (108, 110); seed = 62
    anims = [("idle", 12, [], "idle"), ("attack", 10, [5], "action"), ("whirl", 12, [3, 6, 9], "action"), ("cast", 11, [], "action"),
             ("hurt", 6, [], "hurt"), ("death", 14, [], "death")]
    moves = {"axe_chop": "attack", "whirlwind": "whirl", "war_cry": "cast", "vengeance": "cast"}
    points = {"chest": (-4, -40), "eyes": (-10, -62), "body": (0, -38), "claw": (-44, -40)}

    def draw(self, f, p):
        ox, oy, b, ph = p["ox"], p["oy"], p["b"], p["t"]
        a = p["anim"]
        lunge = (-p["s"] * 8 + p["w"] * 3) if a == "attack" else 0.0
        spin = p["k"] * math.tau * 2 if a == "whirl" and p.get("i", 0) < p.get("n", 1) - 1 else 0.0
        roar = p["s"] if a == "cast" else 0.0
        cx, cy = ox + lunge * 0.5, oy - 40 + b * 0.8
        # legs in fur boots
        capsule(f, cx + 7, cy + 16, cx + 9, oy - 4, 5, 4.4, HIDE, tex=0.1, seed=1)
        capsule(f, cx - 7 + lunge * 0.3, cy + 16, cx - 10 + lunge * 0.4, oy - 4, 5, 4.4, HIDE, tex=0.1, seed=2)
        ellipse(f, cx + 10, oy - 3, 7, 3.4, SNOWFUR, tex=0.15, seed=3); ellipse(f, cx - 11 + lunge * 0.4, oy - 3, 7, 3.4, SNOWFUR, tex=0.15, seed=4)
        # fur cloak behind
        polygon(f, [(cx - 8, cy - 22), (cx + 20, cy - 24), (cx + 26, cy + 18), (cx + 6, cy + 24)], SNOWFUR, tex=0.2, seed=5, mod=bands(4, 0.2, "y", ph * 4))
        # bare chest with blue war paint
        ellipse(f, cx, cy - 4, 16, 18, BARBSKIN, tex=0.06, seed=6)
        for k in range(3):
            line(f, cx - 8 + k * 6, cy - 12, cx - 6 + k * 6, cy - 2, (60, 120, 210))
        polygon(f, [(cx - 14, cy + 8), (cx + 14, cy + 8), (cx + 15, cy + 18), (cx - 15, cy + 18)], HIDE, tex=0.12, seed=7)
        line(f, cx - 15, cy + 9, cx + 14, cy + 9, (200, 170, 90))
        # head: beard, horned helm
        hx, hy = cx - 4, cy - 26
        ellipse(f, hx, hy, 7, 7.5, BARBSKIN)
        polygon(f, [(hx - 7, hy + 1), (hx + 5, hy + 1), (hx + 2, hy + 12), (hx - 4, hy + 13)], ramp((120, 60, 30), (170, 96, 46), (210, 140, 70)), tex=0.12, seed=8)
        ellipse(f, hx, hy - 5, 8, 5, FROSTSTEEL, tex=0.06, seed=9)
        curve(f, [(hx - 7, hy - 6), (hx - 13, hy - 10), (hx - 14, hy - 17)], [2.4, 1.8, 0.8], BONE)
        curve(f, [(hx + 7, hy - 6), (hx + 13, hy - 10), (hx + 14, hy - 17)], [2.4, 1.8, 0.8], BONE)
        eye_c = mix(ICE_EYE, RED_EYE, min(1.0, roar + (0.6 if a == "whirl" else 0.0)))
        f.put(hx - 4, hy - 1, eye_c); f.put(hx - 1, hy - 1, eye_c)
        if roar > 0:
            polygon(f, [(hx - 5, hy + 4), (hx - 1, hy + 4), (hx - 2, hy + 7 + roar * 2), (hx - 5, hy + 7 + roar * 2)], ramp((40, 6, 8), (90, 20, 20)))
        # arms and the great axe
        if a == "whirl":
            ang = math.pi + spin
        elif a == "attack":
            ang = math.radians(250 - 160 * p["s"] + 40 * p["w"]) if p.get("i", 0) >= 1 else math.radians(250)
        else:
            ang = math.radians(240 + math.sin(math.tau * ph) * 4 - roar * 40)
        shx, shy = cx - 10, cy - 14
        hx2, hy2 = shx + math.cos(ang) * 12, shy + math.sin(ang) * 12
        capsule(f, shx, shy, hx2, hy2, 4.2, 3.6, BARBSKIN, tex=0.06, seed=10)
        capsule(f, cx + 10, cy - 14, cx + 13, cy - 2 - roar * 10, 4.2, 3.6, BARBSKIN, tex=0.06, seed=11)
        ex, ey = hx2 + math.cos(ang) * 30, hy2 + math.sin(ang) * 30
        line(f, hx2 - math.cos(ang) * 6, hy2 - math.sin(ang) * 6, ex, ey, (120, 84, 50))
        line(f, hx2 - math.cos(ang) * 6 + 1, hy2 - math.sin(ang) * 6, ex + 1, ey, (80, 54, 32))
        nx, ny = -math.sin(ang), math.cos(ang)
        polygon(f, [(ex - math.cos(ang) * 2, ey - math.sin(ang) * 2), (ex + nx * 13 - math.cos(ang) * 6, ey + ny * 13 - math.sin(ang) * 6),
                    (ex + nx * 14 + math.cos(ang) * 6, ey + ny * 14 + math.sin(ang) * 6), (ex + math.cos(ang) * 4, ey + math.sin(ang) * 4)], ICE, tex=0.1, seed=12)
        line(f, ex + nx * 13 - math.cos(ang) * 5, ey + ny * 13 - math.sin(ang) * 5, ex + nx * 14 + math.cos(ang) * 5, ey + ny * 14 + math.sin(ang) * 5, FROST_CORE)
        if a == "attack" and p.get("i", 0) in (5, 6):
            sparks(f, ex + nx * 12, oy - 6, 20, 22, FROST_CORE, 220, 62 + p["i"], 1)
        if a == "whirl" and p["s"] > 0.3:
            ring(f, shx, shy, 40, FROST, 160 * p["s"], width=2.0)
        if roar > 0:
            for r in (14, 22, 30):
                ring(f, hx - 6, hy + 4, r * (0.5 + roar * 0.6), (255, 120, 90), 150 * roar, arc=(math.pi * 0.6, math.pi * 1.4))


class VoidMage(Recipe):
    id = "voidmage"; cell = (144, 108); anchor = (96, 102); seed = 63
    anims = [("idle", 12, [], "idle"), ("attack", 10, [5], "action"), ("drain", 11, [5], "action"), ("cast", 10, [], "action"),
             ("hurt", 6, [], "hurt"), ("death", 14, [], "death")]
    moves = {"void_orb": "attack", "mana_drain": "drain", "arcane_barrier": "cast", "vengeance": "cast"}
    points = {"chest": (-2, -36), "eyes": (-6, -56), "body": (0, -34), "claw": (-34, -44)}

    def draw(self, f, p):
        ox, oy, b, ph = p["ox"], p["oy"], p["b"], p["t"]
        a = p["anim"]
        power = p["s"] if a in ("drain", "cast") else 0.0
        fl = -3 + b * 1.6
        cx, cy = ox + 2, oy - 34 + fl
        hem = [(cx - 16 + k * 4.6, oy - 4 + fl * 0.4 + (3 if k % 2 else 0) + math.sin(math.tau * ph + k) * 1.2) for k in range(8)]
        polygon(f, [(cx - 9, cy - 14), (cx + 9, cy - 14), (cx + 17, hem[-1][1])] + hem[::-1] + [(cx - 17, hem[0][1])], VOIDROBE, tex=0.12, seed=1,
                mod=bands(6, 0.22, "x", ph * 6))
        for side in (-1, 1):
            line(f, cx + side * 9, cy - 12, cx + side * 15, oy - 6 + fl * 0.4, (140, 90, 220))
        # hood with void face and two pale eyes
        hx, hy = cx - 2, cy - 22
        ellipse(f, hx + 1, hy, 9, 9.5, VOIDROBE, tex=0.06, seed=2)
        polygon(f, [(hx + 1, hy - 9), (hx + 8, hy - 16), (hx + 6, hy - 6)], VOIDROBE)
        ellipse(f, hx - 2, hy + 1, 5, 5.5, ramp((4, 2, 8), (10, 4, 18)))
        f.put(hx - 4, hy, VOID_CORE); f.put(hx - 1, hy, VOID_CORE)
        halo(f, hx - 2.5, hy, 3.5, VOID, 90 + 90 * power)
        # floating void orb in front
        if a == "attack":
            if p.get("i", 0) < 5:
                orx, ory = cx - 18 - p["w"] * 4, cy - 14 - p["w"] * 6
            else:
                d = min(1.0, (p["i"] - 5) / 3)
                orx, ory = cx - 22 - d * 60, cy - 18 + d * 6
        else:
            orx, ory = cx - 18, cy - 10 + math.sin(math.tau * ph) * 2.5
        capsule(f, cx - 6, cy - 10, cx - 12, ory + 2, 2.6, 2.2, VOIDROBE)
        ellipse(f, orx, ory, 4.5, 4.5, VOIDCORE)
        halo(f, orx, ory, 6 + power * 4, VOID, 120 + 80 * power, core=VOID_CORE)
        for k in range(3):
            t = ph * math.tau * 2 + k * 2.1
            f.glow(orx + math.cos(t) * 7, ory + math.sin(t) * 4, VOID_CORE, 170)
        if a == "drain" and p["s"] > 0.2:
            for k in range(9):
                t = (k / 9 + p["k"]) % 1.0
                x = orx - 70 + t * 66
                f.glow(x, ory + math.sin(t * 9) * 4, (120, 200, 255), 220 * p["s"])
                f.glow(x + 1, ory + math.sin(t * 9) * 4, VOID_CORE, 120 * p["s"])
        if a == "cast":
            for k in range(6):   # hexagonal barrier
                t0, t1 = k / 6 * math.tau, (k + 1) / 6 * math.tau
                r = 24 + power * 6
                line(f, cx - 4 + math.cos(t0) * r, cy - 4 + math.sin(t0) * r, cx - 4 + math.cos(t1) * r, cy - 4 + math.sin(t1) * r, VOID, 200 * power)
            halo(f, cx - 4, cy - 4, 20 * power, VOID, 50 * power)


class FrostHunter(Recipe):
    id = "frosthunter"; cell = (144, 108); anchor = (96, 102); seed = 64
    anims = [("idle", 12, [], "idle"), ("attack", 11, [5], "action"), ("volley", 12, [3, 6, 9], "action"), ("cast", 10, [], "action"),
             ("hurt", 6, [], "hurt"), ("death", 14, [], "death")]
    moves = {"ice_javelin": "attack", "bolt_volley": "volley", "snow_stalk": "cast", "vengeance": "cast"}
    points = {"chest": (-2, -32), "eyes": (-8, -50), "body": (0, -30), "claw": (-36, -38)}

    def draw(self, f, p):
        ox, oy, b, ph = p["ox"], p["oy"], p["b"], p["t"]
        a = p["anim"]
        crouch = p["s"] * 6 if a == "cast" else 0.0
        throw = p["w"] if a == "attack" else 0.0
        cx, cy = ox + 2, oy - 30 + b * 0.5 + crouch
        capsule(f, cx + 5, cy + 10, cx + 7, oy - 3, 3.4, 3, HIDE, tex=0.1, seed=1)
        capsule(f, cx - 5, cy + 10, cx - 8, oy - 3, 3.4, 3, HIDE, tex=0.1, seed=2)
        ellipse(f, cx + 8, oy - 2, 5.5, 2.6, SNOWFUR); ellipse(f, cx - 9, oy - 2, 5.5, 2.6, SNOWFUR)
        # quiver of ice javelins on the back
        for k in range(3):
            line(f, cx + 8 + k * 2, cy - 22 - k, cx + 4 + k * 2, cy + 6, (120, 190, 230))
        polygon(f, [(cx + 4, cy - 10), (cx + 12, cy - 12), (cx + 10, cy + 8), (cx + 3, cy + 8)], HIDE)
        # fur parka
        ellipse(f, cx, cy - 4, 11, 14, SNOWFUR, tex=0.18, seed=3, mod=bands(5, 0.15, "y", ph * 3))
        polygon(f, [(cx - 10, cy + 2), (cx + 10, cy + 2), (cx + 12, cy + 12), (cx - 12, cy + 12)], HIDE, tex=0.1, seed=4)
        # hood with a dark face and goggles
        hx, hy = cx - 3, cy - 20
        ellipse(f, hx + 1, hy, 8, 8, SNOWFUR, tex=0.15, seed=5)
        ellipse(f, hx - 2, hy + 1, 4.5, 5, ramp((40, 30, 30), (90, 66, 56)))
        line(f, hx - 6, hy, hx + 1, hy, (40, 60, 80))
        f.put(hx - 5, hy, ICE_EYE); f.put(hx - 2, hy, ICE_EYE)
        # arms: javelin (attack) or crossbow
        if a == "attack":
            if p.get("i", 0) < 5:
                jx, jy = cx + 4 + throw * 8, cy - 18 - throw * 4
                capsule(f, cx - 6, cy - 12, jx, jy + 2, 2.4, 2.2, SNOWFUR)
                line(f, jx + 10, jy - 2, jx - 20, jy + 2, (200, 240, 255))
                tri(f, (jx - 20, jy), (jx - 27, jy + 2.5), (jx - 20, jy + 4), ICE)
            else:
                d = min(1.0, (p["i"] - 5) / 3)
                x = cx - 30 - d * 64
                line(f, x + 26, cy - 18, x, cy - 16, (220, 250, 255))
                tri(f, (x, cy - 18), (x - 8, cy - 15.5), (x, cy - 14), ICE)
                halo(f, x, cy - 16, 6, FROST, 200 * (1 - d * 0.5))
                capsule(f, cx - 6, cy - 12, cx - 18, cy - 14, 2.4, 2.2, SNOWFUR)
        else:
            bx, by = cx - 16, cy - 10 + math.sin(math.tau * ph)
            capsule(f, cx - 6, cy - 12, bx + 4, by, 2.4, 2.2, SNOWFUR)
            polygon(f, [(bx - 8, by - 1), (bx + 6, by - 1), (bx + 6, by + 2), (bx - 8, by + 2)], WOOD)
            curve(f, [(bx - 6, by - 8), (bx - 9, by), (bx - 6, by + 8)], [1.2, 1.4, 1.2], ramp((60, 50, 40), (110, 90, 70)))
            line(f, bx - 6, by - 8, bx - 6, by + 8, (200, 200, 210))
            if a == "volley":
                for st in (3, 6, 9):
                    d = p.get("i", 0) - st
                    if 0 <= d <= 2:
                        x = bx - 12 - d * 26
                        line(f, x, by, x + 10, by, (220, 250, 255), 230)
                        halo(f, x, by, 4, FROST, 180)
        if a == "cast":
            snowflakes(f, cx, cy - 10, 60, 50, 14, p["k"], 64, 200 * p["s"])
            halo(f, cx, cy, 20, (220, 240, 255), 60 * p["s"])


class FrostWisp(Recipe):
    id = "frostwisp"; cell = (128, 100); anchor = (84, 94); seed = 65
    anims = [("idle", 12, [], "idle"), ("attack", 10, [4], "action"), ("icicles", 11, [3, 6], "action"), ("cast", 10, [], "action"),
             ("hurt", 6, [], "hurt"), ("death", 12, [], "death")]
    moves = {"chill_touch": "attack", "icicles": "icicles", "frost_veil": "cast", "vengeance": "cast"}
    points = {"chest": (0, -38), "eyes": (-6, -46), "body": (0, -36), "claw": (-28, -36)}

    def draw(self, f, p):
        ox, oy, b, ph = p["ox"], p["oy"], p["b"], p["t"]
        a = p["anim"]
        reach = (-p["s"] * 12 + p["w"] * 3) if a == "attack" else 0.0
        cx, cy = ox + reach * 0.6, oy - 38 + b * 3
        # tail of mist down to the floor (opaque, pale)
        tail = [(cx + 2, cy + 6), (cx + 6 + math.sin(math.tau * ph) * 2, cy + 16), (cx + 2 + math.sin(math.tau * ph + 1) * 3, cy + 26), (cx + 6, oy - 10)]
        curve(f, tail, [7, 5.5, 3.6, 1.6], WISP, tex=0.06, seed=1)
        # crystalline body
        ellipse(f, cx, cy - 2, 10, 11, WISP, tex=0.05, seed=2)
        for k in range(5):   # crown of ice spikes
            ang = math.radians(-150 + k * 30)
            x0, y0 = cx + math.cos(ang) * 8, cy - 4 + math.sin(ang) * 9
            x1, y1 = cx + math.cos(ang) * (16 + (k % 2) * 4), cy - 4 + math.sin(ang) * (18 + (k % 2) * 4)
            tri(f, (x0 - 2, y0), (x1, y1), (x0 + 2, y0 + 1), ICE)
        # face: two deep eyes and a small mouth
        f.put(cx - 5, cy - 3, (20, 60, 100)); f.put(cx - 4, cy - 3, (20, 60, 100))
        f.put(cx - 1, cy - 3, (20, 60, 100)); f.put(cx, cy - 3, (20, 60, 100))
        f.put(cx - 3, cy + 2, (60, 110, 150))
        halo(f, cx - 2, cy - 3, 4, FROST, 80)
        # long wispy arms
        if a == "attack":
            hx, hy = cx - 18 - p["s"] * 8, cy - 2
        elif a == "icicles":
            hx, hy = cx - 16, cy - 10 - p["s"] * 4
        else:
            hx, hy = cx - 14, cy + 4 + math.sin(math.tau * ph) * 2
        curve(f, [(cx - 6, cy), ((cx + hx) / 2, (cy + hy) / 2 + 2), (hx, hy)], [2.6, 2.0, 1.4], WISP)
        curve(f, [(cx + 7, cy), (cx + 13, cy + 4), (cx + 15, cy + 9 + b)], [2.6, 2.0, 1.4], WISP)
        if a == "attack" and p["s"] > 0.4:
            halo(f, hx - 4, hy, 8, FROST, 220 * p["s"], core=FROST_CORE)
            sparks(f, hx - 6, hy, 14, 14, WHITE, 200, 65 + p.get("i", 0), 1)
        if a == "icicles":
            for st in (3, 6):
                d = p.get("i", 0) - st
                if 0 <= d <= 2:
                    x = hx - 8 - d * 28
                    tri(f, (x + 6, hy - 2 + st), (x - 6, hy + st), (x + 6, hy + 2 + st), ICE)
        if a == "cast":
            for r in (12, 20, 28):
                ring(f, cx, cy, r * (0.5 + p["s"] * 0.6), FROST, 140 * p["s"])
            snowflakes(f, cx, cy, 50, 40, 10, p["k"], 65, 210 * p["s"])


class Tinkerer(Recipe):
    id = "tinkerer"; cell = (144, 104); anchor = (96, 98); seed = 66
    anims = [("idle", 12, [], "idle"), ("attack", 10, [4], "action"), ("spark", 10, [5], "action"), ("build", 12, [7], "action"),
             ("cast", 10, [], "action"), ("hurt", 6, [], "hurt"), ("death", 14, [], "death")]
    moves = {"wrench": "attack", "spark": "spark", "assemble": "build", "tune_up": "cast", "vengeance": "cast"}
    points = {"chest": (-2, -24), "eyes": (-8, -38), "body": (0, -22), "claw": (-30, -26)}

    def draw(self, f, p):
        ox, oy, b, ph = p["ox"], p["oy"], p["b"], p["t"]
        a = p["anim"]
        cx, cy = ox + 2, oy - 22 + b * 0.6
        capsule(f, cx + 4, cy + 8, cx + 5, oy - 3, 2.6, 2.4, HIDE)
        capsule(f, cx - 4, cy + 8, cx - 5, oy - 3, 2.6, 2.4, HIDE)
        ellipse(f, cx + 6, oy - 2, 4.4, 2.2, DARKWOOD); ellipse(f, cx - 6, oy - 2, 4.4, 2.2, DARKWOOD)
        # backpack of gears and a chimney
        polygon(f, [(cx + 6, cy - 16), (cx + 20, cy - 16), (cx + 20, cy + 4), (cx + 6, cy + 4)], COPPER, tex=0.1, seed=1)
        for (gx, gy, r) in ((cx + 14, cy - 10, 5), (cx + 10, cy - 1, 3.6)):
            ellipse(f, gx, gy, r, r, BRASS)
            for k in range(6):
                t = k / 6 * math.tau + ph * math.tau * (1 if r > 4 else -1)
                spot(f, gx + math.cos(t) * (r + 1), gy + math.sin(t) * (r + 1), (200, 150, 70))
            f.put(gx, gy, (60, 36, 14))
        line(f, cx + 17, cy - 16, cx + 17, cy - 24, (70, 60, 56)); line(f, cx + 18, cy - 16, cx + 18, cy - 24, (50, 44, 40))
        halo(f, cx + 17, cy - 27 - (ph * 6) % 4, 3, (200, 200, 210), 90)
        # stout body with a leather apron
        ellipse(f, cx, cy - 2, 10, 11, NAVY, tex=0.1, seed=2)
        polygon(f, [(cx - 7, cy - 6), (cx + 6, cy - 6), (cx + 8, cy + 10), (cx - 9, cy + 10)], HIDE, tex=0.12, seed=3)
        # big head, goggles, bushy beard
        hx, hy = cx - 3, cy - 16
        ellipse(f, hx, hy, 7.5, 7, BARBSKIN)
        polygon(f, [(hx - 7, hy + 2), (hx + 5, hy + 2), (hx + 2, hy + 9), (hx - 5, hy + 9)], SNOWFUR, tex=0.15, seed=4)
        ellipse(f, hx + 1, hy - 5, 7.5, 3.5, HIDE)
        ellipse(f, hx - 4, hy - 1, 2.6, 2.6, BRASS); ellipse(f, hx + 1, hy - 1, 2.6, 2.6, BRASS)
        f.put(hx - 4, hy - 1, ICE_EYE); f.put(hx + 1, hy - 1, ICE_EYE)
        f.put(hx - 6, hy + 1, (200, 120, 110))   # red nose
        # arm with the big wrench
        if a == "attack":
            ang = math.radians(260 - 150 * p["s"] + 30 * p["w"])
        elif a == "build":
            ang = math.radians(200 + 30 * math.sin(p["k"] * math.tau * 3))
        else:
            ang = math.radians(225 + math.sin(math.tau * ph) * 4)
        shx, shy = cx - 7, cy - 6
        wx, wy = shx + math.cos(ang) * 8, shy + math.sin(ang) * 8
        capsule(f, shx, shy, wx, wy, 2.4, 2.2, NAVY)
        ex, ey = wx + math.cos(ang) * 16, wy + math.sin(ang) * 16
        capsule(f, wx, wy, ex, ey, 1.6, 1.6, STEEL)
        nx, ny = -math.sin(ang), math.cos(ang)
        polygon(f, [(ex + nx * 4, ey + ny * 4), (ex + nx * 4 + math.cos(ang) * 5, ey + ny * 4 + math.sin(ang) * 5),
                    (ex - nx * 4 + math.cos(ang) * 5, ey - ny * 4 + math.sin(ang) * 5), (ex - nx * 4, ey - ny * 4)], STEEL)
        if a == "attack" and p.get("i", 0) in (4, 5):
            sparks(f, ex, ey, 16, 14, (255, 220, 140), 220, 66 + p["i"], 1)
        if a == "spark":
            hx2, hy2 = cx + 12, cy - 8
            capsule(f, cx + 6, cy - 6, hx2, hy2, 2.2, 2, NAVY)
            if p["s"] > 0.3:
                pts = [(cx - 10, cy - 10)]
                for k in range(6):
                    pts.append((cx - 16 - k * 9, cy - 10 + (5 if k % 2 else -5) * (0.5 + kit.hash01(k, p.get("i", 0), 66))))
                for k in range(len(pts) - 1):
                    line(f, pts[k][0], pts[k][1], pts[k + 1][0], pts[k + 1][1], (200, 240, 255), 240 * p["s"])
                halo(f, pts[-1][0], pts[-1][1], 6, (160, 220, 255), 200 * p["s"])
        else:
            capsule(f, cx + 6, cy - 6, cx + 9, cy + 2, 2.2, 2, NAVY)
        if a == "build" and p["s"] > 0.1:
            for k in range(4):
                t = p["k"] * 3 + k * 0.25
                f.glow(cx - 34 + math.cos(t * 7) * 6, oy - 8 - (t % 1.0) * 14, (255, 220, 140), 220)
            ring(f, cx - 36, oy - 4, 6 + 10 * p["s"], (255, 210, 120), 180 * p["s"])
        if a == "cast":
            for k in range(5):
                t = ph * math.tau + k * 1.25
                halo(f, cx + math.cos(t) * 20, cy - 6 + math.sin(t) * 8, 3, (255, 210, 120), 190 * p["s"])


# ------------------------------------------------------------------ minions
class Automaton(Recipe):
    id = "automaton"; cell = (128, 100); anchor = (84, 94); seed = 67
    anims = [("idle", 12, [], "idle"), ("attack", 9, [4], "action"), ("cast", 10, [], "action"), ("hurt", 6, [], "hurt"),
             ("death", 12, [], "death"), ("spawn", 8, [], "spawn")]
    moves = {"bolt_shot": "attack", "wind_up": "cast", "vengeance": "cast"}
    points = {"chest": (0, -26), "eyes": (-6, -40), "body": (0, -24), "claw": (-28, -28)}

    def draw(self, f, p):
        ox, oy, b, ph = p["ox"], p["oy"], p["b"], p["t"]
        a = p["anim"]
        tick = 1 if int(ph * 12) % 2 else 0
        recoil = p["s"] * 3 if a == "attack" else 0.0
        cx, cy = ox + recoil, oy - 24 + tick * 0.5
        # stiff legs
        for lx in (-4, 4):
            capsule(f, cx + lx, cy + 8, cx + lx, oy - 3, 1.8, 1.6, BRASS)
            ellipse(f, cx + lx - 1, oy - 2, 3.4, 1.8, COPPER)
        # barrel body with rivets
        ellipse(f, cx, cy - 2, 9, 11, BRASS, tex=0.08, seed=1, mod=bands(6, 0.2, "y", 0))
        for k in range(4):
            f.put(cx - 7 + k * 4, cy - 2, (90, 56, 20))
        ellipse(f, cx, cy + 1, 3, 3, CLOCKFACE)
        line(f, cx, cy + 1, cx + math.cos(ph * math.tau) * 2.4, cy + 1 + math.sin(ph * math.tau) * 2.4, INK)
        # wind-up key on the back (turns)
        kang = (ph * math.tau if a != "cast" else p["k"] * math.tau * 3)
        kx, ky = cx + 10, cy - 4
        line(f, cx + 8, cy - 4, kx, ky, (110, 72, 28))
        for s in (-1, 1):
            ellipse(f, kx + 2, ky + s * 3.2 * math.cos(kang), 2, 2.6 * abs(math.cos(kang)) + 0.6, BRASS)
        # head: a dome with a single glass eye
        hx, hy = cx - 1, cy - 15
        ellipse(f, hx, hy, 6, 5.5, BRASS, tex=0.05, seed=2)
        ellipse(f, hx - 2, hy, 2.4, 2.4, ramp((30, 60, 90), (80, 160, 220), (190, 240, 255)))
        halo(f, hx - 2, hy, 3, ICE_EYE, 70 + 80 * (p["s"] if a == "attack" else 0))
        line(f, hx, hy - 6, hx, hy - 9, (90, 60, 24)); f.put(hx, hy - 10, (230, 80, 60))
        # crossbow arm
        bx, by = cx - 12, cy - 4
        capsule(f, cx - 6, cy - 4, bx + 2, by, 1.8, 1.6, COPPER)
        polygon(f, [(bx - 8, by - 1.5), (bx + 3, by - 1.5), (bx + 3, by + 1.5), (bx - 8, by + 1.5)], DARKWOOD)
        line(f, bx - 6, by - 6, bx - 6, by + 6, STEEL[2])
        if a == "attack" and p.get("i", 0) < 4:
            line(f, bx - 8, by, bx + 2, by, (230, 240, 255))
        if a == "attack" and p.get("i", 0) >= 4:
            d = min(1.0, (p["i"] - 4) / 3)
            x = bx - 12 - d * 50
            line(f, x, by, x + 10, by, (230, 240, 255), 240)
            halo(f, x, by, 4, (255, 220, 140), 200)
        if a == "cast":
            halo(f, kx + 2, ky, 6 + 4 * p["s"], (255, 210, 120), 180 * p["s"])
            for k in range(3):
                ring(f, kx + 2, ky, 4 + k * 4, (255, 220, 150), 140 * p["s"])


class Squire(Recipe):
    id = "squire"; cell = (128, 100); anchor = (84, 94); seed = 68
    anims = [("idle", 12, [], "idle"), ("attack", 9, [4], "action"), ("cast", 10, [], "action"), ("hurt", 6, [], "hurt"),
             ("death", 12, [], "death"), ("spawn", 8, [], "spawn")]
    moves = {"spear_thrust": "attack", "shield_up": "cast", "vengeance": "cast"}
    points = {"chest": (0, -30), "eyes": (-6, -46), "body": (0, -28), "claw": (-32, -30)}

    def draw(self, f, p):
        ox, oy, b, ph = p["ox"], p["oy"], p["b"], p["t"]
        a = p["anim"]
        thrust = (-p["s"] * 12 + p["w"] * 4) if a == "attack" else 0.0
        guard = p["s"] if a == "cast" else 0.0
        cx, cy = ox + thrust * 0.3, oy - 28 + b * 0.5
        capsule(f, cx + 4, cy + 10, cx + 5, oy - 3, 2.6, 2.4, FROSTSTEEL)
        capsule(f, cx - 4, cy + 10, cx - 5 + thrust * 0.2, oy - 3, 2.6, 2.4, FROSTSTEEL)
        ellipse(f, cx + 6, oy - 2, 4.2, 2, HIDE); ellipse(f, cx - 6 + thrust * 0.2, oy - 2, 4.2, 2, HIDE)
        # spear (back hand, thrusting forward)
        sy = cy - 8
        sx0 = cx + 16 + thrust
        line(f, sx0, sy, sx0 - 40, sy - 2, (150, 110, 64))
        line(f, sx0, sy + 1, sx0 - 40, sy - 1, (96, 66, 36))
        tri(f, (sx0 - 40, sy - 4), (sx0 - 48, sy - 1.5), (sx0 - 40, sy + 2), STEEL)
        if a == "attack" and p["s"] > 0.6:
            line(f, sx0 - 50, sy - 1, sx0 - 66, sy - 1, (230, 245, 255), 220 * p["s"])
        # body: navy tabard over mail
        ellipse(f, cx, cy - 4, 8, 10, FROSTSTEEL, tex=0.1, seed=1)
        polygon(f, [(cx - 6, cy - 8), (cx + 6, cy - 8), (cx + 7, cy + 10), (cx - 7, cy + 10)], NAVY, tex=0.1, seed=2)
        line(f, cx, cy - 6, cx, cy + 8, (200, 170, 90))
        # kettle helm
        hx, hy = cx - 1, cy - 18
        ellipse(f, hx, hy + 1, 5, 5.5, BARBSKIN)
        polygon(f, [(hx - 9, hy - 1), (hx + 9, hy - 1), (hx + 6, hy - 4), (hx + 4, hy - 8), (hx - 4, hy - 8), (hx - 6, hy - 4)], FROSTSTEEL, tex=0.06, seed=3)
        f.put(hx - 3, hy + 1, (40, 50, 70)); f.put(hx, hy + 1, (40, 50, 70))
        # round shield in front
        shx, shy = cx - 10 - guard * 6, cy - 2 - guard * 6
        ellipse(f, shx, shy, 8, 9, NAVY, tex=0.08, seed=4)
        for k in range(16):
            t = k / 16 * math.tau
            f.put(shx + math.cos(t) * 7.5, shy + math.sin(t) * 8.5, (180, 150, 70))
        ellipse(f, shx, shy, 2.4, 2.4, ICE)
        if guard > 0:
            halo(f, shx, shy, 12 + guard * 6, FROST, 150 * guard)


class Phylactery(Recipe):
    id = "phylactery"; cell = (128, 100); anchor = (84, 94); seed = 69
    anims = [("idle", 12, [], "idle"), ("attack", 10, [5], "action"), ("beat", 10, [], "action"), ("cast", 10, [], "action"),
             ("hurt", 6, [], "hurt"), ("death", 12, [], "death"), ("spawn", 8, [], "spawn")]
    moves = {"cold_pulse": "cast", "life_beat": "beat", "vengeance": "attack"}
    points = {"chest": (0, -36), "eyes": (-2, -40), "body": (0, -36), "claw": (-24, -36)}

    def draw(self, f, p):
        ox, oy, b, ph = p["ox"], p["oy"], p["b"], p["t"]
        a = p["anim"]
        beat = p["s"] if a in ("beat", "attack") else 0.0
        cx, cy = ox, oy - 46 + b * 2.5
        # broken chains dangling from its base
        for s in (-1, 1):
            for k in range(3):
                ellipse(f, cx + s * (7 + k * 2), cy + 15 + k * 3.5, 1.4, 1.9, STEEL)
        # ornate reliquary: silver frame with ice crystal panes
        polygon(f, [(cx - 9, cy - 10), (cx + 9, cy - 10), (cx + 11, cy + 10), (cx - 11, cy + 10)], ICE, tex=0.05, seed=1,
                mod=lambda x, y, v: v + (0.3 if (x - y) % 7 in (0, 1) else 0.0))
        for (x0, y0, x1, y1) in ((-9, -10, -11, 10), (9, -10, 11, 10), (-11, 10, 11, 10), (-9, -10, 9, -10), (0, -10, 0, 10)):
            line(f, cx + x0, cy + y0, cx + x1, cy + y1, (200, 210, 224))
        polygon(f, [(cx - 11, cy - 12), (cx + 11, cy - 12), (cx + 7, cy - 18), (cx - 7, cy - 18)], SKULL)
        tri(f, (cx - 3, cy - 18), (cx, cy - 26), (cx + 3, cy - 18), SKULL)
        polygon(f, [(cx - 12, cy + 10), (cx + 12, cy + 10), (cx + 9, cy + 14), (cx - 9, cy + 14)], SKULL)
        # the soul inside: a small blue flame with a face
        sr = 4 + beat * 2 + math.sin(math.tau * ph) * 0.5
        ellipse(f, cx, cy + 1, sr * 0.8, sr, ramp((60, 140, 200), (140, 220, 255), (230, 250, 255)))
        f.put(cx - 2, cy, INK); f.put(cx + 1, cy, INK)
        halo(f, cx, cy, 6 + beat * 6, FROST, 90 + 120 * beat, core=FROST_CORE)
        if a == "cast":
            for r in (14, 22, 30):
                ring(f, cx, cy, r * (0.5 + p["s"] * 0.6), FROST, 150 * p["s"])
        if a == "beat" and p["s"] > 0.2:
            for k in range(6):
                t = k / 6 * math.tau + p["k"] * 2
                halo(f, cx + math.cos(t) * 20 * p["s"], cy + math.sin(t) * 12 * p["s"], 3, (150, 255, 190), 200 * p["s"])
        if a == "attack" and p["s"] > 0.4:
            line(f, cx - 8, cy, cx - 50, cy + 2, FROST, 220 * p["s"])


# ------------------------------------------------------------------ elites
class Commander(Recipe):
    id = "commander"; cell = (208, 120); anchor = (142, 115); boss = True; elite = True; seed = 70
    anims = [("idle", 12, [], "idle"), ("attack", 11, [5], "action"), ("lance", 11, [6], "action"), ("call", 11, [], "action"),
             ("bugle", 11, [], "action"), ("cast", 10, [], "action"), ("hurt", 6, [], "hurt"), ("death", 15, [], "death")]
    moves = {"command_thrust": "attack", "piercing_lance": "lance", "call_guard": "call", "last_charge": "call", "bugle_call": "bugle",
             "close_ranks": "cast", "vengeance": "cast"}
    points = {"chest": (-4, -50), "eyes": (-10, -76), "body": (0, -46), "claw": (-60, -48)}

    def draw(self, f, p):
        ox, oy, b, ph = p["ox"], p["oy"], p["b"], p["t"]
        a = p["anim"]
        step = (-p["s"] * 8 + p["w"] * 3) if a in ("attack", "lance") else 0.0
        cx, cy = ox + 2 + step * 0.5, oy - 48 + b * 0.6
        horn = p["s"] if a == "bugle" else 0.0
        # cape
        polygon(f, [(cx + 6, cy - 24), (cx + 26, cy - 20), (cx + 32, oy - 2), (cx + 6, oy - 2)], BANNER, tex=0.1, seed=1, mod=bands(7, 0.2, "x", ph * 5))
        # back banner pole (sashimono) with a small pendón
        line(f, cx + 12, cy - 60, cx + 12, cy - 10, (150, 110, 64)); line(f, cx + 13, cy - 60, cx + 13, cy - 10, (96, 66, 36))
        tri(f, (cx + 10, cy - 60), (cx + 12.5, cy - 66), (cx + 15, cy - 60), STEEL)
        flag(f, cx + 11, cy - 58, 16, 20, ph, BANNER, emblem=ICE, wave=0.8 + horn)
        # legs
        capsule(f, cx + 8, cy + 18, cx + 10, oy - 4, 5.5, 5, FROSTSTEEL, tex=0.08, seed=2)
        capsule(f, cx - 8 + step, cy + 18, cx - 12 + step, oy - 4, 5.5, 5, FROSTSTEEL, tex=0.08, seed=3)
        ellipse(f, cx + 11, oy - 3, 7, 3, FROSTSTEEL); ellipse(f, cx - 13 + step, oy - 3, 7, 3, FROSTSTEEL)
        # armour, tabard and pauldrons
        ellipse(f, cx, cy - 4, 18, 20, FROSTSTEEL, tex=0.1, seed=4)
        polygon(f, [(cx - 11, cy - 12), (cx + 11, cy - 12), (cx + 13, cy + 22), (cx, cy + 26), (cx - 13, cy + 22)], NAVY, tex=0.1, seed=5)
        ellipse(f, cx, cy + 2, 5, 5, ICE)
        line(f, cx - 11, cy - 11, cx + 11, cy - 11, (220, 190, 100))
        polygon(f, [(cx - 22, cy - 22), (cx - 8, cy - 26), (cx - 8, cy - 14), (cx - 20, cy - 12)], FROSTSTEEL, tex=0.08, seed=6)
        polygon(f, [(cx + 8, cy - 26), (cx + 22, cy - 22), (cx + 20, cy - 12), (cx + 8, cy - 14)], FROSTSTEEL, tex=0.08, seed=7)
        line(f, cx - 21, cy - 21, cx - 9, cy - 25, (220, 190, 100)); line(f, cx + 9, cy - 25, cx + 21, cy - 21, (220, 190, 100))
        # crested helm with a visor
        hx, hy = cx - 3, cy - 32
        ellipse(f, hx, hy, 9, 9.5, FROSTSTEEL, tex=0.06, seed=8)
        polygon(f, [(hx - 2, hy - 9), (hx + 12, hy - 14 + b), (hx + 14, hy - 8 + b), (hx + 2, hy - 5)], BANNER)
        line(f, hx - 8, hy, hx + 3, hy, (20, 30, 50))
        f.put(hx - 6, hy, ICE_EYE); f.put(hx - 2, hy, ICE_EYE)
        halo(f, hx - 4, hy, 4, FROST, 70)
        # sword arm / lance arm
        if a == "lance":
            lx = cx - 12 - p["s"] * 18 + p["w"] * 4
            capsule(f, cx - 10, cy - 14, lx + 6, cy - 10, 5, 4.4, FROSTSTEEL)
            line(f, lx + 26, cy - 10, lx - 30, cy - 12, (210, 220, 236))
            line(f, lx + 26, cy - 9, lx - 30, cy - 11, (140, 150, 170))
            tri(f, (lx - 30, cy - 15), (lx - 42, cy - 11.5), (lx - 30, cy - 8), ICE)
            if p["s"] > 0.6:
                line(f, lx - 44, cy - 11, lx - 70, cy - 11, FROST, 230 * p["s"])
                halo(f, lx - 42, cy - 11, 8, FROST, 200 * p["s"])
        else:
            if a == "attack":
                th = math.radians(200 - 30 * p["w"] + 20 * p["s"])
                reach = 12 + 14 * p["s"]
            elif a in ("call", "cast"):
                th = math.radians(250 + 20 * p["s"])
                reach = 12
            else:
                th = math.radians(205 + math.sin(math.tau * ph) * 3)
                reach = 12
            shx, shy = cx - 14, cy - 14
            hx2, hy2 = shx + math.cos(th) * reach * 0.6, shy + math.sin(th) * reach * 0.6 + 6
            capsule(f, shx, shy, hx2, hy2, 5, 4.4, FROSTSTEEL, tex=0.08, seed=9)
            ex, ey = hx2 + math.cos(th) * 32, hy2 + math.sin(th) * 32
            line(f, hx2, hy2, ex, ey, (220, 230, 245)); line(f, hx2 + 1, hy2, ex + 1, ey, (150, 160, 180))
            line(f, hx2 - 4 * math.sin(th), hy2 + 4 * math.cos(th), hx2 + 4 * math.sin(th), hy2 - 4 * math.cos(th), (220, 190, 100))
            if a == "attack" and p["s"] > 0.6:
                halo(f, ex, ey, 7, FROST, 200 * p["s"])
            if a in ("call", "cast"):
                halo(f, ex, ey, 6 + p["s"] * 6, (255, 220, 150), 180 * p["s"])
        if a == "bugle":
            bx, by = cx + 2, cy - 30
            capsule(f, cx + 10, cy - 16, bx + 4, by + 4, 4, 3.6, FROSTSTEEL)
            curve(f, [(bx, by), (bx - 6, by - 2), (bx - 10, by - 6)], [1.4, 2, 3], BRASS)
            for r in (10, 18, 26):
                ring(f, bx - 12, by - 8, r * (0.5 + horn * 0.6), (255, 220, 150), 150 * horn, arc=(math.pi * 0.7, math.pi * 1.3))
        if a == "call":
            ring(f, cx - 60, oy - 4, 8 + 14 * p["s"], (200, 230, 255), 200 * p["s"])


class Wendigo(Recipe):
    id = "wendigo"; cell = (208, 124); anchor = (142, 119); boss = True; elite = True; seed = 71
    anims = [("idle", 12, [], "idle"), ("attack", 11, [5], "action"), ("claws", 11, [3, 6], "action"), ("howl", 11, [], "action"),
             ("hunger", 12, [], "action"), ("cast", 10, [], "action"), ("hurt", 6, [], "hurt"), ("death", 15, [], "death")]
    moves = {"devour_flesh": "attack", "twin_claws": "claws", "frozen_howl": "howl", "endless_hunger": "hunger", "vengeance": "cast"}
    points = {"chest": (-6, -56), "eyes": (-16, -88), "body": (0, -50), "claw": (-60, -46)}

    def draw(self, f, p):
        ox, oy, b, ph = p["ox"], p["oy"], p["b"], p["t"]
        a = p["anim"]
        lunge = (-p["s"] * 12 + p["w"] * 4) if a in ("attack", "claws") else 0.0
        howl = p["s"] if a in ("howl", "hunger") else 0.0
        cx, cy = ox + lunge * 0.4, oy - 56 + b * 1.0
        # long digitigrade legs
        for (lx, sd) in ((8, 1), (-6, 2)):
            curve(f, [(cx + lx, cy + 16), (cx + lx - 6, cy + 30), (cx + lx + 2, cy + 42), (cx + lx - 4, oy - 2)], [4, 3, 2.4, 2], WENDISKIN, tex=0.08, seed=sd)
            tri(f, (cx + lx - 10, oy - 1), (cx + lx - 2, oy - 4), (cx + lx, oy - 1), BONE)
        # emaciated torso with ribs
        ellipse(f, cx, cy, 13, 20, WENDISKIN, tex=0.08, seed=3)
        for k in range(5):
            y = cy - 10 + k * 4
            line(f, cx - 9 + k * 0.5, y, cx + 7 - k * 0.5, y + 1, (40, 38, 44))
        polygon(f, [(cx - 14, cy - 18), (cx + 18, cy - 20), (cx + 22, cy - 6), (cx - 10, cy - 8)], SNOWFUR, tex=0.2, seed=4)
        # deer skull head with huge antlers
        hx, hy = cx - 6 + lunge * 0.4, cy - 27
        curve(f, [(cx + 2, cy - 16), (hx + 4, hy + 6), (hx + 2, hy + 1)], [4, 3, 2.6], WENDISKIN, tex=0.06, seed=12)   # sinewy neck
        polygon(f, [(hx - 4, hy - 6), (hx + 6, hy - 6), (hx + 5, hy + 4), (hx - 10, hy + 8), (hx - 12, hy + 4)], BONE, tex=0.05, seed=5)
        f.put(hx - 2, hy - 2, (10, 10, 14)); f.put(hx + 2, hy - 2, (10, 10, 14))
        eye_c = mix(ICE_EYE, RED_EYE, min(1.0, howl + (0.5 if a in ("attack", "claws") else 0.0)))
        f.put(hx - 2, hy - 3, eye_c); f.put(hx + 2, hy - 3, eye_c)
        halo(f, hx, hy - 3, 4, eye_c, 80 + 100 * howl)
        if a in ("attack", "howl", "hunger") and (p["s"] > 0.3 or howl > 0.3):
            polygon(f, [(hx - 11, hy + 5), (hx - 3, hy + 6), (hx - 4, hy + 10 + howl * 3), (hx - 10, hy + 9)], ramp((30, 6, 10), (80, 16, 24)))
            for k in range(3):
                f.put(hx - 10 + k * 3, hy + 6, WHITE)
        for s in (-1, 1):
            base = (hx + s * 3, hy - 6)
            curve(f, [base, (base[0] + s * 6, base[1] - 10), (base[0] + s * 4, base[1] - 20), (base[0] + s * 10, base[1] - 28)], [2, 1.6, 1.3, 0.8], BONE)
            curve(f, [(base[0] + s * 6, base[1] - 10), (base[0] + s * 13, base[1] - 14)], [1.4, 0.8], BONE)
            curve(f, [(base[0] + s * 4, base[1] - 20), (base[0] - s * 1, base[1] - 27)], [1.2, 0.7], BONE)
        # long arms with claws
        for (sx, sd, front) in ((cx + 12, 6, False), (cx - 12, 7, True)):
            if front and a == "claws":
                tx, ty = cx - 32 - p["s"] * 10, cy - 10 + (10 * p["s"] if p.get("i", 0) < 6 else -10 * p["s"])
            elif front and a == "attack":
                tx, ty = cx - 30 - p["s"] * 12, cy - 18
            elif front:
                tx, ty = cx - 24, cy + 18 + math.sin(math.tau * ph) * 2
            else:
                tx, ty = cx + 18, cy + 22 + math.sin(math.tau * ph + 1) * 2
            curve(f, [(sx, cy - 14), ((sx + tx) / 2 + (0 if front else 4), (cy - 14 + ty) / 2 + 6), (tx, ty)], [3.2, 2.6, 2], WENDISKIN, tex=0.06, seed=sd)
            for k in range(3):
                line(f, tx, ty, tx - 6 + (0 if front else 8), ty + 2 + k * 2, BONE[3])
        if a == "claws" and p["s"] > 0.6:
            for k in range(3):
                line(f, cx - 50 + k * 4, cy - 22 + k * 5, cx - 36 + k * 4, cy - 6 + k * 5, FROST, 230 * p["s"])
        if a == "attack" and p["s"] > 0.5:
            halo(f, hx - 18, hy + 8, 9, (200, 40, 60), 200 * p["s"])
        if a == "howl":
            for k in range(10):
                t = (k / 10 + p["k"]) % 1.0
                f.glow(hx - 14 - t * 60, hy + 6 + math.sin(t * 10 + k) * 10 * t, FROST_CORE, 220 * howl * (1 - t * 0.5))
            snowflakes(f, cx - 30, cy, 90, 60, 18, p["k"], 71, 200 * howl)
        if a == "hunger":
            for r in (16, 26, 36):
                ring(f, cx, cy, r * (0.5 + howl * 0.6), (200, 40, 60), 150 * howl)


class Chronomancer(Recipe):
    id = "chronomancer"; cell = (208, 120); anchor = (142, 115); boss = True; elite = True; seed = 72
    anims = [("idle", 14, [], "idle"), ("attack", 11, [5], "action"), ("paradox", 12, [3, 6, 9], "action"), ("future", 12, [7], "action"),
             ("rewind", 12, [], "action"), ("cast", 10, [], "action"), ("hurt", 6, [], "hurt"), ("death", 15, [], "death")]
    moves = {"slow_time": "attack", "paradox": "paradox", "future_strike": "future", "rewind": "rewind", "time_loop": "rewind", "vengeance": "cast"}
    points = {"chest": (-4, -52), "eyes": (-8, -80), "body": (0, -46), "claw": (-52, -54)}

    def draw(self, f, p):
        ox, oy, b, ph = p["ox"], p["oy"], p["b"], p["t"]
        a = p["anim"]
        power = p["s"] if a in ("rewind", "cast", "future") else 0.0
        fl = -3 + b * 1.5
        cx, cy = ox + 2, oy - 50 + fl
        hem = [(cx - 24 + k * 6, oy - 4 + fl * 0.4 + (3 if k % 2 else 0)) for k in range(9)]
        polygon(f, [(cx - 12, cy - 22), (cx + 12, cy - 22), (cx + 24, hem[-1][1])] + hem[::-1] + [(cx - 24, hem[0][1])], NAVY, tex=0.1, seed=1,
                mod=bands(7, 0.22, "x", 4))
        for k in range(6):   # golden star embroidery
            sx_, sy_ = cx - 16 + k * 6, cy + 8 + (k % 3) * 8
            f.put(sx_, sy_, (240, 210, 120))
        polygon(f, [(cx - 16, cy - 26), (cx + 16, cy - 26), (cx + 11, cy - 16), (cx - 11, cy - 16)], NAVY)
        # head: long white beard, pointed hat
        hx, hy = cx - 2, cy - 34
        ellipse(f, hx, hy, 6.5, 7, BARBSKIN)
        polygon(f, [(hx - 6, hy + 2), (hx + 5, hy + 2), (hx + 2, hy + 18), (hx - 3, hy + 20)], SNOWFUR, tex=0.12, seed=2)
        polygon(f, [(hx - 12, hy - 4), (hx + 12, hy - 4), (hx + 4, hy - 26), (hx + 12, hy - 34)], NAVY, tex=0.06, seed=3)
        f.put(hx - 3, hy - 1, (240, 210, 120)); f.put(hx + 1, hy - 1, (240, 210, 120))
        halo(f, hx - 1, hy - 1, 4, (255, 220, 140), 80 + 100 * power)
        # hourglass staff (back hand)
        sx, top = cx + 22, cy - 46 - (power * 4 if a in ("rewind", "cast") else 0)
        capsule(f, cx + 12, cy - 18, sx, cy - 12, 3.4, 3, NAVY)
        line(f, sx, top + 10, sx, oy - 2, (110, 76, 46)); line(f, sx + 1, top + 10, sx + 1, oy - 2, (70, 46, 26))
        flip = (p["k"] * math.pi if a == "rewind" else 0.0)
        hgx, hgy = sx, top
        polygon(f, [(hgx - 6, hgy - 8), (hgx + 6, hgy - 8), (hgx + 1, hgy), (hgx + 6, hgy + 8), (hgx - 6, hgy + 8), (hgx - 1, hgy)], ramp((140, 180, 210), (200, 230, 250), (240, 252, 255)))
        sand_up = 0.5 + 0.5 * math.cos(ph * math.tau + flip)
        polygon(f, [(hgx - 4 * sand_up, hgy - 1 - 6 * sand_up), (hgx + 4 * sand_up, hgy - 1 - 6 * sand_up), (hgx, hgy - 1)], SAND)
        polygon(f, [(hgx - 4 * (1 - sand_up), hgy + 7), (hgx + 4 * (1 - sand_up), hgy + 7), (hgx, hgy + 7 - 6 * (1 - sand_up))], SAND)
        line(f, hgx - 7, hgy - 9, hgx + 7, hgy - 9, (200, 160, 80)); line(f, hgx - 7, hgy + 9, hgx + 7, hgy + 9, (200, 160, 80))
        # front hand
        if a in ("attack", "paradox", "future"):
            hx2, hy2 = cx - 20 - p["s"] * 10, cy - 18
        else:
            hx2, hy2 = cx - 14, cy - 8 + math.sin(math.tau * ph) * 1.5
        capsule(f, cx - 8, cy - 18, hx2, hy2, 3.2, 2.8, NAVY)
        ellipse(f, hx2, hy2, 2.4, 2.4, BARBSKIN)
        # orbiting clock rings (idle detail on the effects layer, kept small)
        for k in range(3):
            t = ph * math.tau + k * math.tau / 3
            spot(f, cx + math.cos(t) * 26, cy - 8 + math.sin(t) * 7, (220, 190, 110))
        if a == "attack" and p["s"] > 0.3:
            for r in (6, 10):
                ring(f, hx2 - 12, hy2, r, (180, 220, 255), 200 * p["s"])
            line(f, hx2 - 12, hy2, hx2 - 12 + math.cos(p["k"] * 6) * 8, hy2 + math.sin(p["k"] * 6) * 8, WHITE, 230 * p["s"])
        if a == "paradox":
            for st in (3, 6, 9):
                d = p.get("i", 0) - st
                if 0 <= d <= 1:
                    x = hx2 - 16 - d * 30
                    halo(f, x, hy2 - 6 + st * 2, 7, (255, 220, 140), 230, core=WHITE)
        if a == "future" and p["s"] > 0.3:
            line(f, hx2 - 6, hy2, hx2 - 80, hy2 + 6, (255, 230, 160), 240 * p["s"])
            halo(f, hx2 - 80, hy2 + 6, 10, (255, 220, 140), 200 * p["s"])
        if a == "rewind":
            r = 30 + 6 * power
            ring(f, cx, cy - 8, r, (240, 210, 120), 200 * power)
            for k in range(12):
                t = k / 12 * math.tau
                f.glow(cx + math.cos(t) * (r - 3), cy - 8 + math.sin(t) * (r - 3), WHITE, 200 * power)
            ang = -p["k"] * math.tau * 2
            line(f, cx, cy - 8, cx + math.cos(ang) * (r - 6), cy - 8 + math.sin(ang) * (r - 6), (255, 240, 200), 240 * power)
            line(f, cx, cy - 8, cx + math.cos(ang * 0.25) * (r - 14), cy - 8 + math.sin(ang * 0.25) * (r - 14), (255, 240, 200), 240 * power)
        if a == "cast":
            halo(f, cx, cy - 6, 26 * p["s"], (180, 220, 255), 60 * p["s"])


# ------------------------------------------------------------------ bosses
class LichKing(Recipe):
    id = "lichking"; cell = (224, 128); anchor = (150, 121); boss = True; seed = 73
    anims = [("idle", 16, [], "idle"), ("attack", 11, [6], "action"), ("blizzard", 12, [4, 6, 8], "action"), ("touch", 11, [5], "action"),
             ("rite", 12, [], "action"), ("cast", 10, [], "action"), ("hurt", 6, [], "hurt"), ("death", 15, [], "death")]
    moves = {"frost_lance": "attack", "blizzard": "blizzard", "void_touch": "touch", "phylactery_rite": "rite", "lich_fury": "rite",
             "ice_shroud": "cast", "vengeance": "cast"}
    points = {"chest": (-6, -58), "eyes": (-10, -88), "body": (0, -50), "claw": (-54, -60)}

    def draw(self, f, p):
        ox, oy, b, ph = p["ox"], p["oy"], p["b"], p["t"]
        a = p["anim"]
        power = p["s"] if a in ("blizzard", "rite", "cast", "touch") else 0.0
        fl = -4 + b * 2
        cx, cy = ox + 2, oy - 54 + fl
        hem = [(cx - 28 + k * 7, oy - 4 + fl * 0.5 + (4 if k % 2 else 0) + math.sin(math.tau * ph + k) * 1.4) for k in range(9)]
        polygon(f, [(cx - 14, cy - 22), (cx + 14, cy - 22), (cx + 28, hem[-1][1])] + hem[::-1] + [(cx - 28, hem[0][1])], LICHROBE, tex=0.12, seed=1,
                mod=bands(6, 0.22, "x", ph * 6))
        for k in range(5):   # icicles on the hem
            x = cx - 22 + k * 11
            tri(f, (x - 2, hem[min(8, k * 2)][1] - 2), (x, hem[min(8, k * 2)][1] + 5), (x + 2, hem[min(8, k * 2)][1] - 2), ICE)
        # armoured chest with a frost gem
        ellipse(f, cx, cy - 8, 15, 15, FROSTSTEEL, tex=0.08, seed=2)
        ellipse(f, cx, cy - 6, 4.5, 5, ramp((40, 90, 140), (110, 190, 240), (220, 250, 255)))
        halo(f, cx, cy - 6, 6 + power * 4, FROST, 110 + 100 * power, core=FROST_CORE)
        polygon(f, [(cx - 22, cy - 24), (cx - 6, cy - 28), (cx - 6, cy - 16), (cx - 20, cy - 12)], FROSTSTEEL, tex=0.06, seed=3)
        polygon(f, [(cx + 6, cy - 28), (cx + 22, cy - 24), (cx + 20, cy - 12), (cx + 6, cy - 16)], FROSTSTEEL, tex=0.06, seed=4)
        for s in (-1, 1):
            tri(f, (cx + s * 12, cy - 26), (cx + s * 18, cy - 36), (cx + s * 20, cy - 24), ICE)
        # skull with an icy crown
        hx, hy = cx - 3, cy - 38
        ellipse(f, hx, hy, 7.5, 8, SKULL)
        polygon(f, [(hx - 6, hy + 4), (hx + 4, hy + 4), (hx + 3, hy + 9), (hx - 5, hy + 9)], SKULL)
        for ex in (-5, -1):
            f.put(hx + ex, hy, INK); f.put(hx + ex + 1, hy, INK)
            halo(f, hx + ex + 0.5, hy - 1, 3 + power * 2, FROST, 150 + 70 * power, core=FROST_CORE)
        for k in range(3):
            f.put(hx - 4 + k * 2, hy + 7, (60, 64, 76))
        for k in range(5):
            h = 7 + (5 if k == 2 else 0) + (k % 2) * 2
            tri(f, (hx - 8 + k * 4, hy - 7), (hx - 6 + k * 4, hy - 7 - h), (hx - 4 + k * 4, hy - 7), ICE)
        line(f, hx - 8, hy - 7, hx + 9, hy - 7, (200, 170, 90))
        # staff with a crystal (back hand)
        sx, top = cx + 26, cy - 50 - (power * 6 if a in ("rite", "cast") else 0)
        capsule(f, cx + 14, cy - 20, sx, cy - 14, 4, 3.4, FROSTSTEEL)
        line(f, sx, top + 8, sx, oy - 2, (150, 160, 180)); line(f, sx + 1, top + 8, sx + 1, oy - 2, (90, 100, 120))
        polygon(f, [(sx, top - 8), (sx + 5, top), (sx, top + 9), (sx - 5, top)], ICE, tex=0.05, seed=5)
        halo(f, sx, top, 6 + power * 6, FROST, 120 + 100 * power, core=FROST_CORE)
        # front hand: lance of ice, void touch
        if a == "attack":
            hx2, hy2 = cx - 22 - p["s"] * 10, cy - 20
        elif a in ("touch", "blizzard", "rite", "cast"):
            hx2, hy2 = cx - 20, cy - 30 - power * 6
        else:
            hx2, hy2 = cx - 16, cy - 10 + math.sin(math.tau * ph) * 1.5
        capsule(f, cx - 10, cy - 20, hx2, hy2, 4, 3.4, FROSTSTEEL)
        for k in range(3):
            line(f, hx2, hy2, hx2 - 4, hy2 - 2 + k * 2, SKULL[3])
        if a == "attack":
            if p.get("i", 0) < 6:
                ln = 10 + 20 * p["w"]
                tri(f, (hx2 - 2, hy2 - 3), (hx2 - 2 - ln, hy2), (hx2 - 2, hy2 + 3), ICE)
            else:
                d = min(1.0, (p["i"] - 6) / 3)
                x = hx2 - 10 - d * 80
                tri(f, (x + 24, hy2 - 3), (x, hy2), (x + 24, hy2 + 3), ICE)
                halo(f, x, hy2, 8, FROST, 220 * (1 - d * 0.4), core=FROST_CORE)
        if a == "blizzard":
            snowflakes(f, cx - 50, cy - 10, 110, 80, 28, p["k"] * 2, 73, 220 * max(0.3, power))
            for st in (4, 6, 8):
                d = p.get("i", 0) - st
                if 0 <= d <= 1:
                    halo(f, cx - 60 - d * 20, cy - 20 + st * 3, 9, FROST, 220, core=WHITE)
        if a == "touch" and p["s"] > 0.3:
            halo(f, hx2 - 10, hy2, 9 + p["s"] * 5, VOID, 220 * p["s"], core=VOID_CORE)
        if a == "rite":
            ring(f, cx - 60, oy - 6, 10 + 14 * p["s"], FROST, 200 * p["s"])
            for k in range(6):
                t = k / 6 * math.tau + p["k"] * 4
                halo(f, cx - 60 + math.cos(t) * 14, oy - 22 + math.sin(t) * 6, 3, FROST_CORE, 200 * p["s"])
        if a == "cast":
            if power > 0.4:   # a shroud of ice shards around it
                for k in range(8):
                    t = k / 8 * math.tau
                    tri(f, (cx + math.cos(t) * 30, cy - 6 + math.sin(t) * 16), (cx + math.cos(t) * (36 + 6 * power), cy - 6 + math.sin(t) * (20 + 4 * power)),
                        (cx + math.cos(t + 0.1) * 30, cy - 6 + math.sin(t + 0.1) * 16), ICE)
            halo(f, cx, cy - 6, 30 * power, FROST, 70 * power)


class Clockmaker(Recipe):
    id = "clockmaker"; cell = (224, 128); anchor = (150, 121); boss = True; seed = 74
    anims = [("idle", 16, [], "idle"), ("attack", 11, [4, 7], "action"), ("chime", 14, [3, 5, 7, 9, 11], "action"), ("stop", 11, [], "action"),
             ("build", 12, [7], "action"), ("cast", 10, [], "action"), ("hurt", 6, [], "hurt"), ("death", 15, [], "death")]
    moves = {"clock_hands": "attack", "chime": "chime", "stop_time": "stop", "build_automata": "build", "zero_hour": "build",
             "oil_gears": "cast", "vengeance": "cast"}
    points = {"chest": (-4, -60), "eyes": (-8, -94), "body": (0, -54), "claw": (-60, -56)}

    def draw(self, f, p):
        ox, oy, b, ph = p["ox"], p["oy"], p["b"], p["t"]
        a = p["anim"]
        cx, cy = ox + 2, oy - 56 + b * 0.6
        power = p["s"] if a in ("stop", "build", "cast", "chime") else 0.0
        # stubby iron legs
        for lx in (-10, 10):
            capsule(f, cx + lx, cy + 30, cx + lx, oy - 4, 4.4, 4, DARKWOOD, tex=0.08, seed=1)
            ellipse(f, cx + lx - 2, oy - 3, 7, 3, BRASS)
        # grandfather-clock body
        polygon(f, [(cx - 18, cy - 30), (cx + 18, cy - 30), (cx + 16, cy + 34), (cx - 16, cy + 34)], DARKWOOD, tex=0.12, seed=2, mod=bands(5, 0.15, "y", 0))
        polygon(f, [(cx - 20, cy - 36), (cx + 20, cy - 36), (cx + 18, cy - 30), (cx - 18, cy - 30)], BRASS)
        # pendulum window
        polygon(f, [(cx - 9, cy + 6), (cx + 9, cy + 6), (cx + 9, cy + 30), (cx - 9, cy + 30)], ramp((20, 14, 10), (40, 30, 22)))
        swing = math.sin(math.tau * ph * 2) * (0.5 if a != "stop" else 0.5 * (1 - p["s"]))
        px_, py_ = cx + math.sin(swing) * 16, cy + 8 + math.cos(swing) * 16
        line(f, cx, cy + 8, px_, py_, (200, 160, 80))
        ellipse(f, px_, py_, 3.4, 3.4, BRASS)
        # the clock face is its head-chest
        fx_, fy_ = cx, cy - 12
        ellipse(f, fx_, fy_, 15, 15, BRASS)
        ellipse(f, fx_, fy_, 12.5, 12.5, CLOCKFACE)
        for k in range(12):
            t = k / 12 * math.tau
            f.put(fx_ + math.cos(t) * 10.5, fy_ + math.sin(t) * 10.5, INK)
        hour = ph * math.tau / 4 - math.pi / 2
        minute = ph * math.tau - math.pi / 2
        if a == "stop":
            hour, minute = -math.pi / 2 - 0.4, -math.pi / 2
        if a == "chime":
            hour, minute = -math.pi / 2, -math.pi / 2
        line(f, fx_, fy_, fx_ + math.cos(hour) * 6, fy_ + math.sin(hour) * 6, INK)
        line(f, fx_, fy_, fx_ + math.cos(minute) * 9, fy_ + math.sin(minute) * 9, (60, 20, 20))
        # two eyes above the face, a little top with a bell
        f.put(fx_ - 5, fy_ - 4, EMBER); f.put(fx_ + 4, fy_ - 4, EMBER)
        halo(f, fx_, fy_ - 4, 4, EMBER, 60 + 120 * power)
        ellipse(f, cx, cy - 40, 5, 4, BRASS)
        tri(f, (cx - 3, cy - 43), (cx, cy - 50), (cx + 3, cy - 43), BRASS)
        # mechanical arms holding giant clock hands
        for (sx, side, sd) in ((cx - 18, -1, 3), (cx + 18, 1, 4)):
            if a == "attack":
                st = p.get("i", 0)
                th = math.radians(200 - 100 * p["s"] + 30 * p["w"]) if side < 0 else math.radians(-20 + (60 * p["s"] if st >= 6 else 0))
            elif a == "build":
                th = math.radians(150 + 40 * math.sin(p["k"] * math.tau * 3)) if side < 0 else math.radians(30)
            elif a == "chime":
                th = math.radians(250 - 50 * p["s"]) if side < 0 else math.radians(-70 + 50 * p["s"])
            else:
                th = math.radians(160 + math.sin(math.tau * ph) * 4) if side < 0 else math.radians(20 - math.sin(math.tau * ph) * 4)
            ex, ey = sx + math.cos(th) * 14, cy - 16 + math.sin(th) * 14
            capsule(f, sx, cy - 16, ex, ey, 2.4, 2.2, BRASS)
            ellipse(f, sx, cy - 16, 3.4, 3.4, COPPER)
            if side < 0:
                tx, ty = ex + math.cos(th) * 30, ey + math.sin(th) * 30
                capsule(f, ex, ey, tx, ty, 2.2, 0.8, ramp((20, 16, 14), (60, 50, 44), (110, 100, 90)))
                ellipse(f, ex + math.cos(th) * 22, ey + math.sin(th) * 22, 3, 3, ramp((20, 16, 14), (60, 50, 44)))
            else:
                ellipse(f, ex, ey, 3, 3, COPPER)
        if a == "attack" and p["s"] > 0.6:
            ring(f, cx - 18, cy - 16, 40, (255, 230, 170), 180 * p["s"], arc=(math.pi * 0.6, math.pi * 1.3))
        if a == "chime":
            for st in (3, 5, 7, 9, 11):
                d = p.get("i", 0) - st
                if 0 <= d <= 1:
                    for r in (8, 16, 24):
                        ring(f, cx, cy - 46, r + d * 8, (255, 230, 160), 200 - d * 80, arc=(math.pi * 0.55, math.pi * 1.45))
        if a == "stop":
            ring(f, fx_, fy_, 18 + 10 * p["s"], (180, 220, 255), 200 * p["s"])
            ring(f, fx_, fy_, 26 + 14 * p["s"], (180, 220, 255), 120 * p["s"], width=0.8)
        if a == "build" and p["s"] > 0.1:
            for k in range(5):
                t = p["k"] * 3 + k * 0.2
                f.glow(cx - 66 + math.cos(t * 7) * 8, oy - 8 - (t % 1.0) * 18, (255, 220, 140), 220)
            ring(f, cx - 66, oy - 4, 8 + 12 * p["s"], (255, 210, 120), 190 * p["s"])
        if a == "cast":
            for k in range(6):
                t = ph * math.tau + k
                halo(f, cx + math.cos(t) * 30, cy - 10 + math.sin(t) * 10, 3, (255, 210, 120), 190 * p["s"])


class FrostDragon(Recipe):
    id = "frostdragon"; cell = (240, 128); anchor = (164, 121); boss = True; seed = 75
    anims = [("idle", 16, [], "idle"), ("attack", 11, [5], "action"), ("breath", 13, [4, 6, 8], "action"), ("tail", 12, [6], "action"),
             ("fury", 12, [], "action"), ("cast", 11, [], "action"), ("hurt", 6, [], "hurt"), ("death", 15, [], "death")]
    moves = {"piercing_claw": "attack", "frost_breath": "breath", "glacier_tail": "tail", "glacial_fury": "fury", "fold_wings": "cast", "vengeance": "cast"}
    points = {"chest": (-20, -52), "eyes": (-62, -80), "body": (0, -46), "claw": (-70, -30)}

    def draw(self, f, p):
        ox, oy, b, ph = p["ox"], p["oy"], p["b"], p["t"]
        a = p["anim"]
        lunge = (-p["s"] * 10 + p["w"] * 3) if a in ("attack", "breath") else 0.0
        fold = p["s"] if a == "cast" else 0.0
        rage = p["s"] if a == "fury" else 0.0
        cx, cy = ox, oy - 44 + b * 1.0
        # tail curling behind
        if a == "tail":
            sw = p["s"]
            tail = [(cx + 24, cy + 14), (cx + 44, cy + 20), (cx + 56 - sw * 30, cy + 26 - sw * 18), (cx + 50 - sw * 80, cy + 30 - sw * 10)]
        else:
            tail = [(cx + 24, cy + 14), (cx + 44, cy + 22), (cx + 60, cy + 26 + math.sin(math.tau * ph) * 2), (cx + 70, cy + 16 + math.sin(math.tau * ph + 1) * 3)]
        curve(f, tail, [9, 7, 4.5, 2], DRAGON, tex=0.08, seed=1)
        for k in range(3):
            x, y = tail[k + 1]
            tri(f, (x - 3, y - 4), (x, y - 10), (x + 3, y - 4), ICE)
        # back wing (folds over the body in cast)
        wing_up = 0.6 + 0.4 * math.sin(math.tau * ph) - fold * 0.9 + rage * 0.4
        wx, wy = cx + 18, cy - 20
        tip = (wx + 30 - fold * 20, wy - 40 * wing_up - 4)
        polygon(f, [(wx, wy), tip, (wx + 46 - fold * 30, wy - 8 * wing_up + 10), (wx + 30, wy + 14), (wx + 14, wy + 12)],
                ramp((40, 70, 100), (80, 130, 170), (140, 190, 220), (200, 236, 250)), tex=0.06, seed=2)
        line(f, wx, wy, tip[0], tip[1], DRAGON[1])
        line(f, wx, wy, wx + 46 - fold * 30, wy - 8 * wing_up + 10, DRAGON[1])
        # legs
        for (lx, sd) in ((14, 3), (-10, 4)):
            capsule(f, cx + lx, cy + 10, cx + lx - 2, oy - 4, 6.5, 5, DRAGON, tex=0.08, seed=sd)
            for k in range(3):
                tri(f, (cx + lx - 8 + k * 3, oy - 3), (cx + lx - 10 + k * 3, oy + 0), (cx + lx - 6 + k * 3, oy - 2), ICE)
        # body
        ellipse(f, cx + 4, cy + 2, 30, 20, DRAGON, tex=0.08, seed=5)
        ellipse(f, cx - 4, cy + 8, 22, 12, DRAGON_BELLY, tex=0.06, seed=6, mod=bands(4, 0.25, "x", 0))
        for k in range(5):   # dorsal ice spikes
            x = cx - 6 + k * 9
            tri(f, (x - 3, cy - 16 + abs(k - 2)), (x + 1, cy - 26 + abs(k - 2) * 2), (x + 4, cy - 15 + abs(k - 2)), ICE)
        # neck and head
        nx0, ny0 = cx - 20, cy - 6
        hx, hy = cx - 46 + lunge, cy - 30 + (p["s"] * 6 if a == "breath" else 0)
        curve(f, [(nx0, ny0), (nx0 - 10, ny0 - 14), (hx + 10, hy + 4)], [9, 7, 6], DRAGON, tex=0.08, seed=7)
        ellipse(f, hx + 4, hy, 11, 8, DRAGON, tex=0.06, seed=8)
        jaw = (p["s"] * 5 if a in ("breath", "attack") else 0.0) + rage * 4
        polygon(f, [(hx - 4, hy + 1), (hx - 16, hy + 2), (hx - 14, hy + 6), (hx - 2, hy + 6)], DRAGON)
        polygon(f, [(hx - 4, hy + 4 + jaw), (hx - 14, hy + 6 + jaw), (hx - 12, hy + 9 + jaw), (hx - 2, hy + 8 + jaw)], DRAGON_BELLY)
        if jaw > 1:
            for k in range(3):
                f.put(hx - 12 + k * 3, hy + 6, WHITE)
        eye_c = mix(ICE_EYE, (120, 200, 255) if not rage else RED_EYE, rage)
        f.put(hx, hy - 2, eye_c); f.put(hx + 1, hy - 2, eye_c)
        halo(f, hx + 0.5, hy - 2, 3.5, eye_c, 90 + 120 * rage)
        for s in (0, 1):   # horns
            curve(f, [(hx + 8, hy - 5 + s * 2), (hx + 16, hy - 12 + s * 3), (hx + 22, hy - 14 + s * 5)], [2.2, 1.6, 0.8], ICE)
        # front claw
        if a == "attack":
            fx_, fy_ = cx - 26 - p["s"] * 16, cy + 6 - p["w"] * 10 + p["s"] * 10
        else:
            fx_, fy_ = cx - 22, cy + 18 + math.sin(math.tau * ph) * 1
        capsule(f, cx - 12, cy + 4, fx_, fy_, 5, 4, DRAGON, tex=0.06, seed=9)
        for k in range(3):
            tri(f, (fx_ - 2, fy_ - 2 + k * 2), (fx_ - 9, fy_ + k * 2.5), (fx_ - 2, fy_ + 1 + k * 2), ICE)
        if a == "attack" and p["s"] > 0.6:
            for k in range(3):
                line(f, fx_ - 26 + k * 4, fy_ - 10 + k * 5, fx_ - 8 + k * 4, fy_ + 6 + k * 5, FROST_CORE, 240 * p["s"])
        if a == "breath" and p["s"] > 0.2:
            for k in range(14):
                t = (k / 14 + p["k"] * 2) % 1.0
                f.glow(hx - 16 - t * 90, hy + 6 + math.sin(t * 8 + k) * 12 * t, FROST_CORE if k % 3 else WHITE, 230 * p["s"] * (1 - t * 0.4))
            halo(f, hx - 20, hy + 6, 8, FROST, 200 * p["s"], core=WHITE)
        if a == "tail" and p["s"] > 0.5:
            sparks(f, tail[-1][0], oy - 6, 20, 24, FROST_CORE, 220, 75 + p.get("i", 0), 1)
        if fold > 0:
            halo(f, cx, cy, 30 * fold, FROST, 70 * fold)
        if rage > 0:
            snowflakes(f, cx, cy - 10, 140, 90, 24, p["k"] * 2, 75, 220 * rage)
            for r in (20, 34, 48):
                ring(f, cx - 10, cy, r * (0.5 + rage * 0.6), (200, 240, 255), 150 * rage)


RECIPES = [Bannerman, Barbarian, VoidMage, FrostHunter, FrostWisp, Tinkerer, Automaton, Squire, Phylactery, Commander, Wendigo,
           Chronomancer, LichKing, Clockmaker, FrostDragon]


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
            if i in kit.EXISTING or i in OTHER_FLOORS:
                sys.stderr.write("%s no es una hoja del piso 3: no se regenera aquí\n" % i)
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
