"""Animated pixel-art illustrations for ten of the rogue's loot cards (88x64, 24 frames at 12 fps, seamless loop).

Same technique, palette and helpers as rogue_art.py (docs/art-style.md): every frame has a base image (opaque pixel
art, readable without bloom) and an emissive light map, baked for the game by export_game_card_art.py.
No characters: each picture is her steel (the runed daggers of Puñalada and Finta) and what it touches, so nothing is
assembled from body parts.

  pinchazo            Pinchazo (3 de daño): a slim stiletto pins a falling leaf in mid-air with one quick prick.
  tajo_veloz          Tajo Veloz (6 de daño; Combo: +1 de maná): a dash across the rooftops, a long cyan crescent,
                      after-images; a blue mana drop rises from the cut.
  lanzar_la_daga      Lanzar la Daga (4 de daño; mete 1 Daga Oculta): the dagger spins end over end into a wanted
                      poster, quivers, melts into violet smoke and re-forms at home (the hidden dagger).
  lluvia_de_dagas     Lluvia de Dagas (10 de daño a 2 enemigos al azar): two volleys of daggers fall out of a violet
                      rift onto two sigils on the cobbles, stay planted and melt away.
  cuchillada_errante  Cuchillada Errante (4 de daño a un enemigo al azar; Combo: otra vez): a roulette lights one of
                      three floating sigils, the dagger wanders to it on a dotted path, strikes, and does it again.
  marcar_objetivo     Marcar Objetivo (+3 de daño por golpe a ese enemigo): a crimson crosshair locks on, three
                      thrown daggers hit it and each hit throws out crimson "+" marks.
  abrir_la_guardia    Abrir la Guardia (4 de daño; le quita todo su escudo): the thrust hits an enemy's round shield
                      in its blue barrier (block); it cracks, bursts into four pieces and the barrier shatters.
  corte_y_guardia     Corte y Guardia (3 de daño, 3 de escudo): two crossed daggers; one cuts a crescent over the top,
                      comes back, and the clash raises a blue diamond ward.
  afilar              Afilar (tu siguiente carta que haga daño inflige 6 más): the point drags across a whetstone in
                      three strokes with sparks, the runes charge, and the raised blade flashes.
  carterista          Carterista (si un enemigo muere este turno, 10 de oro): her dagger cuts a hanging coin purse,
                      it bursts on the cobbles and the coins fly up and away; a new purse is lowered.
"""
import math

import numpy as np

from pixelkit import Canvas, hexc, dilate, erode, shift
from rogue_art import (AW, AH, FRAMES, K, L, ease_out, ease_in_out, frac, gradient_bg, vignette, star4, motes,
                       outline_mask, crescent, dagger_labels, paint_dagger, arc_dagger)
from mage_art import twinkling_stars, sparks, ellipse_ring
from warrior_art import KW, LW, shake, falling_sparks, dust_puffs, WARM_SPARKS, CYAN_SPARKS

# the rogue palette plus the few warm and mana accents these scenes need
KR = dict(KW)
KR.update({
    'leaf0': hexc('#3a1712'), 'leaf1': hexc('#7a3320'), 'leaf2': hexc('#c0622a'), 'leaf3': hexc('#f2a65a'),
    'bark0': hexc('#07050c'), 'bark1': hexc('#120c1c'), 'bark2': hexc('#1d1429'),
    'brick0': hexc('#0d0818'), 'brick1': hexc('#170e27'), 'brick2': hexc('#221536'), 'brick3': hexc('#2f1d47'),
    'paper0': hexc('#241a1f'), 'paper1': hexc('#4a3b36'), 'paper2': hexc('#75614f'), 'paper3': hexc('#a48d6b'),
    'mana0': hexc('#0d2459'), 'mana1': hexc('#1e4fc0'), 'mana2': hexc('#4b93ff'), 'mana3': hexc('#c3e7ff'),
    'tile0': hexc('#06040b'), 'tile1': hexc('#0e0a19'), 'tile2': hexc('#171127'),
    'cloud0': hexc('#161030'), 'cloud1': hexc('#221a44'),
})
LR = dict(LW)
LR.update({'mana': np.array([0.3, 0.55, 1.0, 1])})
VIOLET_SPARKS = (K['violet4'], K['violet3'], K['violet1'])
GOLD_SPARKS = (KR['gold3'], KR['gold2'], KR['gold1'])


# ---------------------------------------------------------------------------------------- shared bits
def dir_of(ang):
    return math.cos(ang), math.sin(ang)


def dagger(c, g, gx, gy, ang, s, t_rune, glint=None, rune_gain=1.0, ghost=None, wb0=4.4, cut=None):
    """Her runed dagger (rogue_art.dagger_labels) with the crossguard centre at (gx, gy).
    cut = local u beyond which the blade is hidden (stuck in a wall or the ground)."""
    lab, u, v = dagger_labels(c, gx, gy, ang, s=s, wb0=wb0)
    if cut is not None:
        lab[u > cut] = 0
    paint_dagger(c, g, lab, u, t_rune, glint, rune_gain=rune_gain, ghost=ghost)
    return lab


def clipped(c, g, keep, draw):
    """Run draw(c2, g2) on blank layers and copy only the pixels inside `keep` (e.g. emerging from a portal)."""
    c2, g2 = Canvas(c.w, c.h), Canvas(g.w, g.h)
    draw(c2, g2)
    m = keep & (c2.px[..., 3] > 0)
    c.px[m] = c2.px[m]
    gm = keep & (g2.px[..., 3] > 0)
    g.px[gm, :3] += g2.px[gm, :3]
    g.px[gm, 3] = np.maximum(g.px[gm, 3], g2.px[gm, 3])


def centred_dagger(c, g, cx, cy, ang, s, t_rune, glint=None, **kw):
    """A dagger placed by its middle (for spinning): the middle of guard..tip is 10 units past the guard."""
    ux, uy = dir_of(ang)
    return dagger(c, g, cx - ux * 10 * s, cy - uy * 10 * s, ang, s, t_rune, glint, **kw)


def tip_of(gx, gy, ang, s, tip=40.0):
    ux, uy = dir_of(ang)
    return gx + ux * tip * s, gy + uy * tip * s


def shimmer(c, g, f, gx, gy, ang, s, seed, count=5):
    """Sparkles that peel off the blade edge (each lives 5 frames, looping)."""
    rng = np.random.default_rng(seed)
    ux, uy = dir_of(ang)
    vx, vy = -uy, ux
    for i in range(count):
        born = int(rng.integers(0, FRAMES))
        uu = rng.uniform(5, 34) * s
        side = -1 if rng.random() < 0.6 else 1
        age = (f - born) % FRAMES
        if age > 5:
            continue
        drift = 1.0 + age * 1.1
        px = gx + ux * uu + vx * side * (3 * s + drift)
        py = gy + uy * uu + vy * side * (3 * s + drift) - age * 0.6
        m = c.pixel(px, py)
        c.put(m, K['white'] if age < 2 else K['cyan3'])
        g.add(m, L['cyan'], 1 - age / 6)


def speed_lines(c, g, x, y, ux, uy, k, seed, count=6, spread=10, length=(6, 12), back=0):
    """1-px lines trailing behind something moving along (ux, uy)."""
    if k <= 0:
        return
    rng = np.random.default_rng(seed)
    vx, vy = -uy, ux
    for i in range(count):
        off = rng.uniform(-spread, spread)
        start = back + rng.uniform(0, 8)
        ln = rng.uniform(*length) * (0.6 + 0.4 * k)
        sx, sy = x - ux * start + vx * off, y - uy * start + vy * off
        m = c.line(sx, sy, sx - ux * ln, sy - uy * ln)
        c.put(m, K['cyan3'] if i % 2 == 0 else K['steel3'], k)
        g.add(m, L['cyan'], 0.5 * k)


def converge(c, g, f, x, y, born, life, seed, count, radius, cols, light, gain=0.9):
    """Motes drawn into (x, y) during a wind-up."""
    age = f - born
    if not 0 <= age < life:
        return
    rng = np.random.default_rng(seed)
    for i in range(count):
        a0 = rng.uniform(0, 2 * math.pi)
        r0 = radius * rng.uniform(0.6, 1.0)
        k = ease_in_out((age + 1) / life)
        r = r0 * (1 - k)
        m = c.pixel(x + math.cos(a0) * r, y + math.sin(a0) * r)
        c.put(m, cols[0] if k > 0.6 else cols[1])
        g.add(m, light, gain * (0.4 + 0.6 * k))


def mana_drop(c, g, x, y, size=1.0, gain=1.0):
    """A small blue mana crystal (the game's mana colour): pointed top and bottom, lit upper left."""
    dx, dy = c.x - x, c.y - y
    m = np.abs(dx) / (2.6 * size) + np.abs(dy) / (4.2 * size) <= 1.0
    if not m.any():
        return m
    c.put(outline_mask(m), K['ink'])
    c.put(m, KR['mana1'])
    c.put(m & (dx + dy * 0.6 < 0.4), KR['mana2'])
    c.put(m & (dx + dy * 0.6 < -1.4), KR['mana3'])
    c.put(m & (dx > 0.8) & (dy > 0.5), KR['mana0'])
    g.add(m, LR['mana'], 0.8 * gain)
    g.add(m & (dx + dy * 0.6 < -1.4), L['white'], 0.4 * gain)
    return m


def leaf_mask(c, x, y, ang, size=1.0):
    ux, uy = dir_of(ang)
    vx, vy = -uy, ux
    dx, dy = c.x - x, c.y - y
    u = (dx * ux + dy * uy) / size
    v = (dx * vx + dy * vy) / size
    half = 4.3
    wid = 2.1 * np.clip(1 - (u / half) ** 2, 0, None) ** 0.7
    body = (np.abs(u) <= half) & (np.abs(v) <= wid + 0.15)
    stem = (u < -half + 0.3) & (u > -half - 2.2) & (np.abs(v) <= 0.5)
    return body, stem, u, v


def paint_leaf(c, g, x, y, ang, size=1.0, alpha=1.0):
    body, stem, u, v = leaf_mask(c, x, y, ang, size)
    m = body | stem
    c.put(outline_mask(body), K['ink'], alpha)
    c.put(body, KR['leaf2'], alpha)
    c.put(body & (v < -0.4), KR['leaf3'], alpha)
    c.put(body & (v > 0.8), KR['leaf1'], alpha)
    c.put(body & (np.abs(v) <= 0.45) & (u > -3.5) & (u < 3.0), KR['leaf1'], alpha)
    c.put(stem, KR['leaf0'], alpha)
    return m


# ---------------------------------------------------------------------------------------- Pinchazo
P_HOME = (19.0, 49.0)
P_ANG = -0.5
P_S = 0.84
P_MOON = (61.0, 23.0)


def pinchazo_along(f):
    if f <= 7:
        return 0.0
    if f <= 10:
        return [-2.0, -4.0, -5.0][f - 8]
    if f <= 14:
        return [15.0, 14.0, 13.0, 12.0][f - 11]
    return 12.0 * (1 - ease_in_out(min(1.0, (f - 14) / 8.0)))


def full_moon(c, g, x, y, r):
    c.put(c.disc(x, y, r * 2.3), K['teal3'], 0.25)
    c.put(c.disc(x, y, r * 1.6), K['teal3'], 0.5)
    c.put(c.disc(x, y, r * 1.25), hexc('#2b5a7c'), 0.6)
    moon = c.disc(x, y, r)
    c.put(moon, K['moon1'])
    c.put(moon & c.disc(x - r * 0.3, y - r * 0.3, r * 0.8), K['moon2'])
    c.put(moon & ~c.disc(x - r * 0.25, y - r * 0.25, r * 1.02), K['moon0'], 0.7)
    for cx, cy, cr in ((3.5, 2.5, 2.4), (-2.0, 5.0, 1.6)):           # two soft maria, lower right
        cm = c.disc(x + cx * r / 10, y + cy * r / 10, cr * r / 10) & moon
        c.put(cm, K['moon1'])
        c.put(cm & ~c.disc(x + cx * r / 10 - 0.6, y + cy * r / 10 - 0.6, cr * r / 10), K['moon0'], 0.6)


def pinchazo_scene(c, g, t):
    gradient_bg(c, K['teal0'], K['teal2'], K['teal1'], 0.6)
    full_moon(c, g, P_MOON[0], P_MOON[1], 10.5)
    twinkling_stars(c, g, t, 301, 14, (2, 2, 86, 30), avoid=(P_MOON[0], P_MOON[1], 22))
    # drifting fog bands (one full cycle per loop)
    for i in range(3):
        y0 = 38 + i * 5
        off = (t * 88 * (1 if i % 2 == 0 else -1) + i * 29) % 88
        for k in range(-1, 2):
            x0 = off + k * 88
            m = c.poly([(x0, y0 + 1), (x0 + 14, y0 - 1), (x0 + 38, y0), (x0 + 50, y0 + 2), (x0 + 30, y0 + 3), (x0 + 6, y0 + 3)])
            c.put(m, K['teal3'], 0.35)
    # far leaves tumbling across the sky (dark, no light)
    rng = np.random.default_rng(303)
    for i in range(4):
        u = frac(t * (1 + i % 2) + rng.random())
        x = 88 - u * 100 + rng.uniform(0, 12)
        y = rng.uniform(6, 40) + u * 10 + math.sin(u * math.pi * 4) * 2
        m = c.pixel(x, y) | c.pixel(x + (1 if int(u * 12) % 2 else 0), y + 1)
        c.put(m, KR['leaf0'])
    # low wall at the bottom
    wall = c.rect(0, 58, AW - 1, AH - 1)
    for x in range(-2, AW, 9):
        wall |= c.rect(x, 56, x + 6, 57)
    c.put(wall, K['roof'])
    c.put(outline_mask(wall) & (c.y < 59), K['teal2'])


def pinchazo_leaf(f):
    """Leaf position, angle and alpha (None when not on screen)."""
    along = 16.0
    ux, uy = dir_of(P_ANG)
    tx, ty = tip_of(P_HOME[0] + ux * along, P_HOME[1] + uy * along, P_ANG, P_S)
    if f <= 10:
        k = (f + 1) / 11.0
        x = tx + 10 * (1 - k) + math.sin(k * math.pi * 3) * 4 * (1 - k)
        y = -6 + (ty - 1 + 6) * k
        return x, y, math.sin(k * math.pi * 3) * 0.9 + 0.3, 1.0
    if f <= 15:
        along = pinchazo_along(f)
        tx, ty = tip_of(P_HOME[0] + ux * along, P_HOME[1] + uy * along, P_ANG, P_S)
        return tx - ux * 3, ty - uy * 3, 1.25, 1.0
    a = f - 15
    along = pinchazo_along(15)
    rx, ry = tip_of(P_HOME[0] + ux * along, P_HOME[1] + uy * along, P_ANG, P_S)
    x = rx + a * 1.3 + math.sin(a * 0.9) * 2.5
    y = ry + a * 2.6 + 0.08 * a * a
    alpha = 1.0 if f < 19 else max(0.0, 1 - (f - 18) / 5.0)
    return x, y, 1.25 + math.sin(a * 0.8) * 0.9, alpha


def pinchazo_frame(f):
    t = f / FRAMES
    c, g = Canvas(AW, AH), Canvas(AW, AH)
    pinchazo_scene(c, g, t)
    ux, uy = dir_of(P_ANG)
    vx, vy = -uy, ux
    along = pinchazo_along(f)
    bob = math.sin(t * math.pi * 4) * 0.8 if (f <= 7 or f >= 21) else 0.0
    tremble = (1 if f % 2 else -1) * 0.6 if 8 <= f <= 10 else 0.0
    ang = P_ANG - (0.22 if f == 16 else 0.1 if f == 17 else 0.0)
    gx = round(P_HOME[0] + ux * along + vx * tremble)
    gy = round(P_HOME[1] + uy * along + vy * tremble + bob)
    if 11 <= f <= 13:
        k = {11: 1.0, 12: 0.6, 13: 0.25}[f]
        tx, ty = tip_of(gx, gy, ang, P_S)
        speed_lines(c, g, tx - ux * 6, ty - uy * 6, ux, uy, k, 61, count=6, spread=6, length=(7, 14), back=4)
    glint = -2 + (f - 2) * 10.0 if 2 <= f <= 6 else (-2 + (f - 17) * 11.0 if 17 <= f <= 21 else None)
    charge = 9 <= f <= 13
    dagger(c, g, gx, gy, ang, P_S, t * 2 + (1.5 if charge else 0), glint, rune_gain=1.8 if charge else 1.0, wb0=3.2)
    shimmer(c, g, f, gx, gy, ang, P_S, 71, count=4)
    lx, ly, la, lal = pinchazo_leaf(f)
    if lal > 0:
        paint_leaf(c, g, lx, ly, la, 1.3, lal)
    if 11 <= f <= 15:                                         # the blade pokes through the skewered leaf
        dagger(c, g, gx, gy, ang, P_S, t * 2 + 1.5, None, rune_gain=1.6, wb0=3.2)
        body, stem, u, v = leaf_mask(c, lx, ly, la, 1.3)
        # repaint the leaf over the blade except where the blade crosses it (the hole)
        blade_line = c.line(gx, gy, *tip_of(gx, gy, ang, P_S))
        hole = dilate(blade_line) & body
        c.put(body & ~hole, KR['leaf2'])
        c.put(body & ~hole & (v < -0.4), KR['leaf3'])
        c.put(body & ~hole & (v > 0.8), KR['leaf1'])
        c.put(hole & ~blade_line, KR['leaf0'])
    tx, ty = tip_of(gx, gy, ang, P_S)
    if f == 10:
        star4(c, g, tx + 1, ty, 1, gain=0.6)
    elif 11 <= f <= 14:
        star4(c, g, tx + 1, ty, {11: 7, 12: 5, 13: 3, 14: 2}[f], gain=1.4)
    sparks(c, g, f, 11, tx, ty, 63, 10, (1.2, 2.8), 6, CYAN_SPARKS, L['cyan'], gravity=0.14, spread=(-1.2, 1.6))
    # leaf crumbs knocked off by the prick
    rng = np.random.default_rng(65)
    for i in range(5):
        age = f - 11
        if 0 < age <= 7:
            a0 = rng.uniform(0.4, 2.6)
            sp = rng.uniform(0.6, 1.4)
            px = tx + math.cos(a0) * sp * age
            py = ty - math.sin(a0) * sp * age + 0.18 * age * age
            m = c.pixel(px, py)
            c.put(m, KR['leaf3'] if i % 2 else KR['leaf2'], 1 - age / 8)
    motes(c, g, t, 67, 8, (2, 8, 86, 54), K['cyan3'], K['steel3'], L['cyan'], gain=0.55)
    vignette(c, 0.72)
    return c, g


# ---------------------------------------------------------------------------------------- Tajo Veloz
T_HOME = (17.0, 40.0)
T_ANG = -0.30
T_S = 0.7
T_C, T_R = (46.0, 60.0), 36.0
T_HEADS = {11: math.radians(112), 12: math.radians(52), 13: math.radians(18)}
T_HIT = (T_C[0] + math.cos(math.radians(18)) * T_R, T_C[1] - math.sin(math.radians(18)) * T_R)


def tajo_scene(c, g, t):
    gradient_bg(c, K['night0'], K['night2'], K['night1'], 0.58)
    c.put(c.rect(0, 30, AW - 1, 50), K['night3'], np.clip((c.y - 30) / 18.0, 0, 1) * 0.8)   # glow on the horizon
    twinkling_stars(c, g, t, 401, 22, (2, 2, 86, 30))
    # wind-swept cloud streaks (whole cycles per loop, right to left)
    rng = np.random.default_rng(403)
    for i in range(6):
        y = rng.uniform(6, 30)
        ln = rng.uniform(16, 30)
        x = (rng.uniform(0, 120) - t * 120 * (1 + i % 2)) % 120 - 16
        m = c.rect(int(x), int(y), int(x + ln), int(y))
        m |= c.rect(int(x + 4), int(y) + 1, int(x + ln * 0.7), int(y) + 1)
        c.put(m, KR['cloud1'] if i % 2 else KR['cloud0'])
    # far rooftops with lit windows, then the near tiled roof she runs on
    far = c.poly([(0, 46), (6, 46), (6, 41), (12, 37), (18, 41), (18, 44), (30, 44), (30, 40), (40, 40), (40, 43),
                  (52, 43), (58, 38), (64, 43), (76, 43), (76, 39), (82, 39), (82, 44), (88, 44), (88, 64), (0, 64)])
    c.put(far, K['roof2'])
    for i, (wx, wy) in enumerate(((9, 43), (34, 42), (60, 41), (79, 41))):
        lit = 0.5 + 0.5 * math.sin((t * (1 + i % 2) + i * 0.41) * math.pi * 2)
        m = c.rect(wx, wy, wx, wy + 1)
        c.put(m, K['window'] if lit > 0.3 else K['window2'])
        g.add(m, L['warm'], 0.25 * lit)
    roof = c.poly([(0, 53), (34, 49), (60, 49), (88, 52), (88, 64), (0, 64)])
    c.put(roof, KR['tile1'])
    for y in range(51, 64, 3):                                       # scalloped tile rows
        row = roof & (np.abs(c.y - y) < 0.6) & ((np.floor(c.x / 3) + y) % 2 == 0)
        c.put(row, KR['tile2'])
    edge = roof & ~shift(roof, 0, 1)
    c.put(outline_mask(roof) & (c.y < 53), K['ink'])
    c.put(edge, K['moon0'])


def tajo_pose(f, t):
    """Guard position, blade angle and whether it is on the slash arc."""
    if f <= 7 or f >= 23:
        bob = math.sin(t * math.pi * 4) * 0.8
        return T_HOME[0], T_HOME[1] + bob, T_ANG
    if f <= 10:
        k = ease_out((f - 7) / 3.0)
        return T_HOME[0] - 4 * k, T_HOME[1] + 4 * k, T_ANG + (-1.0 - T_ANG) * k
    if f <= 15:
        th = T_HEADS[min(f, 13)]
        da = math.pi / 2 - th
        hx, hy = T_C[0] + math.cos(th) * T_R, T_C[1] - math.sin(th) * T_R
        return hx - math.cos(da) * 40 * T_S, hy - math.sin(da) * 40 * T_S, da
    gx0, gy0, a0 = tajo_pose(15, t)
    k = ease_in_out((f - 15) / 8.0)
    da = ((T_ANG - a0 + math.pi) % (2 * math.pi)) - math.pi
    return gx0 + (T_HOME[0] - gx0) * k, gy0 + (T_HOME[1] - gy0) * k - math.sin(k * math.pi) * 8, a0 + da * k


def tajo_veloz_frame(f):
    t = f / FRAMES
    c, g = Canvas(AW, AH), Canvas(AW, AH)
    tajo_scene(c, g, t)
    cpal = (K['cyan0'], K['cyan2'], K['cyan3'], K['cyan4'])
    if 11 <= f <= 13:
        crescent(c, g, T_C[0], T_C[1], T_R, T_HEADS[f], 2.2, -1, 6.0, cpal, L['cyan'], a=1.0, gain=1.6)
        # after-images where the dagger was a moment ago
        prev = [T_HEADS[k] for k in range(11, f)] or [math.radians(150)]
        for i, th in enumerate(prev[-2:]):
            arc_dagger(c, g, T_C[0], T_C[1], T_R, th, T_S, t, ghost=(K['violet3'], 0.55 + 0.2 * i, L['violet']))
        ux, uy = math.cos(math.pi / 2 - T_HEADS[f]), math.sin(math.pi / 2 - T_HEADS[f])
        hx, hy = T_C[0] + math.cos(T_HEADS[f]) * T_R, T_C[1] - math.sin(T_HEADS[f]) * T_R
        speed_lines(c, g, hx, hy, ux, uy, {11: 1.0, 12: 0.8, 13: 0.5}[f], 411, count=7, spread=7, length=(8, 16), back=10)
    elif 14 <= f <= 18:
        q = (f - 13) / 6.0
        crescent(c, g, T_C[0], T_C[1], T_R, T_HEADS[13], 2.2 * (1 - q * 0.6), -1, 6.0 * (1 - q), cpal, L['cyan'],
                 a=1 - q, gain=1.3)
    if 8 <= f <= 10:
        gx, gy, ang = tajo_pose(f, t)
        converge(c, g, f, *tip_of(gx, gy, ang, T_S), 7, 4, 413, 10, 14, (K['cyan4'], K['cyan2']), L['cyan'])
    gx, gy, ang = tajo_pose(f, t)
    glint = -2 + (f - 1) * 10.0 if 1 <= f <= 5 else None
    charge = 9 <= f <= 14
    dagger(c, g, round(gx), round(gy), ang, T_S, t * 2 + (1.5 if charge else 0), glint, rune_gain=2.0 if charge else 1.0)
    if f <= 7 or f >= 22:
        shimmer(c, g, f, gx, gy, ang, T_S, 415, count=4)
    if 13 <= f <= 16:
        star4(c, g, T_HIT[0], T_HIT[1], {13: 8, 14: 6, 15: 3, 16: 2}[f], gain=1.4)
    sparks(c, g, f, 13, T_HIT[0], T_HIT[1], 417, 12, (1.0, 2.8), 7, CYAN_SPARKS, L['cyan'], gravity=0.15)
    # Combo: a blue mana drop rises out of the cut and bursts into motes
    if 15 <= f <= 20:
        k = ease_out((f - 15) / 5.0)
        y = T_HIT[1] - 4 - 18 * k
        x = T_HIT[0] - 6 + math.sin(k * math.pi * 2) * 1.2
        mana_drop(c, g, round(x), round(y), 0.8 + 0.5 * min(1.0, (f - 14) / 2.0), gain=1.0 + 0.5 * (f == 20))
    if f == 20:
        star4(c, g, T_HIT[0] - 6, T_HIT[1] - 22, 3, col=KR['mana3'], edge=KR['mana2'], light=LR['mana'], gain=1.0)
    sparks(c, g, f, 20, T_HIT[0] - 6, T_HIT[1] - 22, 419, 9, (0.5, 1.5), 5, (KR['mana3'], KR['mana2'], KR['mana1']),
           LR['mana'], gravity=-0.02)
    motes(c, g, t, 421, 8, (2, 6, 86, 50), K['cyan3'], K['violet3'], L['cyan'], gain=0.55)
    vignette(c, 0.7)
    return c, g


# ---------------------------------------------------------------------------------------- Lanzar la Daga
D_HOME = (19.0, 41.0)        # centre of the dagger at home
D_ANG = -0.62
D_S = 0.62
D_HIT = (67.0, 27.0)         # where the blade goes into the poster
D_STUCK = 0.32
D_CUT = 23.0                 # how much blade still shows (local units past the guard)


def brick_wall(c, g, t, lamp=(12, 9)):
    """Night alley wall. The lantern warms the bricks near it in three steps (whole bricks, no noise)."""
    lx, ly = lamp
    fl = 0.85 + 0.15 * math.sin(t * math.pi * 2 * 3) * math.sin(t * math.pi * 2 * 2 + 1)
    c.put(np.ones((c.h, c.w), bool), KR['brick0'])
    rng = np.random.default_rng(501)
    cool = (KR['brick1'], KR['brick2'])
    warm = ((hexc('#241430'), hexc('#2f1a36')), (hexc('#32192f'), hexc('#41223a')), (hexc('#45222f'), hexc('#5a2e36')))
    for row in range(0, 13):
        y0 = row * 5 - 1
        off = 0 if row % 2 == 0 else 6
        for col in range(-1, 9):
            x0 = col * 12 + off
            m = c.rect(x0, y0, x0 + 10, y0 + 3)
            d = math.hypot(x0 + 5 - lx, (y0 + 1.5 - ly) * 1.2)
            band = 2 if d < 11 else 1 if d < 20 else 0 if d < 30 else -1
            pair = cool if band < 0 else warm[band]
            tone = int(rng.integers(0, 3))
            base, top = (pair[0], pair[1]) if tone else (pair[1], pair[1])
            c.put(m, base)
            c.put(m & (c.y < y0 + 1) & (c.x < x0 + 8), top)
    # bracket and lantern
    c.put(c.rect(lx - 1, 0, lx, ly - 4), KR['iron1'])
    cage = c.rect(lx - 3, ly - 3, lx + 2, ly + 4)
    c.put(outline_mask(cage), K['ink'])
    c.put(cage, KR['iron1'])
    glass = c.rect(lx - 2, ly - 2, lx + 1, ly + 3)
    c.put(glass, KR['window'])
    c.put(c.rect(lx - 1, ly, lx, ly + 2), KR['spark2'])
    g.add(glass, L['warm'], 0.7 * fl)
    g.add(c.rect(lx - 1, ly, lx, ly + 2), L['warm'], 0.6 * fl)
    c.put(c.rect(lx - 3, ly - 4, lx + 2, ly - 4) | c.rect(lx - 3, ly + 5, lx + 2, ly + 5), KR['iron2'])
    # floor
    floor = c.rect(0, 58, AW - 1, AH - 1)
    c.put(floor, K['roof'])
    c.put(c.rect(0, 58, AW - 1, 58), KR['brick2'])


def poster(c, g, jolt=0):
    x0, y0, x1, y1 = 57 + jolt, 12, 79 + jolt, 41
    m = c.rect(x0, y0, x1, y1)
    for x in range(x0, x1 + 1, 3):                                   # torn hem
        if (x * 7) % 5 < 2:
            m &= ~c.rect(x, y1, x + 1, y1)
    m &= ~c.rect(x1 - 2, y0, x1, y0 + 1)                            # a dog-eared corner
    c.put(outline_mask(m), K['ink'])
    c.put(m, KR['paper2'])
    c.put(m & (c.x > x1 - 3), KR['paper1'])
    c.put(m & (c.y < y0 + 2) & (c.x < x1 - 3), KR['paper3'])
    c.put(m & ((c.x - x0 + c.y * 0.5) % 9 < 1), KR['paper1'], 0.5)
    # the mark: a crude crimson X over three lines of "text"
    for yy in (33, 35, 37):
        c.put(c.rect(x0 + 4, yy, x1 - 5 - (yy % 3), yy), KR['paper0'])
    x = c.line(x0 + 6, y0 + 6, x0 + 15, y0 + 16) | c.line(x0 + 15, y0 + 6, x0 + 6, y0 + 16)
    x |= shift(x, 1, 0)
    c.put(x, KR['crim3'])
    c.put(x & ~shift(x, 0, -1), KR['crim4'])
    for nx in (x0 + 2, x1 - 4):
        c.put(c.rect(nx, y0 + 1, nx, y0 + 1), KR['iron3'])


def lanzar_pose(f, t):
    """(cx, cy, angle) of the dagger's middle, or None while stuck (drawn separately)."""
    if f <= 4 or f >= 20:
        return D_HOME[0], D_HOME[1] + math.sin(t * math.pi * 4) * 0.8, D_ANG
    if f <= 7:
        k = ease_out((f - 4) / 3.0)
        return D_HOME[0] - 4 * k, D_HOME[1] + 2 * k, D_ANG - 1.4 * k
    if f <= 11:
        k = (f - 7) / 4.5
        hx, hy = D_HIT
        # the middle flies to where it will sit once stuck
        ux, uy = dir_of(D_STUCK)
        ex, ey = hx - ux * (D_CUT - 10) * D_S, hy - uy * (D_CUT - 10) * D_S
        x = D_HOME[0] - 2 + (ex - D_HOME[0] + 2) * k
        y = D_HOME[1] + (ey - D_HOME[1]) * k - math.sin(k * math.pi) * 9
        return x, y, D_STUCK - (11.5 - f) * 2.35
    return None


def lanzar_la_daga_frame(f):
    t = f / FRAMES
    c, g = Canvas(AW, AH), Canvas(AW, AH)
    brick_wall(c, g, t)
    poster(c, g, jolt=1 if f == 12 else 0)
    pose = lanzar_pose(f, t)
    if pose is not None and 8 <= f <= 11:
        # spin blur: the dagger half a frame ago, as a violet after-image, and lines behind it
        cx, cy, ang = pose
        if f >= 9:
            px_, py_, pa_ = lanzar_pose(f - 1, t)
            centred_dagger(c, g, (cx + px_) / 2, (cy + py_) / 2, (ang + pa_) / 2, D_S, t * 2,
                           ghost=(K['violet3'], 0.6, L['violet']))
        speed_lines(c, g, cx - 8, cy, 1.0, -0.12, 0.8, 503 + f, count=5, spread=5, length=(6, 12))
    if pose is not None:
        cx, cy, ang = pose
        glint = -2 + (f - 1) * 10.0 if 1 <= f <= 4 else None
        if f >= 20:                                              # the hidden dagger re-forms at home
            a = min(1.0, (f - 19) / 2.5)
            if f <= 21:
                centred_dagger(c, g, cx, cy, ang, D_S, t * 2, ghost=(K['violet3'], a, L['violet']))
            else:
                centred_dagger(c, g, cx, cy, ang, D_S, t * 2, rune_gain=1.4)
        else:
            centred_dagger(c, g, round(cx), round(cy), ang, D_S, t * 2 + (1.5 if 5 <= f <= 11 else 0), glint,
                           rune_gain=1.8 if 5 <= f <= 11 else 1.0)
        if f <= 4:
            ux, uy = dir_of(ang)
            shimmer(c, g, f, cx - ux * 10 * D_S, cy - uy * 10 * D_S, ang, D_S, 505, count=4)
    else:
        # stuck in the poster: quivering about the entry point, then melting into violet smoke
        q = f - 12
        ang = D_STUCK + 0.2 * math.sin(q * 2.3) * math.exp(-q * 0.55)
        ux, uy = dir_of(ang)
        gx_, gy_ = D_HIT[0] - ux * D_CUT * D_S, D_HIT[1] - uy * D_CUT * D_S
        if f <= 16:
            dagger(c, g, gx_, gy_, ang, D_S, t * 2, None, rune_gain=1.5, cut=D_CUT)
        elif f <= 19:
            a = 1 - (f - 16) / 4.0
            dagger(c, g, gx_, gy_, ang, D_S, t * 2, ghost=(K['violet3'], a, L['violet']), cut=D_CUT)
        slit = c.line(D_HIT[0], D_HIT[1] - 1, D_HIT[0], D_HIT[1] + 1)
        c.put(slit, K['ink'])
    if f == 12:
        star4(c, g, D_HIT[0] + 1, D_HIT[1], 7, gain=1.4)
    elif f == 13:
        star4(c, g, D_HIT[0] + 1, D_HIT[1], 4, gain=1.0)
    sparks(c, g, f, 12, D_HIT[0], D_HIT[1], 507, 10, (1.0, 2.4), 6, CYAN_SPARKS, L['cyan'], gravity=0.16,
           spread=(0.6, 4.0))
    # paper flecks knocked off the poster
    rng = np.random.default_rng(509)
    for i in range(6):
        age = f - 12
        if 0 < age <= 8:
            a0 = rng.uniform(1.2, 4.2)
            sp = rng.uniform(0.6, 1.6)
            px = D_HIT[0] + math.cos(a0) * sp * age
            py = D_HIT[1] - math.sin(a0) * sp * age + 0.16 * age * age
            c.put(c.pixel(px, py), KR['paper3'] if i % 2 else KR['paper2'], 1 - age / 9)
    # violet smoke drifting home (the dagger hides itself in your draw pile)
    if 16 <= f <= 22:
        rng = np.random.default_rng(511)
        k = (f - 16) / 6.0
        for i in range(14):
            sx = D_HIT[0] - rng.uniform(2, 14)
            sy = D_HIT[1] + rng.uniform(-3, 3)
            e = ease_in_out(np.clip(k * 1.2 - rng.uniform(0, 0.2), 0, 1))
            px = sx + (D_HOME[0] + rng.uniform(-8, 8) - sx) * e
            py = sy + (D_HOME[1] + rng.uniform(-4, 4) - sy) * e - math.sin(e * math.pi) * 10
            a = math.sin(min(1.0, k * 1.1) * math.pi) * 0.9 + 0.1
            m = c.pixel(px, py)
            c.put(m, K['violet3'] if i % 3 else K['violet4'], a)
            g.add(m, L['violet'], 0.7 * a)
    motes(c, g, t, 513, 7, (2, 6, 86, 56), K['violet3'], K['cyan3'], L['violet'], gain=0.5)
    vignette(c, 0.72)
    return c, g



# ---------------------------------------------------------------------------------------- Lluvia de Dagas
R_TARGETS = ((25.0, 50.0), (63.0, 51.0))
R_S = 0.5
R_FALL = 5                    # frames from the rift to the ground
R_STAY = 5                    # frames a dagger stays planted before it melts
# (target, x offset, landing frame): two volleys, one per target (2 enemies al azar)
R_DAGGERS = ((0, -8, 7), (0, 0, 8), (0, 8, 8), (0, -4, 9), (0, 4, 10),
             (1, -8, 13), (1, 1, 14), (1, 8, 14), (1, -4, 15), (1, 5, 16))
R_ANG = math.pi / 2 - 0.16


def sage(f, born):
    """Signed age on the loop: (f - born) wrapped into [-12, 12)."""
    return (f - born + 12) % FRAMES - 12


def rift(c, g, t, glow):
    cx, cy, rx, ry = 44.0, 8.0, 33.0, 5.5
    c.put(c.disc(cx, cy, 30) & (c.y < 24), K['vio3'], 0.2 + 0.2 * glow)
    body = (((c.x - cx) / rx) ** 2 + ((c.y - cy) / ry) ** 2) <= 1
    c.put(body, K['vio0'])
    c.put(body & ((((c.x - cx) / (rx * 0.75)) ** 2 + ((c.y - cy) / (ry * 0.6)) ** 2) <= 1), K['ink'])
    ang = np.arctan2((c.y - cy) / ry, (c.x - cx) / rx)
    for i, (rr, w, col) in enumerate(((1.0, 0.1, K['violet2']), (0.8, 0.09, K['violet1']))):
        d = np.hypot((c.x - cx) / rx, (c.y - cy) / ry)
        ring = (np.abs(d - rr) < w) & (((ang / (2 * math.pi) * 12 + t * (2 if i == 0 else -1) * 3) % 1) < 0.62)
        c.put(ring, col)
        c.put(ring & (c.y > cy), K['violet3'] if glow > 0.3 else col)
        g.add(ring, L['violet'], 0.25 + 0.6 * glow)


def cobbles(c, y0):
    c.put(c.rect(0, y0, AW - 1, AH - 1), K['vio0'])
    rng = np.random.default_rng(601)
    for row, (y, h, w) in enumerate(((y0 + 1, 2, 6), (y0 + 4, 3, 8), (y0 + 8, 3, 10), (y0 + 12, 4, 12))):
        off = rng.uniform(0, w)
        for x in np.arange(-w + off, AW + w, w + 1):
            m = c.rect(int(x), y, int(x + w - 1), y + h - 1)
            m &= ~(c.rect(int(x), y, int(x), y) | c.rect(int(x + w - 1), y, int(x + w - 1), y))
            c.put(m, K['vio1'] if (int(x) + row) % 3 else K['vio2'])
            c.put(m & (c.y == y + 0.5), K['vio2'])


def sigil(c, g, x, y, rx, ry, t, a, col=None, light=None, spin=1.0):
    col = K['violet2'] if col is None else col
    light = L['violet'] if light is None else light
    ring = ellipse_ring(c, x, y, rx, ry, 0.55)
    ang = np.arctan2((c.y - y) / ry, (c.x - x) / rx)
    dash = ((ang / (2 * math.pi) * 10 + t * spin) % 1) < 0.7
    inner = ellipse_ring(c, x, y, rx * 0.6, ry * 0.6, 0.45) & ~dash
    c.put((ring & dash) | inner, col, a)
    g.add((ring & dash) | inner, light, 0.5 * a)


def lluvia_de_dagas_frame(f):
    t = f / FRAMES
    c, g = Canvas(AW, AH), Canvas(AW, AH)
    gradient_bg(c, K['vio1'], K['vio2'], K['vio0'], 0.6)
    twinkling_stars(c, g, t, 603, 12, (2, 14, 86, 44), light=L['violet'])
    glow = max(0.0, 1 - min(abs(sage(f, 5)), abs(sage(f, 11))) / 3.0)
    rift(c, g, t, glow)
    # far ruined wall
    far = c.poly([(0, 42), (9, 42), (9, 36), (13, 36), (13, 40), (30, 40), (33, 34), (36, 40), (55, 40), (55, 37),
                  (60, 37), (60, 41), (76, 41), (79, 35), (82, 41), (88, 41), (88, 47), (0, 47)])
    c.put(far, K['roof2'])
    c.put(far & ~shift(far, 0, 1), K['vio3'], 0.7)
    cobbles(c, 46)
    for k, (tx, ty) in enumerate(R_TARGETS):
        land = 8 if k == 0 else 14
        sa = sage(f, land)
        hot = 1.0 if -3 <= sa <= 1 else max(0.0, 1 - (sa - 1) / 4.0) if sa > 1 else 0.0
        sigil(c, g, tx, ty, 11, 3.2, t, 0.45 + 0.55 * hot, col=K['violet3'] if hot > 0.5 else K['violet2'])
    # far ghost daggers falling in the back (no light, depth)
    rng = np.random.default_rng(605)
    for i in range(6):
        u = frac(t * 2 + rng.random())
        x = rng.uniform(4, 84)
        y = 8 + u * 34
        m = c.line(x, y, x + 1, y + 5)
        c.put(m, K['vio3'], 0.9)
        c.put(c.pixel(x + 1, y + 5), K['violet1'])
    # the real daggers: fall, plant, melt
    planted = []
    for (k, dx, land) in R_DAGGERS:
        tx, ty = R_TARGETS[k]
        a = sage(f, land)
        ux, uy = dir_of(R_ANG)
        ex, ey = tx + dx, ty - 1 + (abs(dx) % 3) - 1
        if -R_FALL <= a < 0:
            q = (a + R_FALL) / R_FALL
            e = q ** 1.6
            sx, sy = ex - ux * 46, ey - uy * 46
            px, py = sx + (ex - sx) * e, sy + (ey - sy) * e
            gx_, gy_ = px - ux * 40 * R_S, py - uy * 40 * R_S
            if q > 0.2:
                m = c.line(gx_ - ux * 10, gy_ - uy * 10, gx_ - ux * (10 + 10 * q), gy_ - uy * (10 + 10 * q)) & (c.y > 9)
                c.put(m, K['cyan3'] if dx % 2 else K['violet3'], 0.8)
                g.add(m, L['cyan'], 0.35)
            clipped(c, g, c.y > 9, lambda c2, g2: dagger(c2, g2, gx_, gy_, R_ANG, R_S, t * 2, rune_gain=1.6))
        elif 0 <= a <= R_STAY + 3:
            planted.append((ex, ey, a))
    for ex, ey, a in planted:
        ux, uy = dir_of(R_ANG)
        gx_, gy_ = ex - ux * 14 * R_S, ey - uy * 14 * R_S
        if a <= R_STAY:
            dagger(c, g, gx_, gy_, R_ANG, R_S, t * 2, rune_gain=1.2, cut=14)
        else:
            al = 1 - (a - R_STAY) / 4.0
            dagger(c, g, gx_, gy_, R_ANG, R_S, t * 2, ghost=(K['violet3'], al, L['violet']), cut=14)
            motes(c, g, t, 607 + int(ex), 2, (ex - 3, ey - 10, ex + 3, ey), K['violet3'], K['violet4'], L['violet'], gain=0.6)
        c.put(c.rect(int(ex) - 1, int(ey) + 1, int(ex) + 1, int(ey) + 1), K['ink'])
    for k, (tx, ty) in enumerate(R_TARGETS):
        land = 8 if k == 0 else 14
        a = sage(f, land)
        if 0 <= a <= 3:
            star4(c, g, tx, ty - 3, {0: 7, 1: 5, 2: 3, 3: 2}[a], gain=1.4)
        if 0 <= a <= 5:
            k2 = a / 5.0
            ring = ellipse_ring(c, tx, ty, 6 + 12 * ease_out(k2), 2 + 3.5 * ease_out(k2), 0.6)
            c.put(ring, K['cyan3'] if a < 3 else K['cyan1'], 1 - k2)
            g.add(ring, L['cyan'], 0.8 * (1 - k2))
        sparks(c, g, f, land, tx, ty - 2, 611 + k, 12, (1.0, 2.6), 6, CYAN_SPARKS, L['cyan'], gravity=0.2, spread=(0.3, 2.8))
        dust_puffs(c, tx, ty, f, land, 4, 1.6, rise=0.3, life=6, seed=613 + k)
    vignette(c, 0.72)
    return c, g


# ---------------------------------------------------------------------------------------- Cuchillada Errante
E_HOME_TIP = (40.0, 26.0)
E_HOME_ANG = -0.42
E_S = 0.6
E_SIGILS = ((68.0, 13.0), (76.0, 36.0), (52.0, 44.0))
E_FIRST, E_SECOND = 0, 2
E_ROULETTE = {0: 1, 1: 2, 2: 0, 3: 1, 4: 2, 5: 0, 10: 1, 11: 0, 12: 2}


def bez(p0, p1, p2, p3, u):
    a = (1 - u) ** 3
    b = 3 * (1 - u) ** 2 * u
    cc = 3 * (1 - u) * u * u
    d = u ** 3
    return (a * p0[0] + b * p1[0] + cc * p2[0] + d * p3[0], a * p0[1] + b * p1[1] + cc * p2[1] + d * p3[1])


def bez_dir(p0, p1, p2, p3, u):
    x0, y0 = bez(p0, p1, p2, p3, max(0.0, u - 0.02))
    x1, y1 = bez(p0, p1, p2, p3, min(1.0, u + 0.02))
    return math.atan2(y1 - y0, x1 - x0)


E_PATHS = {
    'out': (E_HOME_TIP, (56.0, 46.0), (44.0, 0.0), E_SIGILS[E_FIRST], 6, 9),
    'again': (E_SIGILS[E_FIRST], (80.0, 22.0), (76.0, 52.0), E_SIGILS[E_SECOND], 13, 15),
}


def errante_tip(f, t):
    """Tip position and blade angle (the dagger always flies tip first)."""
    for name, (p0, p1, p2, p3, f0, f1) in E_PATHS.items():
        if f0 <= f <= f1:
            u = (f - f0 + 1) / (f1 - f0 + 1)
            u = ease_in_out(u) if name == 'home' else u ** 0.8
            x, y = bez(p0, p1, p2, p3, u)
            ang = bez_dir(p0, p1, p2, p3, u)
            if name == 'home' and f == f1:
                ang = E_HOME_ANG
            return x, y, ang, name, u
    if 16 <= f <= 22:                                      # back home: a lazy turn over the top
        k = ease_in_out((f - 15) / 7.0)
        a0 = bez_dir(*E_PATHS['again'][:4], 1.0)
        da = ((E_HOME_ANG - a0 + math.pi) % (2 * math.pi)) - math.pi
        x0, y0 = E_SIGILS[E_SECOND]
        x = x0 + (E_HOME_TIP[0] - x0) * k
        y = y0 + (E_HOME_TIP[1] - y0) * k - math.sin(k * math.pi) * 14
        return x, y, a0 + da * k, None, 0
    if 10 <= f <= 12:                                      # recoil in front of the first sigil
        x, y = E_SIGILS[E_FIRST]
        k = (f - 9) / 3.0
        return x - 3 * math.sin(k * math.pi), y + 2 * math.sin(k * math.pi), bez_dir(*E_PATHS['out'][:4], 1.0), None, 0
    bob = math.sin(t * math.pi * 4) * 0.8
    return E_HOME_TIP[0], E_HOME_TIP[1] + bob, E_HOME_ANG, None, 0


def crypt_arches(c):
    gradient_bg(c, K['teal0'], K['vio2'], K['teal1'], 0.6)
    for ax in (-4, 30, 64):
        arch = c.rect(ax, 14, ax + 3, 64) | c.rect(ax + 21, 14, ax + 24, 64)
        arch |= ellipse_ring(c, ax + 12, 14, 12, 10, 1.6) & (c.y < 14)
        c.put(arch, K['roof2'])
        c.put(arch & ~shift(arch, 1, 0), K['vio3'], 0.6)
    c.put(c.rect(0, 56, AW - 1, AH - 1), K['roof'])
    c.put(c.rect(0, 56, AW - 1, 56), K['vio3'])


def cuchillada_errante_frame(f):
    t = f / FRAMES
    c, g = Canvas(AW, AH), Canvas(AW, AH)
    crypt_arches(c)
    for i in range(3):                                     # fog rolling along the floor
        off = (t * 88 * (1 if i % 2 else -1) + i * 31) % 88
        for k in range(-1, 2):
            x0 = off + k * 88
            m = c.poly([(x0, 52 + i * 2), (x0 + 20, 50 + i * 2), (x0 + 44, 52 + i * 2), (x0 + 22, 54 + i * 2)])
            c.put(m, K['teal3'], 0.4)
    # the targets: three sigils; a roulette picks one (al azar), twice with Combo
    for i, (sx, sy) in enumerate(E_SIGILS):
        broken_until = {E_FIRST: (9, 17), E_SECOND: (15, 20)}.get(i)
        a = 0.55
        if broken_until and broken_until[0] <= f < broken_until[1]:
            continue
        if broken_until and broken_until[1] <= f < broken_until[1] + 3:
            a = 0.55 * (f - broken_until[1] + 1) / 3.0
        lit = E_ROULETTE.get(f) == i or (i == E_FIRST and 5 <= f <= 8) or (i == E_SECOND and 12 <= f <= 14)
        locked = (i == E_FIRST and 5 <= f <= 8) or (i == E_SECOND and 12 <= f <= 14)
        col = K['violet4'] if locked else K['violet3'] if lit else K['violet1']
        sigil(c, g, sx, sy, 6.5, 6.5, t, 1.0 if lit else a, col=col, spin=2.0 if lit else 1.0)
        dot = c.disc(sx, sy, 1.2 if locked else 0.8)
        c.put(dot, K['white'] if locked else col, 1.0 if lit else a)
        g.add(dot, L['violet'], 1.0 if lit else 0.3)
        if f in (5, 12) and locked:
            ring = ellipse_ring(c, sx, sy, 9, 9, 0.5)
            c.put(ring, K['violet4'])
            g.add(ring, L['violet'], 0.8)
    x, y, ang, path, u = errante_tip(f, t)
    # wandering trail: a dotted violet ribbon along the stretch of the path just flown
    if path is not None:
        p0, p1, p2, p3, f0, f1 = E_PATHS[path]
        pts = []
        for j in range(24):
            uu = u - j * 0.025
            if uu <= 0:
                break
            pts.append(bez(p0, p1, p2, p3, uu))
        for j, ((x0, y0), (x1, y1)) in enumerate(zip(pts, pts[1:])):
            k = 1 - j / 24
            m = c.line(x0, y0, x1, y1)
            if (j // 2) % 2 == 0:
                c.put(m, K['violet4'] if j < 4 else K['violet3'] if j < 12 else K['violet2'], k)
                g.add(m, L['violet'], 0.7 * k)
    ux, uy = dir_of(ang)
    gx_, gy_ = x - ux * 40 * E_S, y - uy * 40 * E_S
    fast = path in ('out', 'again')
    if fast:
        speed_lines(c, g, gx_, gy_, ux, uy, 0.7, 621 + f, count=4, spread=4, length=(5, 9))
    glint = -2 + (f - 1) * 10.0 if 1 <= f <= 4 else None
    dagger(c, g, round(gx_), round(gy_), ang, E_S, t * 2 + (1.5 if fast else 0), glint, rune_gain=1.9 if fast else 1.0)
    for hit, (sx, sy) in ((9, E_SIGILS[E_FIRST]), (15, E_SIGILS[E_SECOND])):
        a = f - hit
        if 0 <= a <= 3:
            star4(c, g, sx, sy, {0: 7, 1: 5, 2: 3, 3: 2}[a], gain=1.4)
        sparks(c, g, f, hit, sx, sy, 623 + hit, 10, (1.0, 2.4), 6, CYAN_SPARKS, L['cyan'], gravity=0.14)
        sparks(c, g, f, hit, sx, sy, 625 + hit, 8, (0.8, 2.0), 5, VIOLET_SPARKS, L['violet'], gravity=0.05)
    motes(c, g, t, 627, 8, (2, 6, 86, 54), K['violet3'], K['cyan3'], L['violet'], gain=0.5)
    vignette(c, 0.72)
    return c, g


# ---------------------------------------------------------------------------------------- Marcar Objetivo
M_C = (46.0, 30.0)
M_R = 13.0
M_HITS = (10, 13, 16)
M_S = 0.42
M_OFFS = ((-2, -1), (1, 2), (2, -2))


def mark(c, g, t, power, ticks_r, lock_flash=0.0, a=1.0):
    """The hunter's mark: a crimson crosshair ring with four ticks and a diamond eye. power 0..1 brightens it."""
    cx, cy = M_C
    d = np.hypot(c.x - cx, c.y - cy)
    ang = np.arctan2(c.y - cy, c.x - cx)
    gaps = np.abs(((ang + math.pi / 4) % (math.pi / 2)) - math.pi / 4) < 0.22
    ring = (np.abs(d - M_R) < 0.75) & ~gaps
    dash = (((ang / (2 * math.pi)) * 16 + t * 2) % 1) < 0.55
    outer = (np.abs(d - (M_R + 4)) < 0.55) & dash & ~gaps
    hi = power > 0.5
    c.put(outer, KR['crim2'] if not hi else KR['crim3'], a * (0.5 + 0.5 * power))
    c.put(ring, KR['crim3'], a)
    c.put(ring & (c.y < cy - 3) & (c.x < cx + 3), KR['crim4'] if not hi else KR['crim5'], a)
    g.add(ring, LR['crimson'], a * (0.3 + 0.9 * power))
    g.add(outer, LR['crimson'], a * 0.2 * (0.5 + power))
    if ticks_r is not None:
        for k in range(4):
            th = k * math.pi / 2
            ux, uy = math.cos(th), math.sin(th)
            m = c.line(cx + ux * ticks_r, cy + uy * ticks_r, cx + ux * (ticks_r + 6), cy + uy * (ticks_r + 6))
            m |= shift(m, int(round(-uy)), int(round(ux)))
            c.put(outline_mask(m), K['ink'], a)
            c.put(m, KR['crim4'], a)
            c.put(m & (np.hypot(c.x - cx, c.y - cy) < ticks_r + 1.6), KR['crim5'], a)
            g.add(m, LR['crimson'], a * (0.5 + 0.6 * power))
    rot = t * math.pi * 2
    dx, dy = c.x - cx, c.y - cy
    rx, ry = dx * math.cos(rot) + dy * math.sin(rot), -dx * math.sin(rot) + dy * math.cos(rot)
    eye = (np.abs(rx) + np.abs(ry)) <= 3.6
    edge = eye & ~erode(eye)
    c.put(eye, KR['crim1'], a)
    c.put(edge, KR['crim4'], a)
    core = c.disc(cx, cy, 1.0 + 0.6 * power)
    c.put(core, K['white'] if power > 0.3 else KR['crim5'], a)
    g.add(edge, LR['crimson'], a * (0.4 + 0.6 * power))
    g.add(core, L['white'], a * (0.4 + power))
    if lock_flash > 0:
        pulse = np.abs(d - (M_R + 2 + 9 * (1 - lock_flash))) < 0.7
        c.put(pulse, KR['crim5'], lock_flash)
        g.add(pulse, LR['crimson'], 0.9 * lock_flash)


def marcar_objetivo_frame(f):
    t = f / FRAMES
    c, g = Canvas(AW, AH), Canvas(AW, AH)
    gradient_bg(c, K['night0'], K['vio2'], K['night1'], 0.6)
    c.put(c.disc(*M_C, 30), KR['crim0'], 0.35)
    c.put(c.disc(*M_C, 21), KR['crim0'], 0.6)
    twinkling_stars(c, g, t, 701, 12, (2, 2, 86, 40), avoid=(M_C[0], M_C[1], 22))
    # a ruined watchtower and battlements in silhouette: something out there is being hunted
    town = c.poly([(0, 50), (6, 50), (6, 38), (9, 35), (12, 38), (12, 50), (24, 50), (24, 46), (28, 46), (28, 50),
                   (70, 50), (70, 44), (74, 44), (74, 50), (80, 50), (80, 41), (83, 38), (86, 41), (88, 41), (88, 64), (0, 64)])
    c.put(town, K['roof'])
    c.put(town & ~shift(town, 0, 1), K['vio3'], 0.7)
    c.put(c.rect(9, 41, 9, 42), K['window'])
    g.add(c.rect(9, 41, 9, 42), L['warm'], 0.3)
    hits_done = sum(1 for h in M_HITS if 0 <= f - h <= 6)
    if f <= 2:
        ticks, power, flash = None, 0.15, 0.0
    elif f <= 7:
        k = ease_out((f - 2) / 5.0)
        ticks, power, flash = 26 - 17 * k, 0.15 + 0.35 * k, 0.0
    elif f <= 19:
        ticks, power = 9.0, min(1.0, 0.55 + 0.15 * hits_done)
        flash = max(0.0, 1 - (f - 7) / 3.0)
    else:
        k = ease_in_out((f - 19) / 4.0)
        ticks, power, flash = 9 + 15 * k, 0.6 * (1 - k) + 0.15 * k, 0.0
    tick_a = 1.0 if f <= 20 else max(0.0, 1 - (f - 20) / 3.0)
    mark(c, g, t, power, None, flash)
    if ticks is not None and tick_a > 0:
        mark(c, g, t, power, ticks, 0.0, a=tick_a) if tick_a >= 1 else None
        if tick_a < 1:
            # fading ticks: dithered
            cx, cy = M_C
            for k in range(4):
                th = k * math.pi / 2
                m = c.line(cx + math.cos(th) * ticks, cy + math.sin(th) * ticks,
                           cx + math.cos(th) * (ticks + 6), cy + math.sin(th) * (ticks + 6))
                c.put(m, KR['crim3'], tick_a)
                g.add(m, LR['crimson'], 0.4 * tick_a)
    # three thrown daggers, each hit worth +3 (crimson plus marks pop out)
    for i, h in enumerate(M_HITS):
        a = f - h
        ang = -0.38 + 0.12 * i
        ux, uy = dir_of(ang)
        ex, ey = M_C[0] + M_OFFS[i][0], M_C[1] + M_OFFS[i][1]
        if -2 <= a < 0:
            q = (a + 3) / 3.0
            px, py = ex - ux * 60 * (1 - q), ey - uy * 60 * (1 - q)
            gx_, gy_ = px - ux * 40 * M_S, py - uy * 40 * M_S
            speed_lines(c, g, gx_, gy_, ux, uy, 0.9, 703 + i, count=4, spread=4, length=(6, 12))
            dagger(c, g, gx_, gy_, ang, M_S, t * 2, rune_gain=1.6)
        elif 0 <= a <= 19 - h + 2 and f <= 21:
            gx_, gy_ = ex - ux * 32 * M_S, ey - uy * 32 * M_S
            if f <= 19:
                dagger(c, g, gx_, gy_, ang, M_S, t * 2, rune_gain=1.3, cut=32)
            else:
                dagger(c, g, gx_, gy_, ang, M_S, t * 2, ghost=(KR['crim4'], 1 - (f - 19) / 3.0, LR['crimson']), cut=32)
        if 0 <= a <= 2:
            star4(c, g, ex, ey, {0: 6, 1: 4, 2: 2}[a], gain=1.3)
        if 0 <= a <= 6:                                  # "+" marks rising: the bonus damage of the mark
            for j in range(2):
                px = ex + (j * 2 - 1) * (4 + a * 0.8) + (i - 1) * 2
                py = ey - 3 - a * 1.6 - j
                k = 1 - a / 7.0
                m = c.pixel(px, py) | c.pixel(px - 1, py) | c.pixel(px + 1, py) | c.pixel(px, py - 1) | c.pixel(px, py + 1)
                c.put(m, KR['crim5'] if a < 3 else KR['crim4'], k)
                g.add(m, LR['crimson'], 0.8 * k)
        sparks(c, g, f, h, ex, ey, 705 + i, 7, (0.8, 2.0), 5, CYAN_SPARKS, L['cyan'], gravity=0.12)
    if 19 <= f <= 23:
        motes(c, g, t, 707, 10, (M_C[0] - 14, M_C[1] - 18, M_C[0] + 14, M_C[1] + 6), KR['crim4'], K['violet3'],
              LR['crimson'], gain=0.6)
    motes(c, g, t, 709, 7, (2, 6, 86, 54), K['violet3'], KR['crim4'], L['violet'], gain=0.45)
    vignette(c, 0.72)
    return c, g


# ---------------------------------------------------------------------------------------- Abrir la Guardia
A_C = (61.0, 31.0)
A_R = 14.0
A_HOME = (12.0, 38.0)
A_ANG = -0.2
A_S = 0.72
A_HIT = 9
KB = {'b0': hexc('#0f2550'), 'b1': hexc('#2756a8'), 'b2': hexc('#4f95e6'), 'b3': hexc('#c2e4ff')}
LB = np.array([0.35, 0.65, 1.0, 1])
A_CRACKS = (((0, 0), (-4, -3), (-7, -2), (-11, -6)), ((0, 0), (3, -5), (2, -9), (5, -13)),
            ((0, 0), (5, 2), (9, 1), (12, 4)), ((0, 0), (-2, 5), (-5, 8), (-4, 12)), ((0, 0), (2, 4), (6, 9)))
A_PIECES = ((-0.8, 0), (0.75, 1), (2.4, 2), (-2.4, 3))     # mid angle of each wedge, id


def round_shield(c, g, cx, cy, r, alpha=1.0, wedge=None, cracks=0.0):
    """An enemy's round wooden shield: planks, iron rim and boss. wedge=(a0, a1) keeps one sector only."""
    dx, dy = c.x - cx, c.y - cy
    d = np.hypot(dx, dy)
    m = d <= r
    if wedge is not None:
        ang = np.arctan2(dy, dx)
        a0, a1 = wedge
        m &= ((ang - a0) % (2 * math.pi)) < ((a1 - a0) % (2 * math.pi))
    light = -(dx * 0.7 + dy * 0.7)
    c.put(outline_mask(m), K['ink'], alpha)
    planks = m & (d <= r - 2)
    c.put(planks, KR['wood1'], alpha)
    c.put(planks & (light > 2), KR['wood2'], alpha)
    c.put(planks & ((np.floor((dx + r) / 4.5) % 2) == 0), KR['wood0'], alpha * 0.55)
    c.put(planks & (np.abs(((dx + r) % 4.5) - 0.2) < 0.5), KR['wood0'], alpha)
    rim = m & (d > r - 2)
    c.put(rim, KR['iron2'], alpha)
    c.put(rim & (light > 3), KR['iron3'], alpha)
    c.put(rim & (light < -6), KR['iron1'], alpha)
    band = m & (np.abs(dy) < 1.1) & (d < r - 2)
    c.put(band, KR['iron1'], alpha)
    c.put(band & (dy < 0), KR['iron2'], alpha)
    boss = m & (d <= 3.6)
    c.put(boss, K['steel1'], alpha)
    c.put(boss & (light > 0.5), K['steel2'], alpha)
    c.put(boss & c.disc(cx - 1.2, cy - 1.2, 1.1), K['steel4'], alpha)
    for k in range(8):
        th = k * math.pi / 4 + math.pi / 8
        rv = c.pixel(cx + math.cos(th) * (r - 1), cy + math.sin(th) * (r - 1)) & m
        c.put(rv, K['steel3'], alpha)
    if cracks > 0:
        for path in A_CRACKS:
            n = max(1, int(round(cracks * (len(path) - 1))))
            for (x0, y0), (x1, y1) in zip(path[:n], path[1:n + 1]):
                ln = c.line(cx + x0, cy + y0, cx + x1, cy + y1) & m
                c.put(ln, K['ink'], alpha)
                c.put(shift(ln, 1, 0) & m & ~ln, KB['b2'], alpha * 0.8)
                g.add(ln, LB, 0.5 * alpha * cracks)
    return m


def ward(c, g, t, r, a, flash=0.0):
    """Enemy block: a blue barrier ring of light around the shield (the game's block colour)."""
    cx, cy = A_C
    d = np.hypot(c.x - cx, c.y - cy)
    ang = np.arctan2(c.y - cy, c.x - cx)
    pat = (((ang / (2 * math.pi)) * 18 + t * 3) % 1) < 0.6
    ring = (np.abs(d - r) < 0.6) & pat
    ring2 = (np.abs(d - (r - 2)) < 0.5) & ~pat
    col = K['white'] if flash > 0.5 else KB['b3'] if flash > 0 else KB['b2']
    c.put(ring, col, a)
    c.put(ring2, KB['b1'], a * 0.8)
    g.add(ring, LB, a * (0.5 + flash))
    g.add(ring2, LB, a * 0.25)


def abrir_la_guardia_frame(f):
    t = f / FRAMES
    c, g = Canvas(AW, AH), Canvas(AW, AH)
    gradient_bg(c, K['teal0'], K['teal2'], K['teal1'], 0.6)
    c.put(c.disc(*A_C, 30), K['teal3'], 0.25)
    c.put(c.disc(*A_C, 21), K['teal3'], 0.45)
    # dungeon gate behind: bars
    gate = np.zeros((AH, AW), bool)
    for x in range(38, 86, 6):
        gate |= c.rect(x, 4, x + 1, 56)
    gate |= c.rect(36, 4, 87, 5) | c.rect(36, 26, 87, 27)
    c.put(gate, K['roof2'])
    c.put(gate & ~shift(gate, 1, 0), K['teal3'], 0.6)
    c.put(c.rect(0, 56, AW - 1, AH - 1), K['roof'])
    c.put(c.rect(0, 56, AW - 1, 56), K['teal3'])
    # the guard: whole, cracking, bursting, gone, then rebuilt
    if f < A_HIT or f >= 19:
        rb = 1.0 if f < A_HIT or f >= 22 else (f - 18) / 4.0
        round_shield(c, g, A_C[0], A_C[1], A_R, alpha=rb)
        wr = 18 + (0 if f < A_HIT else 6 * (1 - rb))
        ward(c, g, t, wr, 0.9 * rb)
    elif f <= 10:
        round_shield(c, g, A_C[0], A_C[1], A_R, cracks=(f - A_HIT + 1) / 2.0)
        ward(c, g, t, 18, 1.0, flash=1.0 if f == A_HIT else 0.6)
    else:
        a = f - 10
        for mid, pid in A_PIECES:
            ux, uy = math.cos(mid), math.sin(mid)
            dist = 2 + a * 2.2
            px = A_C[0] + ux * dist + (2 if pid in (0, 1) else 0) * a * 0.3
            py = A_C[1] + uy * dist + 0.25 * a * a
            alpha = 1.0 if a < 5 else max(0.0, 1 - (a - 4) / 4.0)
            if alpha > 0:
                round_shield(c, g, px, py, A_R, alpha=alpha, wedge=(mid - math.pi / 4, mid + math.pi / 4))
        # the barrier shatters into blue shards
        rng = np.random.default_rng(801)
        for i in range(18):
            th = rng.uniform(0, 2 * math.pi)
            sp = rng.uniform(1.4, 3.2)
            r0 = 18
            px = A_C[0] + math.cos(th) * (r0 + sp * a)
            py = A_C[1] + math.sin(th) * (r0 + sp * a) + 0.1 * a * a
            k = 1 - a / 7.0
            if k <= 0:
                continue
            m = c.pixel(px, py) | c.pixel(px + math.cos(th + 1.6), py + math.sin(th + 1.6))
            c.put(m, KB['b3'] if a < 3 else KB['b2'], k)
            g.add(m, LB, 0.8 * k)
        # splinters
        rng = np.random.default_rng(803)
        for i in range(10):
            th = rng.uniform(0, 2 * math.pi)
            sp = rng.uniform(1.0, 2.6)
            px = A_C[0] + math.cos(th) * sp * a
            py = A_C[1] + math.sin(th) * sp * a + 0.3 * a * a
            if a <= 7:
                c.put(c.pixel(px, py) | c.pixel(px + 1, py), KR['wood2'] if i % 2 else KR['iron3'], 1 - a / 8.0)
    # her dagger: wind-up, thrust into the boss, through, back home
    ux, uy = dir_of(A_ANG)
    reach = math.hypot(A_C[0] - A_HOME[0], A_C[1] - A_HOME[1]) - 40 * A_S
    if f <= 5 or f >= 22:
        along, bob = 0.0, math.sin(t * math.pi * 4) * 0.8
    elif f <= 8:
        along, bob = [-2.0, -4.0, -5.0][f - 6], 0.0
    elif f <= 12:
        along, bob = reach + [0.0, 1.0, 5.0, 6.0][f - 9], 0.0
    else:
        along, bob = (reach + 6) * (1 - ease_in_out((f - 12) / 9.0)), 0.0
    ang = math.atan2(A_C[1] - A_HOME[1] - 40 * A_S * uy * 0, A_C[0] - A_HOME[0]) if 9 <= f <= 12 else A_ANG
    ux, uy = dir_of(ang)
    gx_, gy_ = A_HOME[0] + ux * along, A_HOME[1] + uy * along + bob
    if 9 <= f <= 10:
        speed_lines(c, g, gx_, gy_, ux, uy, 1.0 if f == 9 else 0.5, 805, count=6, spread=6, length=(8, 14))
    charge = 6 <= f <= 11
    glint = -2 + (f - 1) * 10.0 if 1 <= f <= 4 else None
    dagger(c, g, round(gx_), round(gy_), ang, A_S, t * 2 + (1.5 if charge else 0), glint, rune_gain=1.9 if charge else 1.0)
    if A_HIT <= f <= A_HIT + 3:
        star4(c, g, A_C[0], A_C[1], {0: 8, 1: 6, 2: 4, 3: 2}[f - A_HIT], gain=1.5)
    sparks(c, g, f, A_HIT, A_C[0], A_C[1], 807, 12, (1.2, 2.8), 6, WARM_SPARKS, L['warm'], gravity=0.18)
    motes(c, g, t, 809, 7, (2, 6, 86, 54), K['cyan3'], KB['b2'], L['cyan'], gain=0.5)
    if A_HIT <= f <= A_HIT + 1:
        shake(c, g, 1 if f == A_HIT else -1, 0)
    vignette(c, 0.72)
    return c, g


# ---------------------------------------------------------------------------------------- Corte y Guardia
G_X = (44.0, 33.0)            # where the two blades cross
G_S = 0.62
G_FRONT = -0.95               # the dagger that cuts (pointing up-right)
G_BACK = -2.2                 # the parrying dagger (pointing up-left)
G_ARC_C, G_ARC_R = (44.0, 46.0), 28.0
G_HEADS = {8: math.radians(18), 9: math.radians(85), 10: math.radians(158)}


def crossed_guard(ang, cross=G_X, at=16.0, s=G_S):
    """Guard position so that the blade passes through the crossing point `at` units from its guard."""
    ux, uy = dir_of(ang)
    return cross[0] - ux * at * s, cross[1] - uy * at * s


def diamond_ring(c, cx, cy, r, w=0.6, squash=0.8):
    d = np.abs(c.x - cx) / r + np.abs(c.y - cy) / (r * squash)
    return np.abs(d - 1) * r <= w


def chapel_window(c, g, t):
    gradient_bg(c, K['teal0'], K['teal2'], K['teal1'], 0.6)
    # tall arched window with bars and moonlight; a dusty beam falls across the floor
    win = c.rect(32, 8, 55, 40) | (c.disc(43.5, 9, 11.6) & (c.y < 9))
    c.put(win, hexc('#1f4766'))
    c.put(win & (c.y > 22), hexc('#18384f'))
    for x in (39, 47):
        c.put(c.rect(x, 0, x, 40) & win, K['teal1'])
    c.put(c.rect(32, 24, 55, 24) & win, K['teal1'])
    c.put(outline_mask(win), K['ink'])
    c.put(dilate(outline_mask(win)) & ~win & ~outline_mask(win), K['teal2'])
    beam = c.poly([(32, 40), (55, 40), (74, 60), (24, 60)])
    c.put(beam, K['teal3'], 0.35)
    rng = np.random.default_rng(901)
    for i in range(9):                                      # dust turning in the light
        u = frac(t + rng.random())
        x = rng.uniform(34, 62) + math.sin((u + i) * math.pi * 2) * 2
        y = 58 - u * 18
        if beam[int(min(63, max(0, y))), int(min(87, max(0, x)))]:
            c.put(c.pixel(x, y), K['moon1'], math.sin(u * math.pi))
    c.put(c.rect(0, 58, AW - 1, AH - 1), K['roof'])
    c.put(c.rect(0, 58, AW - 1, 58), K['teal3'])


def corte_y_guardia_frame(f):
    t = f / FRAMES
    c, g = Canvas(AW, AH), Canvas(AW, AH)
    chapel_window(c, g, t)
    bob = math.sin(t * math.pi * 4) * 0.8 if (f <= 4 or f >= 21) else 0.0
    cross = (G_X[0], G_X[1] + bob)
    # the parrying dagger always holds the guard
    bx, by = crossed_guard(G_BACK, cross)
    pose_back = (bx, by, G_BACK)
    # the cutting dagger: rest, wind-up, slash over the top (right to left), back into the cross
    if f <= 4 or f >= 13:
        if 11 <= f <= 13:
            pass
        fa = G_FRONT
        fx, fy = crossed_guard(fa, cross)
        front = ('dagger', fx, fy, fa)
    if 5 <= f <= 7:
        k = ease_out((f - 4) / 3.0)
        fa = G_FRONT + 1.2 * k
        fx, fy = crossed_guard(fa, (cross[0] + 6 * k, cross[1] + 3 * k))
        front = ('dagger', fx, fy, fa)
    elif 8 <= f <= 10:
        front = ('arc', G_HEADS[f])
    elif 11 <= f <= 12:
        k = ease_in_out((f - 10) / 3.0)
        th = G_HEADS[10]
        a0 = -math.pi / 2 - th + math.pi
        hx, hy = G_ARC_C[0] + math.cos(th) * G_ARC_R, G_ARC_C[1] - math.sin(th) * G_ARC_R
        sx, sy = hx - math.cos(a0) * 40 * G_S, hy - math.sin(a0) * 40 * G_S
        ex, ey = crossed_guard(G_FRONT, cross)
        da = ((G_FRONT - a0 + math.pi) % (2 * math.pi)) - math.pi
        front = ('dagger', sx + (ex - sx) * k, sy + (ey - sy) * k - math.sin(k * math.pi) * 5, a0 + da * k)
    cpal = (K['cyan0'], K['cyan2'], K['cyan3'], K['cyan4'])
    if 8 <= f <= 10:
        crescent(c, g, G_ARC_C[0], G_ARC_C[1], G_ARC_R, G_HEADS[f], 2.0, 1, 5.0, cpal, L['cyan'], a=1.0, gain=1.5)
    elif 11 <= f <= 14:
        q = (f - 10) / 5.0
        crescent(c, g, G_ARC_C[0], G_ARC_C[1], G_ARC_R, G_HEADS[10], 2.0 * (1 - q * 0.6), 1, 5.0 * (1 - q), cpal,
                 L['cyan'], a=1 - q, gain=1.2)
    # the guard: block rises as a diamond ward from the clash (f13), holds, fades
    if 13 <= f <= 22:
        a = f - 13
        k = ease_out(min(1.0, a / 3.0))
        fade = 1.0 if a <= 5 else max(0.0, 1 - (a - 5) / 5.0)
        for i, (r, w) in enumerate(((6 + 15 * k, 0.6), (4 + 11 * k, 0.5))):
            ring = diamond_ring(c, cross[0], cross[1] + 2, r, w)
            shimmer_m = ring & (((c.x + c.y + f * 2) % 7) < 4)
            c.put(ring, KB['b2'] if i == 0 else KB['b1'], fade)
            c.put(shimmer_m, KB['b3'], fade)
            g.add(ring, LB, (0.7 if i == 0 else 0.35) * fade)
        if a <= 3:
            fill = (np.abs(c.x - cross[0]) / (6 + 15 * k) + np.abs(c.y - cross[1] - 2) / ((6 + 15 * k) * 0.8)) <= 1
            c.put(fill & (c.B < 0.25 * (1 - a / 4)), KB['b3'])
    dagger(c, g, round(pose_back[0]), round(pose_back[1]), pose_back[2], G_S, t * 2 + 0.5,
           -2 + (f - 2) * 10.0 if 2 <= f <= 5 else None, rune_gain=1.6 if 13 <= f <= 15 else 1.0)
    if front[0] == 'arc':
        th = front[1]
        a0 = -math.pi / 2 - th + math.pi
        hx, hy = G_ARC_C[0] + math.cos(th) * G_ARC_R, G_ARC_C[1] - math.sin(th) * G_ARC_R
        if f >= 9:
            pth = G_HEADS[f - 1]
            pa = -math.pi / 2 - pth + math.pi
            phx, phy = G_ARC_C[0] + math.cos(pth) * G_ARC_R, G_ARC_C[1] - math.sin(pth) * G_ARC_R
            dagger(c, g, phx - math.cos(pa) * 40 * G_S, phy - math.sin(pa) * 40 * G_S, pa, G_S, t * 2,
                   ghost=(K['violet3'], 0.6, L['violet']))
        dagger(c, g, round(hx - math.cos(a0) * 40 * G_S), round(hy - math.sin(a0) * 40 * G_S), a0, G_S, t * 2 + 1.5,
               rune_gain=2.0)
    else:
        _, fx, fy, fa = front
        glint = -2 + (f - 16) * 10.0 if 16 <= f <= 20 else None
        dagger(c, g, round(fx), round(fy), fa, G_S, t * 2, glint, rune_gain=1.6 if 5 <= f <= 7 or 13 <= f <= 15 else 1.0)
    end = (G_ARC_C[0] + math.cos(G_HEADS[10]) * G_ARC_R, G_ARC_C[1] - math.sin(G_HEADS[10]) * G_ARC_R)
    if 10 <= f <= 12:
        star4(c, g, end[0], end[1], {10: 7, 11: 4, 12: 2}[f], gain=1.3)
    sparks(c, g, f, 10, end[0], end[1], 903, 10, (1.0, 2.4), 6, CYAN_SPARKS, L['cyan'], gravity=0.15)
    if 13 <= f <= 14:                                        # the clash where the blades meet
        star4(c, g, cross[0], cross[1], 5 if f == 13 else 3, core=K['white'], col=KB['b3'], edge=KB['b2'], light=LB, gain=1.3)
    sparks(c, g, f, 13, cross[0], cross[1], 905, 10, (0.8, 2.2), 6, WARM_SPARKS, L['warm'], gravity=0.2)
    motes(c, g, t, 907, 8, (2, 6, 86, 56), K['cyan3'], KB['b2'], L['cyan'], gain=0.5)
    vignette(c, 0.72)
    return c, g


# ---------------------------------------------------------------------------------------- Afilar
F_STONE = (26, 45, 68, 49)    # whetstone on the table (x0, y0, x1, y1)
F_ANG = 0.3                   # the blade leans onto the stone, its point on the top face
F_S = 0.64
F_TIP0, F_TIP1 = 39.0, 65.0   # the point drags across the stone from here to there on each stroke
F_STROKES = ((0, 4), (6, 10), (12, 14))   # frames of each stroke (start, end)


def tavern(c, g, t):
    gradient_bg(c, K['night0'], K['vio1'], K['night1'], 0.6)
    # plank wall
    for x in range(0, AW, 9):
        c.put(c.rect(x, 0, x, 50), K['vio0'])
        c.put(c.rect(x + 1, 0, x + 1, 50), K['vio2'], 0.5)
    # candle on the right: warm pool of light in three steps, flame flickers
    fl = 0.85 + 0.15 * math.sin(t * math.pi * 2 * 4) * math.sin(t * math.pi * 2 * 3 + 0.7)
    cx, cy = 77.0, 36.0
    for r, col in ((24 * fl, hexc('#1f1226')), (15 * fl, hexc('#2c1626')), (8 * fl, hexc('#3d1f28'))):
        c.put(c.disc(cx, cy, r) & (c.y < 50), col)
    for x in range(0, AW, 9):                                   # planks stay readable in the light
        c.put(c.rect(x, 0, x, 50), K['vio0'], 0.8)
    table = c.rect(0, 50, AW - 1, AH - 1)
    c.put(table, KR['wood1'])
    c.put(c.rect(0, 50, AW - 1, 50), KR['wood2'])
    c.put(table & ((c.y.astype(int) % 5) == 3), KR['wood0'])
    c.put(table & c.disc(cx, 51, 14 * fl), KR['wood2'], 0.4)
    wax = c.rect(74, 40, 79, 49)
    c.put(outline_mask(wax), K['ink'])
    c.put(wax, hexc('#c9b8a0'))
    c.put(wax & (c.x > 77), hexc('#8f7d6c'))
    c.put(c.rect(73, 49, 80, 50), KR['iron2'])
    wick = c.rect(76, 38, 76, 39)
    c.put(wick, K['ink'])
    sway = round(math.sin(t * math.pi * 2 * 3) * 0.8)
    flame = c.poly([(76 + sway, 30), (78.5, 36), (77.5, 39), (74.5, 39), (73.5, 36)])
    core = c.poly([(76 + sway * 0.5, 34), (77.3, 37), (76.5, 38.5), (75.5, 38.5), (74.7, 37)])
    c.put(flame, KR['spark1'])
    c.put(core, KR['spark2'])
    g.add(flame, L['warm'], 0.8 * fl)
    g.add(core, L['white'], 0.4 * fl)


def whetstone(c, g, wet):
    x0, y0, x1, y1 = F_STONE
    m = c.rect(x0, y0, x1, y1) & ~c.rect(x0, y0, x0, y0) & ~c.rect(x1, y0, x1, y0)
    c.put(outline_mask(m), K['ink'])
    c.put(m, hexc('#40465c'))
    c.put(m & (c.y < y0 + 1.5), hexc('#6c7590'))
    c.put(m & (c.y > y1 - 1), hexc('#262a3a'))
    c.put(m & ((c.x * 7 + c.y * 3) % 11 < 1), hexc('#2c3142'))
    if wet > 0:                                                 # wet sheen along the top
        sheen = m & (c.y < y0 + 1.5) & (((c.x - wet) % 14) < 3)
        c.put(sheen, K['cyan1'])
    base = c.rect(x0 - 2, y1 + 1, x1 + 2, y1 + 1)
    c.put(base, KR['wood0'])


def afilar_pose(f, t):
    """Guard position, blade angle, whether the point is on the stone, and the stroke number."""
    def at_tip(tx, ty, ang):
        ux, uy = dir_of(ang)
        return tx - ux * 40 * F_S, ty - uy * 40 * F_S
    top = F_STONE[1] - 0.5
    for i, (a, b) in enumerate(F_STROKES):
        if a <= f <= b:
            k = (f - a) / max(1, b - a)
            return (*at_tip(F_TIP0 + (F_TIP1 - F_TIP0) * k, top, F_ANG), F_ANG, True, i)
        if f == b + 1 and i < 2:                                # lifted and carried back for the next stroke
            return (*at_tip((F_TIP0 + F_TIP1) / 2, top - 6, F_ANG - 0.1), F_ANG - 0.1, False, i)
    if 15 <= f <= 19:                                           # raised to show the edge: points up
        k = ease_out(min(1.0, (f - 14) / 3.0))
        tx, ty = F_TIP1 + (54 - F_TIP1) * k, top + (8 - top) * k
        ang = F_ANG + (-1.05 - F_ANG) * k
        return (*at_tip(tx, ty, ang), ang, False, 3)
    k = ease_in_out((f - 19) / 5.0)                             # down again to the start of the first stroke
    tx, ty = 54 + (F_TIP0 - 54) * k, 8 + (top - 8) * k
    ang = -1.05 + (F_ANG + 1.05) * k
    return (*at_tip(tx, ty, ang), ang, False, 3)


def afilar_frame(f):
    t = f / FRAMES
    c, g = Canvas(AW, AH), Canvas(AW, AH)
    tavern(c, g, t)
    whetstone(c, g, wet=(f * 3) % 14)
    gx_, gy_, ang, on_stone, stroke = afilar_pose(f, t)
    # each stroke charges the runes a little more; the flare is the +6 of the next hit
    charge = {0: 1.1, 1: 1.5, 2: 1.9, 3: 2.6}[stroke] if f <= 19 else 2.6 - 1.6 * (f - 19) / 5.0
    glint = -2 + (f - 16) * 11.0 if 16 <= f <= 20 else None
    dagger(c, g, round(gx_), round(gy_), ang, F_S, t * 2 + f * 0.1, glint, rune_gain=charge)
    if on_stone:
        # sparks fly from where the point bites the stone, a scratch of light trails behind it
        cx, cy = tip_of(gx_, gy_, ang, F_S)
        trail = c.rect(int(F_TIP0), F_STONE[1], int(cx), F_STONE[1])
        c.put(trail, KR['spark1'], 0.8)
        g.add(trail, L['warm'], 0.3)
        for back in (0, 1, 2):                                 # sparks of this and the two previous frames
            falling_sparks(c, g, f, f - 1 - back, cx - back * 4, cy, 1001 + f - back, 6, (1.4, 3.0), 6,
                           WARM_SPARKS, L['warm'], floor=50, spread=(-0.1, 1.2), gain=1.1)
        star4(c, g, cx + 1, cy, 2 if f % 2 else 1, core=KR['spark2'], col=KR['spark1'], edge=KR['spark0'],
              light=L['warm'], gain=0.9)
    if f in (17, 18, 19):
        tx, ty = tip_of(gx_, gy_, ang, F_S)
        star4(c, g, tx, ty, {17: 4, 18: 7, 19: 4}[f], gain=1.5)
    if 17 <= f <= 22:
        tx, ty = tip_of(*afilar_pose(18, t)[:3], F_S)
        motes(c, g, t, 1003, 9, (tx - 18, ty - 10, tx + 6, ty + 24), K['cyan3'], K['cyan4'], L['cyan'], gain=0.8)
    motes(c, g, t, 1005, 6, (2, 6, 70, 48), KR['spark1'], K['cyan3'], L['warm'], gain=0.35)
    vignette(c, 0.72)
    return c, g


# ---------------------------------------------------------------------------------------- Carterista
C_HOOK = (58.0, 4.0)
C_PURSE_Y = 26.0
C_CUT = 7
C_LAND = 11
C_FLOOR = 56


def coin(c, g, x, y, phase, a=1.0, gain=0.5):
    """A spinning gold coin: its width follows |cos(phase)|."""
    w = 0.6 + 2.0 * abs(math.cos(phase))
    m = (((c.x - x) / w) ** 2 + ((c.y - y) / 2.6) ** 2) <= 1
    if not m.any():
        m = c.pixel(x, y)
    c.put(outline_mask(m), K['ink'], a)
    c.put(m, KR['gold2'], a)
    c.put(m & (c.x < x - 0.3) & (c.y < y), KR['gold3'], a)
    c.put(m & (c.x > x + 0.6), KR['gold1'], a)
    g.add(m, LW['gold'], gain * a * 0.3)
    return m


def purse(c, g, x, y, sx=1.0, sy=1.0, a=1.0, open_=False, size=1.25):
    """Leather coin purse hanging by its drawstring: (x, y) = the knot at its neck."""
    dx, dy = (c.x - x) / (sx * size), (c.y - y) / (sy * size)
    body = (((dx) / 7.0) ** 2 + ((dy - 8.5) / 7.2) ** 2) <= 1
    neck = (np.abs(dx) <= 2.4 + (dy - 1) * 0.4) & (dy >= 0) & (dy <= 3)
    frill = (np.abs(dx) <= 4.2) & (dy >= -2.2) & (dy < 0) & ((np.floor(dx + 9) % 2) == 0)
    m = body | neck | frill
    c.put(outline_mask(m), K['ink'], a)
    c.put(m, hexc('#5c3424'), a)
    c.put(m & (dx + (dy - 8) * 0.8 < -2), hexc('#83503a'), a)
    c.put(m & (dx + (dy - 8) * 0.8 > 4), hexc('#3a1f18'), a)
    c.put(neck & (np.abs(dy - 1.5) < 0.6), KR['crim3'], a)               # the drawstring
    c.put(body & (np.abs(dx) < 0.6) & (dy > 6) & (dy < 13), hexc('#3a1f18'), a)   # seam
    if open_:
        c.put(frill, KR['gold2'], a)
    return m


def carterista_scene(c, g, t):
    gradient_bg(c, K['night0'], K['vio2'], K['night1'], 0.6)
    # market stalls: striped awnings and hanging lanterns
    for x0, w, cols in ((-4, 30, (KR['crim1'], K['vio1'])), (60, 34, (K['teal2'], K['vio1']))):
        aw = c.poly([(x0, 10), (x0 + w, 10), (x0 + w - 2, 18), (x0 + 2, 18)])
        c.put(aw, cols[0])
        c.put(aw & ((np.floor((c.x - x0) / 4) % 2) == 0), cols[1])
        hem = c.rect(x0 + 2, 18, x0 + w - 2, 19) & ((np.floor(c.x / 2) % 2) == 0)
        c.put(hem, cols[0])
        c.put(outline_mask(aw | hem), K['ink'])
        c.put(c.rect(x0 + 3, 19, x0 + 4, 52) | c.rect(x0 + w - 4, 19, x0 + w - 3, 52), KR['wood0'])
    for i, (lx, ly) in enumerate(((14, 24), (78, 25))):
        fl = 0.8 + 0.2 * math.sin((t * (2 + i) + i * 0.3) * math.pi * 2)
        c.put(c.disc(lx, ly, 9 * fl), hexc('#2c1838'))
        c.put(c.disc(lx, ly, 5.5 * fl), hexc('#3d2036'))
        lamp = c.disc(lx, ly, 2.2)
        c.put(outline_mask(lamp), K['ink'])
        c.put(lamp, K['window'])
        c.put(c.pixel(lx - 0.5, ly - 0.5), KR['spark2'])
        g.add(lamp, L['warm'], 0.7 * fl)
        c.put(c.rect(lx, ly - 5, lx, ly - 3), KR['iron1'])
    # beam the purse hangs from
    beam = c.rect(30, 2, 74, 4)
    c.put(outline_mask(beam), K['ink'])
    c.put(beam, KR['wood1'])
    c.put(c.rect(30, 2, 74, 2), KR['wood2'])
    cobbles(c, 53)


def carterista_frame(f):
    t = f / FRAMES
    c, g = Canvas(AW, AH), Canvas(AW, AH)
    carterista_scene(c, g, t)
    hx, hy = C_HOOK
    sway = math.sin(t * math.pi * 2 * 2) * 0.12             # pendulum, two swings per loop
    rope_len = C_PURSE_Y - hy
    cut_y = hy + 10
    # the purse: hanging, cut down, falling, bursting; a new one is lowered on its string
    if f < C_CUT or f >= 21:
        px, py = hx + math.sin(sway) * rope_len, hy + math.cos(sway) * rope_len
        rope = c.line(hx, hy, px, py)
        c.put(rope, KR['paper1'])
        purse(c, g, px, py)
        if f in (2, 3, 4):
            star4(c, g, px - 3, py + 6, {2: 1, 3: 2, 4: 1}[f], core=KR['gold3'], col=KR['gold2'], edge=KR['gold1'],
                  light=LW['gold'], gain=0.8)
    elif f < C_LAND:
        a = f - C_CUT
        py = C_PURSE_Y + 0.9 * a * a * 1.5 + a * 1.5
        py = min(py, C_FLOOR - 19)
        c.put(c.line(hx, hy, hx, cut_y - a * 2), KR['paper1'])        # the cut string springs up
        c.put(c.line(hx, py - 2 - max(0, 6 - a * 2), hx, py), KR['paper1'])
        purse(c, g, hx, py)
    else:
        a = f - C_LAND
        c.put(c.line(hx, hy, hx, max(hy, cut_y - 8)), KR['paper1'])
        squash = 0.7 if a == 0 else 0.85 if a == 1 else 1.0
        fade = 1.0 if a <= 2 else max(0.0, 1 - (a - 2) / 3.0)
        if fade > 0:
            purse(c, g, hx, C_FLOOR - 19 + 19 * (1 - squash), sx=1 / squash ** 0.5, sy=squash, a=fade, open_=True)
        if 16 <= f < 21:                                        # a new purse lowered on its string
            k = ease_out((f - 15) / 5.0)
            py = hy + 2 + (C_PURSE_Y - hy - 2) * k
            c.put(c.line(hx, hy, hx, py), KR['paper1'])
            purse(c, g, hx, py)
    # her dagger flicks across and cuts the string
    if C_CUT - 1 <= f <= C_CUT + 1:
        a = f - C_CUT
        ang = -0.25
        ux, uy = dir_of(ang)
        tx = hx + 10 + a * 26
        ty = cut_y + 2 - a * 4
        for back in (2, 1):
            dagger(c, g, tx - ux * (40 + back * 9) * 0.55, ty - uy * (40 + back * 9) * 0.55, ang, 0.55, t * 2,
                   ghost=(K['violet3'], 0.3 * (3 - back), L['violet']))
        if a <= 0:
            dagger(c, g, tx - ux * 40 * 0.55, ty - uy * 40 * 0.55, ang, 0.55, t * 2 + 1.5, rune_gain=2.0)
        speed_lines(c, g, tx - ux * 20, ty - uy * 20, ux, uy, 0.9, 1101, count=5, spread=6, length=(8, 14))
        cpal = (K['violet0'], K['violet2'], K['violet3'], K['violet4'])
        crescent(c, g, hx - 4, cut_y + 22, 22, math.radians(70 - a * 30), 0.9, -1, 2.5, cpal, L['violet'], a=1 - a * 0.3)
    if C_CUT <= f <= C_CUT + 2:
        star4(c, g, hx, cut_y, {0: 5, 1: 3, 2: 1}[f - C_CUT], gain=1.2)
    # coins burst out, spin, fly up and away (into your pocket): each one sparkles as it goes
    rng = np.random.default_rng(1103)
    for i in range(7):
        a = f - C_LAND
        vx = rng.uniform(-3.6, 3.6)
        vy = rng.uniform(-6.0, -4.2)
        ph = rng.uniform(0, math.pi)
        if 0 <= a <= 9:
            if a <= 4:
                x = hx + vx * a
                y = C_FLOOR - 12 + vy * a + 0.32 * a * a
            else:                                               # pulled up and right, towards the rogue
                x0, y0 = hx + vx * 4, C_FLOOR - 12 + vy * 4 + 0.32 * 16
                k = ease_in_out((a - 4) / 5.0)
                x = x0 + (86 - x0) * k + math.sin(k * math.pi) * (i - 3) * 2
                y = y0 + (-2 - y0) * k
            alpha = 1.0 if a < 8 else 0.5
            coin(c, g, x, y, ph + a * 1.3, a=alpha)
            if a >= 5 and (a + i) % 3 == 0:
                star4(c, g, x, y, 2, core=KR['gold3'], col=KR['gold2'], edge=KR['gold1'], light=LW['gold'], gain=0.8)
    sparks(c, g, f, C_LAND, hx, C_FLOOR - 6, 1105, 10, (0.8, 2.2), 5, GOLD_SPARKS, LW['gold'], gravity=0.15,
           spread=(0.4, 2.7))
    dust_puffs(c, hx, C_FLOOR - 1, f, C_LAND, 4, 1.5, rise=0.3, life=6, seed=1107)
    motes(c, g, t, 1109, 6, (2, 6, 86, 50), KR['gold2'], K['violet3'], LW['gold'], gain=0.4)
    vignette(c, 0.72)
    return c, g

ILLUSTRATIONS = {
    'pinchazo': pinchazo_frame,
    'tajo_veloz': tajo_veloz_frame,
    'lanzar_la_daga': lanzar_la_daga_frame,
    'lluvia_de_dagas': lluvia_de_dagas_frame,
    'cuchillada_errante': cuchillada_errante_frame,
    'marcar_objetivo': marcar_objetivo_frame,
    'abrir_la_guardia': abrir_la_guardia_frame,
    'corte_y_guardia': corte_y_guardia_frame,
    'afilar': afilar_frame,
    'carterista': carterista_frame,
}


def render(card_id):
    fn = ILLUSTRATIONS[card_id]
    base, glow = [], []
    for f in range(FRAMES):
        b, gl = fn(f)
        base.append(b)
        glow.append(gl)
    return base, glow
