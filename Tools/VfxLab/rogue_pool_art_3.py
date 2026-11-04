"""Animated pixel-art illustrations for a third batch of the rogue's loot cards (88x64, 24 frames at 12 fps, seamless).

Same technique and helpers as rogue_pool_art.py and rogue_pool_art_2.py (docs/art-style.md): every frame has a base
image (opaque pixel art, readable without bloom) and an emissive light map, baked for the game by
export_game_card_art.py. No characters: her daggers and what they cut, her cards, her cloak hanging on its own.

  corte_rapido       Corte Rápido (5 de daño, roba 1; Combo: roba 1): two quick cuts make an X through a hanging straw
                     bundle; one card flips up, and a second one with Combo.
  tajo               Tajo (6 de daño, roba 1; Combo: +5 de daño): one diagonal cleave splits a crate on the pier; its top
                     slides into the water, a card flips up, crimson "+" marks for the Combo.
  punalada_trapera   Puñalada Trapera (5 de daño; Combo: +5 de daño): from a shadow rift behind an enemy's sigil the
                     dagger stabs once, and again harder (Combo).
  golpe_desesperado  Golpe Desesperado (12 de daño; descarta una carta al azar): a card burns away as fuel, the blade
                     shakes in a crimson aura and one huge blow cracks a temple pillar.
  instinto           Instinto (2 de daño por carta en tu mano; Combo: +6): a fan of five cards lights up one by one,
                     each sends its light into the blade, and it strikes.
  juego_de_manos     Juego de Manos (devuelve a tu mano la última carta del descarte): the top card of the discard pile
                     lifts, vanishes in violet smoke and reappears on the other side, turned to her hand.
  manto_raido        Manto Raído (7 de escudo; Despojo: +7 de escudo): her tattered cloak hangs in the wind and stops an
                     arrow behind a blue ward; with Despojo its tears glow orange and a second ward rises.
  reflejos           Reflejos (poder: +1 de destreza este combate): time slows in a dripping cave, the dagger splits
                     three falling drops, and a rune circle with rising chevrons stays under it (the power).
  escudo_reactivo    Escudo Reactivo (6 de escudo, roba 1; Combo: +1 de maná): a trap fires a bolt, a rune buckler snaps
                     open just in time and shatters it; a card and a mana drop rise.
  guardia_evasiva    Guardia Evasiva (10 de escudo; Combo: los enemigos infligen 25% menos): two crossed daggers raise a
                     big ward that stops two arrows; with Combo a mist dulls the enemies' sigils (blue chevrons down).
"""
import math

import numpy as np

from pixelkit import Canvas, hexc, dilate, erode, shift
from rogue_art import (AW, AH, FRAMES, K, L, ease_out, ease_in_out, frac, gradient_bg, vignette, star4, motes,
                       outline_mask, crescent, dagger_labels, paint_dagger, arc_dagger)
from mage_art import twinkling_stars, sparks, ellipse_ring
from warrior_art import shake, falling_sparks, dust_puffs, WARM_SPARKS, CYAN_SPARKS
from rogue_pool_art import (KR, LR, KB, LB, VIOLET_SPARKS, GOLD_SPARKS, dir_of, dagger, centred_dagger, tip_of,
                            shimmer, speed_lines, converge, clipped, mana_drop, brick_wall, cobbles, sigil,
                            diamond_ring, coin)
from rogue_pool_art_2 import K2, L2, SPOIL_SPARKS, card_mask, paint_card, burn_away, bez, arrow, smoke_puff

K3 = dict(K2)
K3.update({
    'straw0': hexc('#2c2012'), 'straw1': hexc('#55401f'), 'straw2': hexc('#80652f'), 'straw3': hexc('#b0904c'),
    'water0': hexc('#060d18'), 'water1': hexc('#0d1d30'), 'water2': hexc('#1a3550'), 'water3': hexc('#2f5f80'),
    'felt0': hexc('#1a0812'), 'felt1': hexc('#2c0e1c'), 'felt2': hexc('#431628'),
    'cloth0': hexc('#120a1e'), 'cloth1': hexc('#21123a'), 'cloth2': hexc('#33205a'), 'cloth3': hexc('#4c3380'),
    'temple0': hexc('#120f1c'), 'temple1': hexc('#1e1a2c'), 'temple2': hexc('#2d2840'), 'temple3': hexc('#443d5c'),
})
CRIM_SPARKS = (K3['crim5'], K3['crim4'], K3['crim2'])
MANA_SPARKS = (KR['mana3'], KR['mana2'], KR['mana1'])


# ---------------------------------------------------------------------------------------- shared bits
def card_rot(c, g, x, y, ang, size=1.0, face=False, alpha=1.0, lit=0.0):
    """A small card turned by `ang` (a card held in a fan). lit 0..1 makes its border and rune shine."""
    ux, uy = math.cos(ang), math.sin(ang)
    dx, dy = c.x - x, c.y - y
    lx = dx * ux + dy * uy
    ly = -dx * uy + dy * ux
    w, h = 4.5 * size, 6.5 * size
    m = (np.abs(lx) <= w) & (np.abs(ly) <= h) & ~((np.abs(lx) > w - 1) & (np.abs(ly) > h - 1))
    inner = erode(m)
    c.put(outline_mask(m), K['ink'], alpha)
    if face:
        c.put(m, KR['paper1'], alpha)
        c.put(inner, KR['paper3'], alpha)
        for yy in (-3, -1, 1, 3):
            c.put(inner & (np.abs(ly - yy * size) < 0.5) & (np.abs(lx) < w - 1.5), KR['paper1'], alpha)
        return m
    c.put(m, KR['gold3'] if lit > 0.5 else KR['gold1'], alpha)
    c.put(inner, K['vio2'], alpha)
    c.put(inner & (lx < -w * 0.4), K['vio3'], alpha)
    rune = inner & ((np.abs(lx) / (w * 0.5) + np.abs(ly) / (3.0 * size)) <= 1.0)
    c.put(rune, K['cyan4'] if lit > 0.5 else K['cyan2'], alpha)
    g.add(rune, L['cyan'], (0.4 + 0.9 * lit) * alpha)
    if lit > 0:
        g.add(outline_mask(m), L['warm'], 0.6 * lit * alpha)
    return m


def plus_marks(c, g, x, y, a, count=2, col=None, light=None):
    """Little "+" signs rising and fading: bonus damage."""
    if a < 0:
        return
    col = K3['crim5'] if col is None else col
    light = LR['crimson'] if light is None else light
    for j in range(count):
        px = x + (j * 2 - 1) * (3 + a * 0.8)
        py = y - a * 1.6 - j
        k = 1 - a / 7.0
        if k <= 0:
            continue
        m = c.pixel(px, py) | c.pixel(px - 1, py) | c.pixel(px + 1, py) | c.pixel(px, py - 1) | c.pixel(px, py + 1)
        c.put(m, col, k)
        g.add(m, light, 0.8 * k)


def rising_card(c, g, f, born, x, y0, y1, life=7, seed=0):
    """A card drawn: it flips up from below, sparkles and fades into the hand."""
    a = f - born
    if not 0 <= a <= life:
        return
    k = ease_out(min(1.0, (a + 1) / (life - 2)))
    y = y0 + (y1 - y0) * k
    spin = math.cos(a * 0.85) if a < life - 2 else 1.0
    fade = 1.0 if a <= life - 2 else 1 - (a - life + 2) / 3.0
    paint_card(c, g, x, y, spin, alpha=fade, glow=0.4)
    if a == life - 2:
        star4(c, g, x, y - 7, 3, col=K['violet3'], edge=K['violet2'], light=L['violet'])


def ward_rings(c, g, x, y, a, size=12, squash=1.0, life=7):
    """The blue diamond ward of a block: grows, holds, fades."""
    k = ease_out(min(1.0, a / 2.0))
    fade = 1.0 if a <= 2 else max(0.0, 1 - (a - 2) / (life - 2))
    for i, (r, w) in enumerate(((5 + size * k, 0.6), (3 + size * 0.65 * k, 0.5))):
        ring = diamond_ring(c, x, y, r, w, squash=squash)
        c.put(ring, KB['b2'] if i == 0 else KB['b1'], fade)
        c.put(ring & (((c.x + c.y + a * 2) % 7) < 2), KB['b3'], fade)
        g.add(ring, LB, (0.7 if i == 0 else 0.35) * fade)


def slash_line(c, g, x, y, ang, half, width, pal, light, a=1.0):
    """A straight slash: thick and bright in the middle, thinning to points."""
    ux, uy = math.cos(ang), math.sin(ang)
    vx, vy = -uy, ux
    dx, dy = c.x - x, c.y - y
    u = dx * ux + dy * uy
    v = dx * vx + dy * vy
    w = np.clip(1 - (u / half) ** 2, 0, None) * width
    band = (np.abs(u) <= half) & (np.abs(v) <= w + 0.3)
    c.put(band, pal[1], a)
    c.put(band & (np.abs(v) <= w * 0.5), pal[2], a)
    c.put(band & (np.abs(v) <= w * 0.2) & (np.abs(u) < half * 0.6), pal[3], a)
    g.add(band, light, 1.3 * a)
    return band


CPAL = (K['cyan0'], K['cyan2'], K['cyan3'], K['cyan4'])
VPAL = (K['violet0'], K['violet2'], K['violet3'], K['violet4'])
RPAL = (K3['crim1'], K3['crim3'], K3['crim5'], K['white'])


# ---------------------------------------------------------------------------------------- Corte Rápido
CR_BUNDLE = (62.0, 10.0, 34.0)  # x, top of the bundle, length
CR_HOME = (12.0, 42.0)
CR_ANG = -0.35
CR_S = 0.62
CR_CUT = 8


def dojo(c, g, t):
    gradient_bg(c, K['night0'], K['vio1'], K['night1'], 0.6)
    # paper screens with a lattice, moonlight behind
    screen = c.rect(0, 6, AW - 1, 50)
    c.put(screen, hexc('#2a2440'))
    c.put(screen & (c.x > 30) & (c.x < 70), hexc('#35304f'))
    lat = (((c.x.astype(int) % 11) == 0) | ((c.y.astype(int) % 9) == 6)) & screen
    c.put(lat, KR['wood0'])
    c.put(c.rect(0, 5, AW - 1, 6) | c.rect(0, 50, AW - 1, 51), KR['wood1'])
    for x in (0, 43, 86):
        c.put(c.rect(x, 0, x + 1, 56), KR['wood0'])
    floor = c.rect(0, 52, AW - 1, AH - 1)
    c.put(floor, KR['wood1'])
    c.put(floor & ((c.x.astype(int) % 13) == 0), KR['wood0'])
    c.put(c.rect(0, 52, AW - 1, 52), KR['wood2'])
    # a beam the bundle hangs from
    c.put(c.rect(40, 2, 86, 4), KR['wood0'])
    c.put(c.rect(40, 2, 86, 2), KR['wood2'])


def straw_bundle(c, g, x, top, length, sway=0.0, keep=None, alpha=1.0):
    """A bundle of straw bound with rope (a cutting target); `keep` clips it (the cut pieces)."""
    xs = x + sway * (c.y - top) / max(1.0, length)
    m = (np.abs(c.x - xs) <= 4.6) & (c.y >= top) & (c.y <= top + length)
    m &= ~((np.abs(c.x - xs) > 3.6) & ((c.y < top + 1) | (c.y > top + length - 1)))
    if keep is not None:
        m &= keep
    c.put(outline_mask(m), K['ink'], alpha)
    c.put(m, K3['straw1'], alpha)
    strands = m & (((c.x - xs + 10) * 1.7).astype(int) % 3 == 0)
    c.put(strands, K3['straw2'], alpha)
    c.put(m & (c.x < xs - 2), K3['straw3'], alpha)
    c.put(m & (c.x > xs + 3), K3['straw0'], alpha)
    for by in (top + 4, top + length - 5):
        band = m & (np.abs(c.y - by) < 1.0)
        c.put(band, KR['wood0'], alpha)
        c.put(band & (c.y < by), KR['paper2'], alpha)
    return m


def corte_rapido_frame(f):
    t = f / FRAMES
    c, g = Canvas(AW, AH), Canvas(AW, AH)
    dojo(c, g, t)
    bx, top, length = CR_BUNDLE
    sway = math.sin(t * math.pi * 4) * 0.8
    # the bundle: hangs, is cut in an X, its lower part drops; a new one is lowered at the end
    rope_top = 4
    if f < CR_CUT:
        c.put(c.line(bx, rope_top, bx + sway * 0.1, top), KR['paper1'])
        straw_bundle(c, g, bx, top, length, sway)
    elif f < 19:
        a = f - CR_CUT
        cy = top + 14
        lift = 0 if f < 14 else (f - 13) ** 2 * 1.2                # the stump is pulled up out of the picture
        c.put(c.line(bx, rope_top, bx, top - lift), KR['paper1'])
        upper = (c.y - (cy - lift)) < -(np.abs(c.x - bx)) * 0.9    # above the X stays on the rope
        straw_bundle(c, g, bx, top - lift, length, keep=upper)
        lower_y = 0.5 * a * a + a
        fade = 1.0 if a <= 6 else max(0.0, 1 - (a - 6) / 4.0)
        if top + 14 + lower_y < 64:
            keep = (c.y - (cy + lower_y)) > (np.abs(c.x - bx - a * 0.4)) * 0.9
            straw_bundle(c, g, bx + a * 0.4, top + lower_y, length, keep=keep, alpha=fade)
        # straw bits
        rng = np.random.default_rng(2201)
        for i in range(14):
            if a <= 8:
                a0 = rng.uniform(0, 2 * math.pi)
                sp = rng.uniform(0.5, 1.8)
                px = bx + math.cos(a0) * sp * a
                py = cy - math.sin(a0) * sp * a + 0.25 * a * a
                c.put(c.line(px, py, px + 1, py + (1 if i % 2 else -1)), K3['straw3'] if i % 3 else K3['straw2'], 1 - a / 9)
    else:
        k = ease_out((f - 18) / 5.0)
        tp = -length + (top + length) * k
        c.put(c.line(bx, rope_top, bx, max(rope_top, tp)), KR['paper1'])
        straw_bundle(c, g, bx, tp, length, sway * k)
    # the dagger: rest, wind-up, two cuts (down-right then up-right), back
    hit = (bx, top + 14)
    if f <= 4 or f >= 19:
        gx_, gy_, ang = CR_HOME[0], CR_HOME[1] + math.sin(t * math.pi * 4) * 0.8, CR_ANG
    elif f <= 6:
        k = ease_out((f - 4) / 2.0)
        gx_, gy_, ang = CR_HOME[0] - 3 * k, CR_HOME[1] + 2 * k, CR_ANG - 0.6 * k
    elif f <= 9:
        pose = {7: (hit[0] - 30, hit[1] - 12, 0.5), 8: (hit[0] - 26, hit[1] + 10, -0.55), 9: (hit[0] - 24, hit[1] + 8, -0.4)}
        gx_, gy_, ang = pose[f]
    else:
        k = ease_in_out((f - 9) / 9.0)
        sx, sy, sa = hit[0] - 24, hit[1] + 8, -0.4
        gx_, gy_, ang = sx + (CR_HOME[0] - sx) * k, sy + (CR_HOME[1] - sy) * k - math.sin(k * math.pi) * 6, sa + (CR_ANG - sa) * k
    if f in (7, 8, 9, 10):
        q = {7: 0.0, 8: 0.0, 9: 0.4, 10: 0.75}[f]
        if f >= 7:
            slash_line(c, g, hit[0], hit[1], 0.75, 13, 2.4 * (1 - q), CPAL, L['cyan'], a=1 - q)
        if f >= 8:
            slash_line(c, g, hit[0], hit[1], -0.75, 13, 2.4 * (1 - q * (f > 8)), VPAL, L['violet'], a=1 - q * (f > 8))
    dagger(c, g, round(gx_), round(gy_), ang, CR_S, t * 2 + (1.5 if 6 <= f <= 9 else 0),
           -2 + (f - 1) * 10.0 if 1 <= f <= 4 else None, rune_gain=2.0 if 6 <= f <= 9 else 1.0)
    if CR_CUT <= f <= CR_CUT + 2:
        star4(c, g, hit[0], hit[1], {0: 7, 1: 4, 2: 2}[f - CR_CUT], gain=1.3)
    # roba 1, and one more with Combo
    rising_card(c, g, f, 11, 26, 70, 24)
    rising_card(c, g, f, 14, 38, 70, 20)
    motes(c, g, t, 2203, 7, (2, 6, 86, 50), K['cyan3'], K3['straw3'], L['cyan'], gain=0.45)
    vignette(c, 0.72)
    return c, g


# ---------------------------------------------------------------------------------------- Tajo
TJ_CRATE = (56.0, 41.0)       # centre of the crate on the pier
TJ_HOME = (12.0, 36.0)
TJ_ANG = -0.5
TJ_S = 0.64
TJ_CUT = 9
TJ_ARC_C, TJ_ARC_R = (40.0, 58.0), 30.0
TJ_HEADS = {8: math.radians(132), 9: math.radians(70), 10: math.radians(28)}


def pier(c, g, t):
    gradient_bg(c, K['night0'], K['night2'], K['night1'], 0.55)
    twinkling_stars(c, g, t, 2301, 16, (2, 2, 86, 26))
    moon = c.disc(20, 12, 5) & ~c.disc(22, 10.5, 4.6)
    c.put(moon, K['moon1'])
    # far shore and a moored boat
    shore = c.poly([(0, 36), (14, 33), (30, 35), (52, 32), (70, 34), (88, 31), (88, 40), (0, 40)])
    c.put(shore, K['roof2'])
    boat = c.poly([(64, 37), (82, 37), (79, 40), (67, 40)]) | c.rect(72, 27, 72, 37)
    c.put(boat, K['roof'])
    c.put(c.poly([(73, 28), (80, 35), (73, 35)]), K['vio2'])
    water = c.rect(0, 40, AW - 1, AH - 1)
    c.put(water, K3['water1'])
    for i, y in enumerate(range(42, 64, 3)):
        off = (t * 88 * (1 if i % 2 else -1) + i * 17) % 18
        for x in np.arange(-18 + off, AW, 18):
            c.put(c.rect(int(x), y, int(x) + 5 + i % 3, y), K3['water2'] if i % 2 else K3['water3'])
    # the pier
    deck = c.rect(0, 50, 70, 53)
    c.put(deck, KR['wood1'])
    c.put(c.rect(0, 50, 70, 50), KR['wood2'])
    for x in range(0, 70, 7):
        c.put(c.rect(x, 50, x, 53), KR['wood0'])
    for x in (6, 30, 56):
        c.put(c.rect(x, 54, x + 2, 63), KR['wood0'])


def crate(c, g, x, y, keep=None, alpha=1.0, w=11, h=9):
    m = (np.abs(c.x - x) <= w) & (np.abs(c.y - y) <= h)
    if keep is not None:
        m &= keep
    c.put(outline_mask(m), K['ink'], alpha)
    c.put(m, KR['wood1'], alpha)
    c.put(m & ((np.floor((c.y - y + h) / 4) % 2) == 0), KR['wood2'], alpha)
    c.put(m & (np.abs(np.abs(c.x - x) - w + 1) < 0.6), KR['wood0'], alpha)
    c.put(m & (np.abs((c.x - x) - (c.y - y) * w / h) < 0.8), KR['wood0'], alpha)    # diagonal brace
    for cx, cy in ((x - w + 1, y - h + 1), (x + w - 1, y - h + 1), (x - w + 1, y + h - 1), (x + w - 1, y + h - 1)):
        c.put(m & c.disc(cx, cy, 1.0), KR['iron2'], alpha)
    return m


def tajo_frame(f):
    t = f / FRAMES
    c, g = Canvas(AW, AH), Canvas(AW, AH)
    pier(c, g, t)
    cx, cy = TJ_CRATE
    cut_ang = math.radians(-32)                                 # the cut runs from upper left to lower right
    nx, ny = -math.sin(cut_ang), math.cos(cut_ang)
    if f < TJ_CUT or f >= 20:
        a = 1.0 if f < TJ_CUT else (f - 19) / 4.0
        crate(c, g, cx, cy, alpha=a)
    else:
        a = f - TJ_CUT
        side = (c.x - cx) * nx + (c.y - cy) * ny
        crate(c, g, cx, cy, keep=side > 0.4)                    # the bottom stays on the pier
        # the top slides down the cut and off the pier into the water
        ux_, uy_ = math.cos(cut_ang), math.sin(cut_ang)
        d = 0.6 * a * a + a
        tx_, ty_ = cx + ux_ * d * 1.2 * (-1 if ux_ < 0 else 1), cy + uy_ * d * 1.2 * (-1 if ux_ < 0 else 1)
        tx_, ty_ = cx + abs(ux_) * d, cy - abs(uy_) * d * 0.2 + 0.35 * a * a
        if ty_ < 60:
            side2 = (c.x - tx_) * nx + (c.y - ty_) * ny
            crate(c, g, tx_, ty_, keep=side2 <= 0.4, alpha=1.0 if ty_ < 54 else 0.6)
        if 5 <= a <= 9:                                        # splash where it hits the water
            k = (a - 5) / 4.0
            ring = ellipse_ring(c, 76, 56, 3 + 7 * k, 1 + 2 * k, 0.6)
            c.put(ring, K3['water3'], 1 - k)
            sparks(c, g, f, TJ_CUT + 5, 76, 55, 2303, 10, (0.6, 1.8), 5, (K['moon2'], K3['water3'], K3['water2']), L['blue'],
                   gravity=0.25, spread=(0.6, 2.6), gain=0.3)
    cpal = CPAL
    if f in TJ_HEADS:
        crescent(c, g, TJ_ARC_C[0], TJ_ARC_C[1], TJ_ARC_R, TJ_HEADS[f], 2.0, -1, 5.5, cpal, L['cyan'], gain=1.5)
        prev = TJ_HEADS.get(f - 1)
        if prev is not None:
            arc_dagger(c, g, TJ_ARC_C[0], TJ_ARC_C[1], TJ_ARC_R, prev, TJ_S, t, ghost=(K['violet3'], 0.55, L['violet']))
        arc_dagger(c, g, TJ_ARC_C[0], TJ_ARC_C[1], TJ_ARC_R, TJ_HEADS[f], TJ_S, t, rune_gain=2.0)
    else:
        if 11 <= f <= 14:
            q = (f - 10) / 5.0
            crescent(c, g, TJ_ARC_C[0], TJ_ARC_C[1], TJ_ARC_R, TJ_HEADS[10], 2.0 * (1 - q * 0.6), -1, 5.5 * (1 - q), cpal,
                     L['cyan'], a=1 - q, gain=1.2)
        if f <= 5 or f >= 20:
            gx_, gy_, ang = TJ_HOME[0], TJ_HOME[1] + math.sin(t * math.pi * 4) * 0.8, TJ_ANG
        elif f <= 7:
            k = ease_out((f - 5) / 2.0)
            gx_, gy_, ang = TJ_HOME[0] - 2 * k, TJ_HOME[1] + 6 * k, TJ_ANG - 0.7 * k
        else:
            th = TJ_HEADS[10]
            a0 = math.pi / 2 - th
            hx, hy = TJ_ARC_C[0] + math.cos(th) * TJ_ARC_R, TJ_ARC_C[1] - math.sin(th) * TJ_ARC_R
            sx, sy = hx - math.cos(a0) * 40 * TJ_S, hy - math.sin(a0) * 40 * TJ_S
            k = ease_in_out((f - 10) / 10.0)
            da = ((TJ_ANG - a0 + math.pi) % (2 * math.pi)) - math.pi
            gx_, gy_ = sx + (TJ_HOME[0] - sx) * k, sy + (TJ_HOME[1] - sy) * k - math.sin(k * math.pi) * 5
            ang = a0 + da * k
        dagger(c, g, round(gx_), round(gy_), ang, TJ_S, t * 2 + (1.5 if 6 <= f <= 8 else 0),
               -2 + (f - 1) * 10.0 if 1 <= f <= 4 else None, rune_gain=1.8 if 6 <= f <= 8 else 1.0)
    if TJ_CUT <= f <= TJ_CUT + 2:
        star4(c, g, cx, cy, {0: 8, 1: 5, 2: 2}[f - TJ_CUT], gain=1.4)
    rng = np.random.default_rng(2305)
    for i in range(8):                                          # splinters
        a = f - TJ_CUT
        if 0 < a <= 7:
            a0 = rng.uniform(-0.6, 3.6)
            sp = rng.uniform(0.8, 2.0)
            px = cx + math.cos(a0) * sp * a
            py = cy - math.sin(a0) * sp * a + 0.25 * a * a
            c.put(c.pixel(px, py) | c.pixel(px + 1, py), KR['wood2'], 1 - a / 8)
    for i in range(2):                                          # Combo: +5 de daño
        plus_marks(c, g, cx - 6 + i * 12, cy - 6, f - TJ_CUT - 1 - i)
    rising_card(c, g, f, 13, 20, 70, 22)
    motes(c, g, t, 2307, 7, (2, 6, 86, 48), K['cyan3'], K['moon1'], L['cyan'], gain=0.45)
    vignette(c, 0.72)
    return c, g


# ---------------------------------------------------------------------------------------- Puñalada Trapera
PT_SIGIL = (62.0, 38.0)
PT_RIFT = (32.0, 22.0)
PT_HITS = (9, 14)


def lantern_alley(c, g, t):
    brick_wall(c, g, t, lamp=(78, 9))
    # a pool of warm light on the ground under the lamp; the rest of the alley is in shadow
    floor = c.rect(0, 52, AW - 1, AH - 1)
    c.put(floor, K['roof'])
    pool = (((c.x - 64) / 22) ** 2 + ((c.y - 56) / 5) ** 2) <= 1
    c.put(pool & floor, hexc('#3a2230'))
    c.put(pool & floor & ((((c.x - 64) / 14) ** 2 + ((c.y - 56) / 3.2) ** 2) <= 1), hexc('#4d2c34'))
    c.put(c.rect(0, 52, AW - 1, 52), KR['brick3'])
    # deep shadow on the left half
    c.put(np.ones((AH, AW), bool) & (c.x < 40), K['ink'], np.clip((40 - c.x) / 40, 0, 1) * 0.55)


def rift(c, g, x, y, open_, t):
    """A vertical tear of shadow, rimmed in violet, opening (0..1)."""
    if open_ <= 0:
        return
    rx, ry = 4.5 * open_, 16 * min(1.0, open_ * 1.4)
    m = (((c.x - x) / max(rx, 0.5)) ** 2 + ((c.y - y) / ry) ** 2) <= 1
    edge = m & ~erode(m)
    c.put(m, K['ink'])
    c.put(m & (((c.x - x) / max(rx * 0.5, 0.4)) ** 2 + ((c.y - y) / (ry * 0.8)) ** 2 <= 1), K['vio0'])
    c.put(edge, K['violet2'])
    c.put(edge & (c.y < y), K['violet3'])
    g.add(edge, L['violet'], 0.8 * open_)
    rng = np.random.default_rng(2401)
    for i in range(6):
        u = frac(t * 2 + rng.random())
        px = x + rng.uniform(-rx, rx) * 1.5
        py = y + ry - u * ry * 2
        m2 = c.pixel(px + math.sin(u * 6) * 2, py)
        c.put(m2, K['violet3'], open_ * math.sin(u * math.pi))
        g.add(m2, L['violet'], 0.5 * open_)


def punalada_trapera_frame(f):
    t = f / FRAMES
    c, g = Canvas(AW, AH), Canvas(AW, AH)
    lantern_alley(c, g, t)
    hot = any(0 <= f - h <= 3 for h in PT_HITS)
    sigil(c, g, PT_SIGIL[0], PT_SIGIL[1], 7, 7, t, 1.0 if hot else 0.7, col=K3['crim4'] if hot else K3['crim3'],
          light=LR['crimson'])
    c.put(c.disc(*PT_SIGIL, 1.5), K3['crim5'])
    g.add(c.disc(*PT_SIGIL, 1.5), LR['crimson'], 0.7)
    open_ = 0.0 if f <= 3 else min(1.0, (f - 3) / 3.0) if f <= 18 else max(0.0, 1 - (f - 18) / 3.0)
    rift(c, g, PT_RIFT[0], PT_RIFT[1], open_, t)
    # the dagger comes out of the rift, stabs, slips back; again harder (Combo), then hides
    ang = math.atan2(PT_SIGIL[1] - PT_RIFT[1], PT_SIGIL[0] - PT_RIFT[0])
    ux, uy = dir_of(ang)
    reach = math.hypot(PT_SIGIL[0] - PT_RIFT[0], PT_SIGIL[1] - PT_RIFT[1]) - 1.5
    along = {7: 2.0, 8: 10.0, 9: reach, 10: reach - 2, 11: 8.0, 12: 2.0, 13: 12.0, 14: reach + 1, 15: reach,
             16: reach - 4, 17: 6.0}.get(f)
    if along is not None:
        tx, ty = PT_RIFT[0] + ux * along, PT_RIFT[1] + uy * along
        gx_, gy_ = tx - ux * 40 * 0.6, ty - uy * 40 * 0.6
        keep = ((c.x - PT_RIFT[0]) * ux + (c.y - PT_RIFT[1]) * uy) > -0.5   # only what is out of the rift shows
        power = 2.0 if f >= 13 else 1.4
        if f in (9, 14):
            speed_lines(c, g, tx - ux * 6, ty - uy * 6, ux, uy, 0.9, 2403 + f, count=5, spread=4, length=(6, 10))
        clipped(c, g, keep, lambda c2, g2: dagger(c2, g2, gx_, gy_, ang, 0.6, t * 2 + 1.5, rune_gain=power))
        if f >= 13:                                            # Combo: crimson light on the second stab
            lab, u, v = dagger_labels(c, gx_, gy_, ang, s=0.6)
            aura = dilate(lab > 0) & ~(lab > 0) & keep
            c.put(aura, K3['crim3'], 0.7)
            g.add(aura, LR['crimson'], 0.5)
    for i, h in enumerate(PT_HITS):
        a = f - h
        if 0 <= a <= 2:
            star4(c, g, PT_SIGIL[0] - 1, PT_SIGIL[1], ({0: 6, 1: 4, 2: 2} if i == 0 else {0: 8, 1: 6, 2: 3})[a],
                  core=K['white'], col=K['cyan3'] if i == 0 else K3['crim5'], edge=K['cyan2'] if i == 0 else K3['crim4'],
                  light=L['cyan'] if i == 0 else LR['crimson'], gain=1.4)
        sparks(c, g, f, h, PT_SIGIL[0], PT_SIGIL[1], 2405 + i, 8 + 4 * i, (0.8, 2.2), 6,
               CYAN_SPARKS if i == 0 else CRIM_SPARKS, L['cyan'] if i == 0 else LR['crimson'], gravity=0.14)
        if i == 1:
            plus_marks(c, g, PT_SIGIL[0] + 2, PT_SIGIL[1] - 6, a)
    motes(c, g, t, 2407, 6, (36, 6, 86, 50), K['violet3'], K3['crim4'], L['violet'], gain=0.45)
    vignette(c, 0.72)
    return c, g


# ---------------------------------------------------------------------------------------- Golpe Desesperado
GD_HOME = (14.0, 40.0)
GD_ANG = -0.35
GD_S = 0.68
GD_PILLAR = (68.0, 30.0)
GD_HIT = 11


def ruined_temple(c, g, t):
    gradient_bg(c, K['night0'], K['vio2'], K['night1'], 0.6)
    c.put(c.disc(60, 16, 26), K3['crim0'], 0.35)
    c.put(c.disc(60, 16, 16), K3['crim0'], 0.6)
    twinkling_stars(c, g, t, 2501, 12, (2, 2, 86, 26), avoid=(60, 16, 18))
    for x in (6, 28):                                           # broken columns far behind
        col = c.rect(x, 22 - (x % 7), x + 5, 52)
        c.put(col, K3['temple1'])
        c.put(c.rect(x, 22 - (x % 7), x, 52), K3['temple2'])
    c.put(c.rect(0, 52, AW - 1, AH - 1), K3['temple0'])
    c.put(c.rect(0, 52, AW - 1, 52), K3['temple3'])
    for x in range(4, AW, 12):
        c.put(c.rect(x, 53, x, 63), K['ink'])


def pillar(c, g, x, y, crack=0.0, chunk=None, alpha=1.0):
    """The temple pillar: fluted stone with a capital; `crack` 0..1 grows the crack, `chunk` = (dx, dy, a) knocked off."""
    shaft = c.rect(int(x) - 7, int(y) - 22, int(x) + 7, 52)
    cap = c.rect(int(x) - 9, int(y) - 26, int(x) + 9, int(y) - 22)
    base = c.rect(int(x) - 9, 49, int(x) + 9, 52)
    body = shaft | cap | base
    piece = c.poly([(x - 2, y - 5), (x + 3, y - 7), (x + 8, y - 4), (x + 8, y + 7), (x + 2, y + 9), (x - 1, y + 4),
                    (x + 1, y)]) & shaft
    if chunk is not None:
        body &= ~piece
    c.put(outline_mask(body), K['ink'], alpha)
    c.put(body, K3['temple2'], alpha)
    c.put(body & shaft & ((c.x.astype(int) - int(x)) % 4 == 0), K3['temple1'], alpha)
    c.put(body & (c.x < x - 4), K3['temple3'], alpha)
    c.put((cap | base) & ~shift(cap | base, 0, 1), K3['temple3'], alpha)
    if chunk is not None:
        c.put(outline_mask(~body & shaft) & body, K['ink'], alpha)
    if crack > 0:
        pts = ((-7, -2), (-3, 0), (-4, 3), (0, 5), (2, 2), (5, 6), (7, 4))
        n = max(1, int(round(crack * (len(pts) - 1))))
        for (x0, y0), (x1, y1) in list(zip(pts, pts[1:]))[:n]:
            ln = c.line(x + x0, y + y0, x + x1, y + y1) & body
            c.put(ln, K['ink'])
            c.put(shift(ln, 0, 1) & body & ~ln, K3['crim3'], 0.9)
            g.add(ln, LR['crimson'], 0.5)
    if chunk is not None:
        dx, dy, a = chunk
        moved = shift(piece, int(round(dx)), int(round(dy)))
        c.put(outline_mask(moved), K['ink'], a)
        c.put(moved, K3['temple2'], a)
        c.put(moved & ~shift(moved, 0, 1), K3['temple3'], a)


def golpe_desesperado_frame(f):
    t = f / FRAMES
    c, g = Canvas(AW, AH), Canvas(AW, AH)
    ruined_temple(c, g, t)
    px, py = GD_PILLAR
    if f < GD_HIT or f >= 21:
        pillar(c, g, px, py, crack=0.0 if f < GD_HIT else 0.0)
    elif f <= GD_HIT + 1:
        pillar(c, g, px, py, crack=(f - GD_HIT + 1) / 2.0)
    else:
        a = f - GD_HIT - 1
        heal = 1.0 if f <= 17 else max(0.0, 1 - (f - 17) / 3.0)
        pillar(c, g, px, py, crack=heal, chunk=(a * 1.5, 0.45 * a * a, 1.0 if a < 6 else max(0.0, 1 - (a - 5) / 3.0)))
        if f >= 18:                                            # the stone mends by the end of the loop
            pillar(c, g, px, py, crack=0.0, alpha=(f - 17) / 4.0)
    # a card from her hand burns away: the price of the blow
    cardp = (30.0, 20.0)
    if 2 <= f <= 8:
        p = min(1.0, (f - 2) / 5.0)
        m = card_mask(c, cardp[0], cardp[1] + math.sin(t * math.pi * 4), 1.0, 1.3)
        dist = np.clip(c.y - (cardp[1] - 9), 0, None)
        dist = (cardp[1] + 9) - c.y                             # burns from the bottom up
        burnt, edge, light = burn_away(c, g, m, np.abs(dist), p, 2501, light=LR['crimson'])
        paint_card(c, g, cardp[0], cardp[1] + math.sin(t * math.pi * 4), 1.0, face=True, size=1.3, keep=~burnt)
        c.put(edge, K3['crim5'])
        c.put(edge & (c.B < 0.5), K3['spoil2'])
        g.add(edge, LR['crimson'], 0.9)
    if 4 <= f <= 9:                                             # its embers stream into the blade
        rng = np.random.default_rng(2503)
        k = ease_in_out((f - 3) / 6.0)
        tip = tip_of(GD_HOME[0], GD_HOME[1], GD_ANG, GD_S, tip=24)
        for i in range(12):
            sx, sy = cardp[0] + rng.uniform(-5, 5), cardp[1] + rng.uniform(-6, 6)
            x = sx + (tip[0] - sx) * k
            y = sy + (tip[1] - sy) * k - math.sin(k * math.pi) * rng.uniform(2, 8)
            m = c.pixel(x, y)
            c.put(m, K3['crim5'] if i % 2 else K3['spoil3'])
            g.add(m, LR['crimson'], 0.8)
    # the blade: trembles in a crimson aura, then the huge blow
    trem = (1 if f % 2 else -1) if 6 <= f <= 9 else 0
    if f <= 9 or f >= 22:
        gx_, gy_, ang = GD_HOME[0] + trem, GD_HOME[1] + (math.sin(t * math.pi * 4) * 0.8 if f <= 4 or f >= 22 else 0), GD_ANG
        if 8 <= f <= 9:
            gx_ -= 3
            ang = GD_ANG - 0.4
    elif f <= 12:
        k = ease_out((f - 9) / 2.0)
        ex, ey = px - 8 - math.cos(-0.1) * 40 * GD_S, py - math.sin(-0.1) * 40 * GD_S
        gx_, gy_ = GD_HOME[0] + (ex - GD_HOME[0]) * k, GD_HOME[1] + (ey - GD_HOME[1]) * k
        ang = GD_ANG - 0.4 + (-0.1 - GD_ANG + 0.4) * k
    else:
        ex, ey = px - 8 - math.cos(-0.1) * 40 * GD_S, py - math.sin(-0.1) * 40 * GD_S
        k = ease_in_out((f - 12) / 10.0)
        gx_, gy_ = ex + (GD_HOME[0] - ex) * k, ey + (GD_HOME[1] - ey) * k - math.sin(k * math.pi) * 6
        ang = -0.1 + (GD_ANG + 0.1) * k
    fury = 5 <= f <= 12
    if fury:
        lab, u, v = dagger_labels(c, gx_, gy_, ang, s=GD_S)
        aura = dilate(dilate(lab > 0)) & ~(lab > 0)
        c.put(aura, K3['crim2'], 0.8)
        c.put(aura & ((c.x + c.y + f) % 3 == 0), K3['crim4'], 0.8)
        g.add(aura, LR['crimson'], 0.7)
    if 10 <= f <= 11:
        ux, uy = dir_of(ang)
        speed_lines(c, g, *tip_of(gx_, gy_, ang, GD_S), ux, uy, 1.0, 2505, count=7, spread=7, length=(9, 16), back=14)
    dagger(c, g, round(gx_), round(gy_), ang, GD_S, t * 2, -2 + (f - 1) * 10.0 if 1 <= f <= 4 else None,
           rune_gain=2.2 if fury else 1.0)
    if fury:                                                    # crimson runes while the blade is enraged
        lab, u, v = dagger_labels(c, round(gx_), round(gy_), ang, s=GD_S)
        c.put(lab == 5, K3['crim5'])
        g.add(lab == 5, LR['crimson'], 1.0)
    if GD_HIT <= f <= GD_HIT + 1:
        crescent(c, g, px - 22, py + 10, 22, math.radians(40 if f == GD_HIT else 20), 1.5, -1, 6.0, RPAL, LR['crimson'],
                 a=1.0 if f == GD_HIT else 0.5, gain=1.5)
    if GD_HIT <= f <= GD_HIT + 3:
        star4(c, g, px - 7, py, {0: 10, 1: 7, 2: 4, 3: 2}[f - GD_HIT], gain=1.6)
    falling_sparks(c, g, f, GD_HIT, px - 7, py, 2507, 12, (1.0, 2.8), 7, WARM_SPARKS, L['warm'], floor=52, spread=(-1.2, 2.4))
    rng = np.random.default_rng(2509)
    for i in range(10):                                         # stone debris
        a = f - GD_HIT
        if 0 < a <= 8:
            a0 = rng.uniform(-0.8, 2.2)
            sp = rng.uniform(0.8, 2.2)
            x = px - 4 + math.cos(a0) * sp * a
            y = py - math.sin(a0) * sp * a + 0.3 * a * a
            if y < 52:
                c.put(c.rect(int(x), int(y), int(x) + (i % 2), int(y) + (i % 2)), K3['temple3'], 1 - a / 9)
    dust_puffs(c, px, 51, f, GD_HIT + 2, 5, 2.0, rise=0.4, life=7, seed=2511)
    if GD_HIT <= f <= GD_HIT + 2:
        shake(c, g, {0: 2, 1: -1, 2: 1}[f - GD_HIT], {0: 1, 1: 0, 2: -1}[f - GD_HIT])
    motes(c, g, t, 2513, 6, (2, 6, 86, 50), K3['crim4'], K['violet3'], LR['crimson'], gain=0.45)
    vignette(c, 0.72)
    return c, g


# ---------------------------------------------------------------------------------------- Instinto
IN_FAN = (24.0, 58.0)         # pivot of the fan of five cards (her hand)
IN_TIP = (60.0, 20.0)         # the dagger hangs pointing up, ready
IN_TARGET = (72.0, 34.0)
IN_HIT = 15


def night_forest(c, g, t):
    gradient_bg(c, K['teal0'], K['teal2'], K['teal1'], 0.6)
    twinkling_stars(c, g, t, 2601, 10, (2, 2, 86, 16))
    for i, (x, w, h) in enumerate(((4, 5, 40), (16, 4, 34), (40, 6, 44), (52, 4, 30), (80, 6, 42))):
        trunk = c.rect(x, 56 - h, x + w, 56)
        crown = c.poly([(x - 8, 56 - h + 12), (x + w / 2, 56 - h - 10), (x + w + 8, 56 - h + 12)])
        c.put(trunk | crown, K['roof2'] if i % 2 else K['roof'])
    c.put(c.rect(0, 56, AW - 1, AH - 1), K['roof'])
    # eyes in the dark that blink (the hunted)
    for i, (ex, ey) in enumerate(((70, 30), (75, 30))):
        if (int(t * 24) % 12) != 3:
            m = c.pixel(ex, ey)
            c.put(m, K3['crim4'])
            g.add(m, LR['crimson'], 0.5)
    rng = np.random.default_rng(2603)                          # fireflies
    for i in range(6):
        u = frac(t * (1 + i % 2) + rng.random())
        x = rng.uniform(6, 82) + math.sin((u + i) * math.pi * 2) * 3
        y = rng.uniform(20, 50) + math.cos((u + i) * math.pi * 2) * 2
        a = 0.5 + 0.5 * math.sin(u * math.pi * 2)
        m = c.pixel(x, y)
        c.put(m, K3['pois4'] if a > 0.5 else K3['pois2'])
        g.add(m, L2['poison'], 0.4 * a)


def instinto_frame(f):
    t = f / FRAMES
    c, g = Canvas(AW, AH), Canvas(AW, AH)
    night_forest(c, g, t)
    # the target: a sigil where the eyes are
    sigil(c, g, IN_TARGET[0], IN_TARGET[1], 6, 6, t, 0.6 + 0.4 * (IN_HIT <= f <= IN_HIT + 3), col=K3['crim3'],
          light=LR['crimson'])
    # her hand: five cards in a fan; one by one they light up and their light flies into the blade (2 de daño cada una)
    tipx, tipy = IN_TIP
    for i in range(5):
        ang = -0.55 + i * 0.27 - math.pi / 2 + math.pi / 2
        fan = -0.72 + i * 0.36
        fx = IN_FAN[0] + math.sin(fan) * 20
        fy = IN_FAN[1] - math.cos(fan) * 13
        lit_at = 3 + i * 2
        lit = 1.0 if lit_at <= f <= IN_HIT else 0.0
        card_rot(c, g, fx, fy, fan, size=1.15, lit=lit)
        a = f - lit_at
        if 0 <= a <= 3:                                         # a spark from the card into the blade
            k = ease_in_out(a / 3.0)
            x = fx + (tipx - 4 - fx) * k
            y = fy - 8 + (tipy + 14 - fy + 8) * k - math.sin(k * math.pi) * 6
            star4(c, g, x, y, 1 if a < 3 else 2, core=K['white'], col=K['cyan3'], edge=K['cyan2'], light=L['cyan'])
    charge = sum(1 for i in range(5) if 3 + i * 2 + 3 <= f <= IN_HIT)
    # the dagger: point up, charging, then it turns and strikes the target
    if f < IN_HIT - 1 or f >= 22:
        ang = -math.pi / 2 + 0.25
        bob = math.sin(t * math.pi * 4) * 0.8
        tx, ty = tipx, tipy + bob
    elif f <= IN_HIT + 1:
        k = {IN_HIT - 1: 0.4, IN_HIT: 1.0, IN_HIT + 1: 1.0}[f]
        ang = (-math.pi / 2 + 0.25) + (math.atan2(IN_TARGET[1] - tipy, IN_TARGET[0] - tipx) + math.pi / 2 - 0.25) * k
        tx, ty = tipx + (IN_TARGET[0] - 1 - tipx) * k, tipy + (IN_TARGET[1] - tipy) * k
    else:
        k = ease_in_out((f - IN_HIT - 1) / 6.0)
        a0 = math.atan2(IN_TARGET[1] - tipy, IN_TARGET[0] - tipx)
        ang = a0 + (-math.pi / 2 + 0.25 - a0) * k
        tx, ty = IN_TARGET[0] - 1 + (tipx - IN_TARGET[0] + 1) * k, IN_TARGET[1] + (tipy - IN_TARGET[1]) * k
    ux, uy = dir_of(ang)
    gx_, gy_ = tx - ux * 40 * 0.6, ty - uy * 40 * 0.6
    if f == IN_HIT:
        speed_lines(c, g, tx, ty, ux, uy, 1.0, 2605, count=6, spread=5, length=(7, 12), back=10)
    dagger(c, g, round(gx_), round(gy_), ang, 0.6, t * 2 + charge * 0.3, None, rune_gain=1.0 + 0.3 * charge)
    if charge >= 3:                                             # the edge glows with the gathered light
        lab, u, v = dagger_labels(c, round(gx_), round(gy_), ang, s=0.6)
        c.put((lab == 1), K['cyan4'])
        g.add(lab == 1, L['cyan'], 0.3 * charge)
    if IN_HIT <= f <= IN_HIT + 3:
        star4(c, g, IN_TARGET[0], IN_TARGET[1], {0: 9, 1: 6, 2: 4, 3: 2}[f - IN_HIT], gain=1.5)
    sparks(c, g, f, IN_HIT, IN_TARGET[0], IN_TARGET[1], 2607, 14, (1.0, 2.6), 6, CYAN_SPARKS, L['cyan'], gravity=0.12)
    plus_marks(c, g, IN_TARGET[0] + 2, IN_TARGET[1] - 6, f - IN_HIT - 1)       # Combo: +6
    motes(c, g, t, 2609, 6, (2, 6, 86, 50), K['cyan3'], K3['pois3'], L['cyan'], gain=0.4)
    vignette(c, 0.72)
    return c, g


# ---------------------------------------------------------------------------------------- Juego de Manos
JM_PILE = (22.0, 42.0)
JM_HAND = (64.0, 28.0)
JM_VANISH = 7
JM_APPEAR = 12


def card_table(c, g, t):
    gradient_bg(c, K['night0'], K['vio1'], K['night1'], 0.6)
    # tavern back wall with a candle-lit niche
    c.put(c.rect(0, 0, AW - 1, 30), hexc('#1b1226'))
    for x in range(0, AW, 10):
        c.put(c.rect(x, 0, x, 30), K['vio0'])
    fl = 0.85 + 0.15 * math.sin(t * math.pi * 2 * 3)
    for r, col in ((14 * fl, hexc('#28172a')), (8 * fl, hexc('#3a2030'))):
        c.put(c.disc(44, 18, r) & (c.y < 31), col)
    flame = c.poly([(44, 11), (45.5, 15), (44.8, 17), (43.2, 17), (42.5, 15)])
    c.put(c.rect(43, 17, 45, 22), hexc('#c9b8a0'))
    c.put(flame, KR['spark1'])
    g.add(flame, L['warm'], 0.7 * fl)
    # the felt table with its wooden rim
    table = c.rect(0, 31, AW - 1, AH - 1)
    c.put(table, K3['felt1'])
    c.put(table & (c.y > 52), K3['felt0'])
    c.put(table & (np.abs(c.x - 44) < 30) & (c.y < 46), K3['felt2'], 0.5)
    c.put(c.rect(0, 31, AW - 1, 32), KR['wood1'])
    c.put(c.rect(0, 31, AW - 1, 31), KR['wood2'])
    for x, y, ph in ((40, 52, 0.0), (47, 54, 1.0), (52, 50, 2.0)):  # a few coins on the felt
        coin(c, g, x, y, math.pi / 2 * 0 + ph * 0.3, gain=0.2)


def discard_pile(c, g, x, y, n=4):
    for i in range(n):
        dx, dy = (-1, 1, 0, 2)[i % 4], -i
        card_rot(c, g, x + dx, y + dy, (-0.15, 0.1, -0.05, 0.2)[i % 4], size=1.6, face=True)


def juego_de_manos_frame(f):
    t = f / FRAMES
    c, g = Canvas(AW, AH), Canvas(AW, AH)
    card_table(c, g, t)
    discard_pile(c, g, JM_PILE[0], JM_PILE[1] + 4, 3)
    # the top card: rests on the pile, lifts and turns, vanishes in smoke, reappears on the other side, goes to the hand
    top = (JM_PILE[0] + 1, JM_PILE[1])
    if f <= 3 or f >= 22:
        a = 1.0 if f <= 3 else (f - 21) / 3.0
        card_rot(c, g, top[0], top[1], 0.12, size=1.6, face=True, alpha=a)
    elif f < JM_VANISH:
        k = ease_out((f - 3) / 3.0)
        card_rot(c, g, top[0], top[1] - 10 * k, 0.12 + k * 0.5, size=1.6, face=True)
    elif JM_APPEAR <= f <= 19:
        a = f - JM_APPEAR
        k = ease_out(min(1.0, (a + 1) / 4.0))
        y = JM_HAND[1] - 4 * math.sin(k * math.pi) - (0 if a < 5 else (a - 4) * 1.5)
        spin = math.cos(a * 0.9) if a < 4 else 1.0
        fade = 1.0 if a <= 5 else 1 - (a - 5) / 3.0
        paint_card(c, g, JM_HAND[0], y, spin, alpha=fade, glow=0.6, size=1.6)
    # violet smoke where it vanishes and where it appears, with a spiral of stars between
    for born, (x, y) in ((JM_VANISH, (top[0], top[1] - 10)), (JM_APPEAR - 1, JM_HAND)):
        a = f - born
        if 0 <= a <= 6:
            rng = np.random.default_rng(2701 + born)
            for j in range(7):
                ox, oy = rng.uniform(-5, 5), rng.uniform(-4, 4)
                r = (1.5 + a * 0.8) * rng.uniform(0.7, 1.2)
                m = c.disc(x + ox * (1 + a * 0.2), y + oy - a * 0.8, r)
                c.put(m, K['violet1'], 1 - a / 7)
                c.put(m & c.disc(x + ox - 0.6, y + oy - a * 0.8 - 0.6, r * 0.6), K['violet2'], 1 - a / 7)
                g.add(m, L['violet'], 0.2 * (1 - a / 7))
        if a == 0:
            star4(c, g, x, y, 5, col=K['violet3'], edge=K['violet2'], light=L['violet'], gain=1.3)
    if JM_VANISH <= f <= JM_APPEAR:
        k = (f - JM_VANISH) / (JM_APPEAR - JM_VANISH)
        for j in range(6):
            kk = max(0.0, k - j * 0.06)
            x, y = bez((top[0], top[1] - 10), (44.0, -6.0), JM_HAND, kk)
            m = c.pixel(x, y)
            c.put(m, K['violet4'] if j == 0 else K['violet3'], 1 - j / 6)
            g.add(m, L['violet'], 0.8 * (1 - j / 6))
        x, y = bez((top[0], top[1] - 10), (44.0, -6.0), JM_HAND, k)
        star4(c, g, x, y, 2, col=K['violet3'], edge=K['violet2'], light=L['violet'])
    sparks(c, g, f, JM_APPEAR + 3, JM_HAND[0], JM_HAND[1], 2703, 9, (0.6, 1.6), 5, VIOLET_SPARKS, L['violet'], gravity=0.03)
    motes(c, g, t, 2705, 7, (2, 6, 86, 46), K['violet3'], KR['gold3'], L['violet'], gain=0.45)
    vignette(c, 0.72)
    return c, g


# ---------------------------------------------------------------------------------------- Manto Raído
MR_PEG = (44.0, 8.0)          # the peg the cloak hangs from by its collar
MR_HIT = 8


def cloak_mask(c, t, x, y0, billow=0.0):
    """Her tattered cloak hanging from a peg: gathered at the collar, flaring towards a hem torn into tongues,
    waving in the wind. Returns (cloak, hood) masks; the hood hangs down its back."""
    m = np.zeros((AH, AW), bool)
    for y in range(int(y0), min(AH, int(y0) + 46)):
        k = (y - y0) / 46.0
        wav = math.sin((k * 2.0 - t * 2) * math.pi * 2) * (0.3 + 2.8 * k)
        push = billow * math.sin(k * math.pi) * 5
        half = 3.0 + 15.0 * k ** 0.7
        xa, xb = x - half + wav + push, x + half + wav * 0.6 + push
        row = (c.x[y] >= xa) & (c.x[y] <= xb)
        if k > 0.8:                                             # torn hem: tongues of cloth
            row &= ((c.x[y] - xa + (y % 3) * 2) % 6) < 6 * (1 - (k - 0.8) * 4.0)
        m[y] |= row
    hood = (((c.x - x - 1) / 7.0) ** 2 + ((c.y - y0 - 4) / 9.0) ** 2) <= 1
    hood &= c.y > y0 + 1
    return m | hood, hood


def tatter_holes(c, t, x, y0):
    holes = np.zeros((AH, AW), bool)
    for hx, hy, r in ((x - 6, y0 + 20, 1.6), (x + 5, y0 + 29, 2.2), (x - 2, y0 + 36, 1.4)):
        holes |= c.disc(hx + math.sin(t * math.pi * 4) * 0.5, hy, r)
    return holes


def belfry(c, g, t):
    gradient_bg(c, K['night0'], K['night2'], K['night1'], 0.58)
    twinkling_stars(c, g, t, 2801, 18, (2, 2, 86, 40))
    rng = np.random.default_rng(2803)                          # wind streaks
    for i in range(5):
        y = rng.uniform(8, 48)
        x = (rng.uniform(0, 120) + t * 120 * (1 + i % 2)) % 120 - 16
        c.put(c.rect(int(x), int(y), int(x) + 10, int(y)), KR['cloud1'])
    # stone arch of a belfry framing the scene, a beam across it with the peg
    arch = c.rect(0, 0, 5, 63) | c.rect(82, 0, 87, 63) | (c.rect(0, 0, 87, 6) & ~ellipse_ring(c, 44, 30, 40, 26, 30))
    c.put(arch, KR['stone1'])
    c.put(arch & ~shift(arch, 1, 0), KR['stone2'])
    beam = c.rect(6, 5, 81, 7)
    c.put(beam, KR['wood1'])
    c.put(c.rect(6, 5, 81, 5), KR['wood2'])
    c.put(c.rect(0, 54, AW - 1, AH - 1), KR['stone0'])
    c.put(c.rect(0, 54, AW - 1, 54), KR['stone3'])


def manto_raido_frame(f):
    t = f / FRAMES
    c, g = Canvas(AW, AH), Canvas(AW, AH)
    belfry(c, g, t)
    x, y0 = MR_PEG
    billow = {8: 1.0, 9: 1.0, 10: 0.7, 11: 0.4, 12: 0.2}.get(f, 0.0)
    whole, hood = cloak_mask(c, t, x, y0 + 1, billow=billow)
    holes = tatter_holes(c, t, x, y0)
    m = whole & ~holes
    c.put(outline_mask(m), K['ink'])
    c.put(m, K3['cloth1'])
    c.put(m & (np.sin((c.x - x) * 0.8 + (c.y - y0) * 0.15 + t * math.pi * 4) > 0.55), K3['cloth2'])    # folds
    c.put(m & ~shift(m, -1, 0), K3['cloth3'])                                                         # lit left edge
    c.put(m & ~shift(m, 1, 0), K3['cloth0'])
    c.put(hood & m, K3['cloth0'])
    c.put(hood & m & ~shift(hood, 0, -1), K3['cloth2'])
    clasp = c.disc(x, y0 + 3, 1.4)
    c.put(clasp, KR['gold2'])
    c.put(c.pixel(x - 0.5, y0 + 2.5), KR['gold3'])
    c.put(c.rect(int(x) - 1, int(y0) - 3, int(x), int(y0)), KR['iron2'])                             # the peg
    spoil = 13 <= f <= 20
    if spoil:                                                   # Despojo: the torn edges glow orange
        hem = m & ~shift(m, 0, -1) & (c.y > y0 + 37)            # the very ends of the torn tongues
        rims = dilate(holes) & m
        glow = hem | rims
        c.put(glow, K3['spoil1'])
        c.put(glow & ((c.x.astype(int) + c.y.astype(int) + f) % 3 == 0), K3['spoil2'])
        g.add(glow, L2['spoil'], 0.6)
    # an arrow flies at the cloak and is caught in it
    contact = (x + 10, y0 + 24)
    src = (96.0, 28.0)
    a_ang = math.atan2(contact[1] - src[1], contact[0] + 3 - src[0])
    if 4 <= f < MR_HIT:
        k = (f - 3) / (MR_HIT - 3)
        arrow(c, g, src[0] + (contact[0] + 3 - src[0]) * k, src[1] + (contact[1] - src[1]) * k, a_ang)
    elif MR_HIT <= f <= MR_HIT + 7:
        a = f - MR_HIT
        drop = 0 if a < 3 else (a - 3) ** 2 * 0.6
        arrow(c, g, contact[0] + 2, contact[1] + drop, a_ang + (0 if a < 3 else (a - 2) * 0.35), a=1.0 if a < 6 else 0.5)
    if MR_HIT <= f <= MR_HIT + 7:
        ward_rings(c, g, x + 2, y0 + 24, f - MR_HIT, size=14, squash=1.2)
    if 14 <= f <= 21:                                           # Despojo: a second ward (+7)
        ward_rings(c, g, x + 2, y0 + 24, f - 14, size=20, squash=1.2)
    if f == 14:
        star4(c, g, x + 2, y0 + 24, 4, core=K['white'], col=K3['spoil3'], edge=K3['spoil2'], light=L2['spoil'])
    if MR_HIT <= f <= MR_HIT + 1:
        star4(c, g, contact[0], contact[1], 5 if f == MR_HIT else 3, core=K['white'], col=KB['b3'], edge=KB['b2'], light=LB)
    sparks(c, g, f, 14, x, y0 + 40, 2805, 10, (0.6, 1.8), 6, SPOIL_SPARKS, L2['spoil'], gravity=0.05)
    motes(c, g, t, 2807, 7, (8, 6, 80, 50), K['violet3'], K3['spoil2'] if spoil else KB['b2'], L['violet'], gain=0.45)
    vignette(c, 0.72)
    return c, g


# ---------------------------------------------------------------------------------------- Reflejos
RF_DAGGER = (22.0, 44.0)      # guard
RF_ANG = -0.5
RF_DROPS = ((7, 50.0), (10, 62.0), (13, 40.0))   # (frame it is cut, x)
RF_CUT_Y = 30.0


def cave(c, g, t, slow):
    gradient_bg(c, K['teal0'], K['teal2'], K['teal1'], 0.6)
    if slow > 0:                                                # time slows: everything turns a colder blue
        c.put(np.ones((AH, AW), bool), K3['water2'], 0.25 * slow)
    # stalactites
    c.put(c.disc(56, 30, 30), K3['water3'], 0.18)                # cold light from a crystal seam
    st = np.zeros((AH, AW), bool)
    for x, w, h in ((4, 7, 12), (17, 5, 8), (34, 9, 14), (48, 6, 10), (60, 8, 15), (77, 7, 11)):
        st |= c.poly([(x, 0), (x + w, 0), (x + w / 2 + 0.5, h)])
    c.put(outline_mask(st), K['ink'])
    c.put(st, KR['stone2'])
    c.put(st & ~shift(st, 1, 0), KR['stone3'])
    for cx_, cy_ in ((80, 46), (84, 50), (6, 48)):              # crystals glinting at the water's edge
        cr = c.poly([(cx_ - 1.5, cy_ + 4), (cx_, cy_ - 3), (cx_ + 1.5, cy_ + 4)])
        c.put(cr, K['cyan1'])
        c.put(cr & (c.x < cx_), K['cyan3'])
        g.add(cr, L['cyan'], 0.35)
    c.put(c.rect(0, 0, AW - 1, 1), KR['stone0'])
    pool = c.rect(0, 52, AW - 1, AH - 1)
    c.put(pool, K3['water1'])
    for i, y in enumerate((54, 57, 61)):
        off = (t * 44 * (1 if i % 2 else -1)) % 14
        for x in np.arange(-14 + off, AW, 14):
            c.put(c.rect(int(x), y, int(x) + 4, y), K3['water2'])


def drop_mask(c, x, y, s=1.4):
    d = (((c.x - x) / (1.6 * s)) ** 2 + ((c.y - y - 0.8) / (1.9 * s)) ** 2) <= 1
    d |= (np.abs(c.x - x) <= np.clip((y + 0.5 - c.y) * 0.45, 0, None)) & (c.y > y - 2.5 * s) & (c.y <= y + 0.5)
    return d


def reflejos_frame(f):
    t = f / FRAMES
    slow = 1.0 if 6 <= f <= 14 else 0.5 if f in (5, 15) else 0.0
    c, g = Canvas(AW, AH), Canvas(AW, AH)
    cave(c, g, t, slow)
    # the power: a rune circle under the blade and rising chevrons (destreza), from the end of the cuts on
    power = 0.0 if f < 14 else min(1.0, (f - 13) / 3.0) if f <= 20 else max(0.0, 1 - (f - 20) / 3.0)
    if power > 0:
        ring = ellipse_ring(c, RF_DAGGER[0] + 10, 56, 14, 3.5, 0.6)
        ang = np.arctan2((c.y - 56) / 3.5, (c.x - RF_DAGGER[0] - 10) / 14)
        ring &= ((ang / (2 * math.pi) * 12 + t * 3) % 1) < 0.7
        c.put(ring, KB['b3'], power)
        g.add(ring, LB, 0.9 * power)
        for i in range(3):
            a = (f - 14 - i * 1.5)
            if a < 0:
                continue
            cy = 50 - (a % 8) * 2.6
            cx = RF_DAGGER[0] + 4 + i * 6
            m = c.line(cx - 2, cy + 2, cx, cy) | c.line(cx, cy, cx + 2, cy + 2)
            k = (1 - (a % 8) / 8) * power
            c.put(m, KB['b3'], k)
            g.add(m, LB, 0.9 * k)
    # drops fall from the stalactites; the blade flicks out and splits each one in mid-air
    for i, (cut, x) in enumerate(RF_DROPS):
        speed = 0.35 if slow > 0.9 else 1.0
        a = f - (cut - 4)
        if -2 <= a < 4:
            y = 8 + (RF_CUT_Y - 8) * max(0.0, (a + 2) / 6.0)
            m = drop_mask(c, x, y)
            c.put(outline_mask(m), K['ink'])
            c.put(m, K3['water3'])
            c.put(m & (c.x < x), K['moon2'])
            g.add(m, L['blue'], 0.25)
        elif 4 <= a <= 9:
            b = a - 4
            for side in (-1, 1):                                 # the two halves part and fall slowly
                hx, hy = x + side * (1 + b * 0.8), RF_CUT_Y + b * b * 0.35 * speed + b * 0.5
                m = drop_mask(c, hx, hy, 1.1) & ((c.x - x) * side > 0)
                c.put(m, K3['water3'], 1 - b / 7)
                c.put(m & (c.y < hy), K['moon2'], 1 - b / 7)
            if b == 0:
                star4(c, g, x, RF_CUT_Y, 6, gain=1.4)
                slash_line(c, g, x, RF_CUT_Y, 0.15, 10, 1.8, CPAL, L['cyan'])
    # the dagger: a quick turn of the wrist towards each drop, a ghost where it was
    poses = {6: (0.3, 4), 7: (-0.05, 10), 8: (-0.2, 8), 9: (-0.15, 10), 10: (-0.15, 16), 11: (-0.3, 12), 12: (-0.3, 6),
             13: (-0.25, 2), 14: (-0.4, 1)}
    spin, along = poses.get(f, (0.0, 0.0))
    # aim at the drop being cut
    aim = {7: RF_DROPS[0][1], 10: RF_DROPS[1][1], 13: RF_DROPS[2][1]}
    ang = RF_ANG + spin
    if f in aim:
        ang = math.atan2(RF_CUT_Y - RF_DAGGER[1], aim[f] - RF_DAGGER[0])
        along = math.hypot(aim[f] - RF_DAGGER[0], RF_CUT_Y - RF_DAGGER[1]) - 40 * 0.62 + 1
    ux, uy = dir_of(ang)
    gx_, gy_ = RF_DAGGER[0] + ux * along, RF_DAGGER[1] + uy * along + (math.sin(t * math.pi * 4) * 0.7 if slow == 0 else 0)
    if f in aim:
        dagger(c, g, RF_DAGGER[0], RF_DAGGER[1], RF_ANG, 0.62, t * 2, ghost=(K['cyan3'], 0.5, L['cyan']))
    dagger(c, g, round(gx_), round(gy_), ang, 0.62, t * 2 + slow, -2 + (f - 16) * 10.0 if 16 <= f <= 20 else None,
           rune_gain=1.0 + 0.8 * slow + 0.6 * power)
    motes(c, g, t, 2901, 7, (2, 10, 86, 50), K['cyan3'], KB['b3'], L['cyan'], gain=0.45)
    vignette(c, 0.72)
    return c, g


# ---------------------------------------------------------------------------------------- Escudo Reactivo
ER_GUARD = (26.0, 50.0)
ER_ANG = -1.3
ER_SNAP = 7
ER_HIT = 8


def trap_corridor(c, g, t):
    gradient_bg(c, K['night0'], K['vio1'], K['night1'], 0.6)
    wall = c.rect(0, 0, AW - 1, 52)
    c.put(wall, K3['temple1'])
    for row, y in enumerate(range(4, 52, 7)):
        c.put(c.rect(0, y, AW - 1, y), K3['temple0'])
        off = 0 if row % 2 else 8
        for x in range(off, AW, 16):
            c.put(c.rect(x, y + 1, x, y + 6), K3['temple0'])
    # the trap: a carved face on the right wall with a slit mouth
    face = c.rect(76, 22, 87, 38)
    c.put(face, K3['temple2'])
    c.put(c.rect(76, 22, 76, 38), K3['temple3'])
    c.put(c.rect(78, 26, 80, 27) | c.rect(83, 26, 85, 27), K['ink'])
    c.put(c.rect(78, 31, 87, 32), K['ink'])
    c.put(c.rect(0, 52, AW - 1, AH - 1), K3['temple0'])
    c.put(c.rect(0, 52, AW - 1, 52), K3['temple3'])
    # a pressure plate glowing under the floor
    plate = c.rect(40, 53, 54, 54)
    c.put(plate, K3['temple2'])


def rune_buckler(c, g, x, y, r, t, a, hit=0.0):
    """A round buckler of light: ring of runes around a hexagon net; flashes on impact."""
    d = np.hypot(c.x - x, (c.y - y) * 1.15)
    ang = np.arctan2(c.y - y, c.x - x)
    ring = (np.abs(d - r) < 0.7)
    runes = (np.abs(d - (r - 2.2)) < 0.5) & ((((ang / (2 * math.pi)) * 14 + t * 2) % 1) < 0.5)
    inside = d < r - 0.5
    hexes = inside & ((((c.x - x) % 5) < 0.6) | (((c.x - x) * 0.5 + (c.y - y) * 0.87) % 5 < 0.6))
    c.put(inside, KB['b0'], 0.45 * a)
    c.put(hexes, KB['b1'], a)
    c.put(runes, KB['b3'] if hit > 0.3 else KB['b2'], a)
    c.put(ring, K['white'] if hit > 0.5 else KB['b3'], a)
    g.add(ring, LB, (0.8 + hit) * a)
    g.add(runes | hexes, LB, (0.3 + 0.6 * hit) * a)


def escudo_reactivo_frame(f):
    t = f / FRAMES
    c, g = Canvas(AW, AH), Canvas(AW, AH)
    trap_corridor(c, g, t)
    contact = (40.0, 31.5)
    # the bolt: fired from the trap's mouth, stopped by the buckler, shatters
    if 4 <= f < ER_HIT:
        k = (f - 3) / (ER_HIT - 3)
        x = 76 + (contact[0] + 2 - 76) * k
        arrow(c, g, x, 31.5, math.pi, s=1.1)
        if f == 4:
            star4(c, g, 76, 31.5, 2, core=K['white'], col=KR['spark2'], edge=KR['spark1'], light=L['warm'])
    rng = np.random.default_rng(3001)
    for i in range(8):                                          # bolt splinters
        a = f - ER_HIT
        if 0 < a <= 6:
            a0 = rng.uniform(-1.2, 1.2)
            sp = rng.uniform(0.8, 2.2)
            px = contact[0] + 3 + math.cos(a0) * sp * a
            py = contact[1] - math.sin(a0) * sp * a + 0.3 * a * a
            c.put(c.pixel(px, py) | c.pixel(px + 1, py), KR['wood2'] if i % 2 else K['steel3'], 1 - a / 7)
    # the dagger in guard; the buckler snaps open in front of it just in time
    bob = math.sin(t * math.pi * 4) * 0.7 if f <= 5 or f >= 18 else 0
    push = {ER_HIT: -1, ER_HIT + 1: -1}.get(f, 0)
    dagger(c, g, ER_GUARD[0] + push, round(ER_GUARD[1] + bob), ER_ANG, 0.66, t * 2,
           -2 + (f - 1) * 10.0 if 1 <= f <= 4 else None, rune_gain=1.6 if ER_SNAP <= f <= 12 else 1.0)
    if ER_SNAP <= f <= 15:
        a = f - ER_SNAP
        r = {0: 4.0, 1: 9.0}.get(a, 10.0)
        fade = 1.0 if a <= 5 else max(0.0, 1 - (a - 5) / 3.0)
        rune_buckler(c, g, contact[0], contact[1], r, t, fade, hit=1.0 if a == 1 else 0.5 if a == 2 else 0.0)
        if a in (1, 2, 3):                                      # ripples across it
            rip = np.abs(np.hypot(c.x - contact[0], (c.y - contact[1]) * 1.15) - a * 3) < 0.6
            rip &= np.hypot(c.x - contact[0], (c.y - contact[1]) * 1.15) < r
            c.put(rip, KB['b3'])
    if f == ER_SNAP:
        star4(c, g, contact[0], contact[1], 3, core=K['white'], col=KB['b3'], edge=KB['b2'], light=LB)
    if ER_HIT <= f <= ER_HIT + 2:
        star4(c, g, contact[0] + 2, contact[1], {0: 6, 1: 4, 2: 2}[f - ER_HIT], core=K['white'], col=KR['spark2'],
              edge=KR['spark1'], light=L['warm'], gain=1.3)
    sparks(c, g, f, ER_HIT, contact[0] + 2, contact[1], 3003, 8, (0.8, 2.0), 5, (KB['b3'], KB['b2'], KB['b1']), LB,
           gravity=0.1, spread=(-1.3, 1.3))
    rising_card(c, g, f, 12, 62, 70, 28)                        # roba 1
    if 14 <= f <= 20:                                           # Combo: +1 de maná
        k = ease_out((f - 14) / 6.0)
        mana_drop(c, g, round(74), round(46 - 22 * k), 0.8 + 0.5 * min(1.0, (f - 13) / 2.0), gain=1.0 + 0.5 * (f == 20))
    if f == 20:
        star4(c, g, 74, 24, 3, col=KR['mana3'], edge=KR['mana2'], light=LR['mana'])
    sparks(c, g, f, 20, 74, 24, 3005, 9, (0.5, 1.5), 5, MANA_SPARKS, LR['mana'], gravity=-0.02)
    motes(c, g, t, 3007, 6, (2, 6, 86, 50), KB['b2'], K['violet3'], LB, gain=0.4)
    vignette(c, 0.72)
    return c, g


# ---------------------------------------------------------------------------------------- Guardia Evasiva
GE_X = (44.0, 34.0)
GE_HIT = 8
GE_ENEMIES = ((17.0, 14.0), (71.0, 14.0))


def misty_clearing(c, g, t):
    gradient_bg(c, K['teal0'], K['teal2'], K['teal1'], 0.6)
    twinkling_stars(c, g, t, 3101, 10, (16, 2, 72, 16))
    for x, h in ((0, 40), (8, 34), (74, 38), (82, 44)):
        c.put(c.poly([(x - 6, 56), (x + 2, 56 - h), (x + 10, 56)]), K['roof2'])
    c.put(c.rect(0, 54, AW - 1, AH - 1), K['roof'])
    c.put(c.rect(0, 54, AW - 1, 54), K['teal3'])


def guardia_evasiva_frame(f):
    t = f / FRAMES
    c, g = Canvas(AW, AH), Canvas(AW, AH)
    misty_clearing(c, g, t)
    weak = 0.0 if f < 13 else min(1.0, (f - 12) / 3.0) if f <= 20 else max(0.0, 1 - (f - 20) / 3.0)
    # the two enemies (sigils in the corners), dulled by the mist with Combo
    for i, (ex, ey) in enumerate(GE_ENEMIES):
        col = K3['crim3'] if weak < 0.5 else K3['crim1']
        sigil(c, g, ex, ey, 5.5, 5.5, t, 0.9 - 0.5 * weak, col=col, light=LR['crimson'])
        if weak > 0:
            for j in range(2):                                  # blue chevrons pointing down: they hit softer
                a = (f - 13 - j * 2) % 6
                cy = ey + 6 + a * 1.4
                m = c.line(ex - 2, cy - 2, ex, cy) | c.line(ex, cy, ex + 2, cy - 2)
                c.put(m, KB['b3'], weak * (1 - a / 6))
                g.add(m, LB, 0.8 * weak * (1 - a / 6))
    # their arrows fly in from both sides and stop on the ward
    for i, (sx, sy) in enumerate(GE_ENEMIES):
        side = -1 if i == 0 else 1
        if 4 <= f < GE_HIT:
            k = (f - 3) / (GE_HIT - 3)
            ex, ey = GE_X[0] + side * 9, GE_X[1] - 2
            arrow(c, g, sx + (ex - sx) * k, sy + 4 + (ey - sy - 4) * k, math.atan2(ey - sy - 4, ex - sx), s=1.1)
        elif GE_HIT <= f <= GE_HIT + 5:
            a = f - GE_HIT
            ex, ey = GE_X[0] + side * (9 + a * 0.8), GE_X[1] - 2 + 0.5 * a * a
            arrow(c, g, ex, ey, math.atan2(GE_X[1] - 2 - sy - 4, GE_X[0] + side * 9 - sx) + side * a * 0.5, a=1 - a / 7, s=1.1)
    # two crossed daggers in guard
    for side in (-1, 1):
        ang = -math.pi / 2 - side * 0.7
        ux, uy = dir_of(ang)
        bob = math.sin((t + (0.25 if side > 0 else 0)) * math.pi * 4) * 0.7
        dagger(c, g, GE_X[0] - ux * 16 * 0.6, GE_X[1] - uy * 16 * 0.6 + 12 + bob, ang, 0.6, t * 2,
               -2 + (f - 1 - side) * 10.0 if 1 <= f - side <= 4 else None, rune_gain=1.6 if 6 <= f <= 10 else 1.0)
    # the big ward (10 de escudo)
    if 6 <= f <= 17:
        ward_rings(c, g, GE_X[0], GE_X[1] + 2, f - 6, size=20, squash=0.85, life=11)
    if GE_HIT <= f <= GE_HIT + 1:
        for side in (-1, 1):
            star4(c, g, GE_X[0] + side * 9, GE_X[1] - 2, 4 if f == GE_HIT else 2, core=K['white'], col=KB['b3'],
                  edge=KB['b2'], light=LB, gain=1.2)
    # Combo: a pale mist spreads from the ward to the enemies
    if 12 <= f <= 21:
        a = f - 12
        rng = np.random.default_rng(3103)
        for j in range(10):
            side = -1 if j % 2 else 1
            k = min(1.0, a / 6.0)
            x = GE_X[0] + side * (6 + 26 * k) * rng.uniform(0.6, 1.0)
            y = GE_X[1] - 8 * k * rng.uniform(0.5, 1.5) + rng.uniform(-4, 4)
            r = 2.5 + 2.5 * k
            fa = (0.55 if a < 6 else max(0.0, 0.55 * (1 - (a - 5) / 4.0)))
            smoke_puff(c, g, x, y, r, fa)
    motes(c, g, t, 3105, 7, (2, 6, 86, 52), KB['b2'], K['cyan3'], LB, gain=0.45)
    vignette(c, 0.72)
    return c, g


ILLUSTRATIONS = {
    'corte_rapido': corte_rapido_frame,
    'tajo': tajo_frame,
    'punalada_trapera': punalada_trapera_frame,
    'golpe_desesperado': golpe_desesperado_frame,
    'instinto': instinto_frame,
    'juego_de_manos': juego_de_manos_frame,
    'manto_raido': manto_raido_frame,
    'reflejos': reflejos_frame,
    'escudo_reactivo': escudo_reactivo_frame,
    'guardia_evasiva': guardia_evasiva_frame,
}


def render(card_id):
    fn = ILLUSTRATIONS[card_id]
    base, glow = [], []
    for f in range(FRAMES):
        b, gl = fn(f)
        base.append(b)
        glow.append(gl)
    return base, glow
