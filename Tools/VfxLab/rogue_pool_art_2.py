"""Animated pixel-art illustrations for ten more of the rogue's loot cards (88x64, 24 frames at 12 fps, seamless loop).

Second batch after rogue_pool_art.py, same technique and helpers (docs/art-style.md): every frame has a base image
(opaque pixel art, readable without bloom) and an emissive light map, baked for the game by export_game_card_art.py.
No characters: her runed daggers, the cards she plays and throws away, her poisons and her smoke.

  chatarra          Chatarra (roba 1; Despojo: +1 de maná): a discarded card drops onto a scrap heap, the heap rattles,
                    a card springs out of it and a blue mana drop rises (Despojo).
  deshacerse        Deshacerse (4 de escudo; descarta una carta al azar): a floating card is cut in two, the halves burn
                    into violet embers and the embers gather into a blue ward.
  finta_doble       Finta Doble (3 de escudo; Combo: 3 de daño a un enemigo al azar): two violet ghost daggers feint
                    from both sides, cross in front of the real one (ward), and one darts at a sigil picked at random.
  quiebro           Quiebro (6 de escudo; Combo: roba 1): an arrow flies at the dagger, a twist of the blade turns it
                    away in sparks behind a blue ward, and a card flips up out of the dark (Combo).
  tirar_y_cortar    Tirar y Cortar (6 de daño; Despojo: +6 de daño): a card tossed in the air is cut, the blade catches
                    the orange light of Despojo and an X of crescents splits a wooden training post.
"""
import math

import numpy as np

from pixelkit import Canvas, hexc, dilate, erode, shift
from rogue_art import (AW, AH, FRAMES, K, L, ease_out, ease_in_out, frac, gradient_bg, vignette, star4, motes,
                       outline_mask, crescent, dagger_labels, paint_dagger, arc_dagger)
from mage_art import twinkling_stars, sparks, ellipse_ring
from warrior_art import shake, falling_sparks, dust_puffs, WARM_SPARKS, CYAN_SPARKS, axe_labels, paint_axe
from rogue_pool_art import (KR, LR, KB, LB, VIOLET_SPARKS, GOLD_SPARKS, dir_of, dagger, centred_dagger, tip_of,
                            shimmer, speed_lines, converge, clipped, mana_drop, brick_wall, cobbles, sigil,
                            diamond_ring, sage)

K2 = dict(KR)
K2.update({
    'spoil0': hexc('#4a1a06'), 'spoil1': hexc('#9c3f10'), 'spoil2': hexc('#f0822a'), 'spoil3': hexc('#ffd08a'),
    'pois0': hexc('#0c2410'), 'pois1': hexc('#1d5420'), 'pois2': hexc('#3f9a2c'), 'pois3': hexc('#8fe04a'),
    'pois4': hexc('#e2ffb0'),
    'smoke0': hexc('#16121f'), 'smoke1': hexc('#2a2436'), 'smoke2': hexc('#3f3750'), 'smoke3': hexc('#5f5675'),
    'smoke4': hexc('#8a809e'),
    'glass0': hexc('#10202e'), 'glass1': hexc('#24445a'), 'glass2': hexc('#5f8fa8'),
})
L2 = dict(LR)
L2.update({'spoil': np.array([1.0, 0.5, 0.15, 1]), 'poison': np.array([0.35, 1.0, 0.25, 1])})
SPOIL_SPARKS = (K2['spoil3'], K2['spoil2'], K2['spoil1'])
POISON_SPARKS = (K2['pois4'], K2['pois3'], K2['pois1'])


# ---------------------------------------------------------------------------------------- shared bits
def card_mask(c, x, y, wscale=1.0, size=1.0, w=4.5, h=6.5):
    w, h = w * size, h * size
    hw = max(0.6, w * abs(wscale))
    dx, dy = np.abs(c.x - x), np.abs(c.y - y)
    m = (dx <= hw) & (dy <= h)
    return m & ~((dx > hw - 1) & (dy > h - 1))


def paint_card(c, g, x, y, wscale=1.0, face=False, alpha=1.0, glow=0.0, keep=None, size=1.0):
    """A small card seen from the front: violet back with a gold border and a cyan rune, or a pale face (a card being
    thrown away). wscale = |cos| of its spin (it narrows to an edge). keep = optional mask it is clipped to."""
    m = card_mask(c, x, y, wscale, size)
    if keep is not None:
        m &= keep
    hw = 4.5 * size * abs(wscale)
    c.put(outline_mask(m) if keep is None else outline_mask(m) & dilate(keep), K['ink'], alpha)
    inner = erode(m)
    if face:
        c.put(m, KR['paper1'], alpha)
        c.put(inner, KR['paper3'], alpha)
        c.put(inner & (c.x < x - hw * 0.3), KR['paper2'], alpha)
        for yy in (-3, -1, 1, 3):
            yy = yy * size
            c.put(inner & (np.abs(c.y - (y + yy)) < 0.5) & (np.abs(c.x - x) < hw - 1.5), KR['paper1'], alpha)
    else:
        c.put(m, KR['gold1'], alpha)
        c.put(m & (c.y < y - 5 * size) & (c.x < x), KR['gold3'], alpha)
        c.put(inner, K['vio2'], alpha)
        c.put(inner & (c.x < x - hw * 0.4), K['vio3'], alpha)
        if hw > 1.8:
            rune = inner & ((np.abs(c.x - x) / max(1.0, hw * 0.5) + np.abs(c.y - y) / (3.0 * size)) <= 1.0)
            c.put(rune, K['cyan2'], alpha)
            c.put(rune & (np.abs(c.x - x) < 0.6) & (np.abs(c.y - y) < 0.6), K['cyan4'], alpha)
            g.add(rune, L['cyan'], (0.5 + glow) * alpha)
    if glow > 0:
        g.add(outline_mask(m), L['violet'], glow * alpha)
    return m


def burn_away(c, g, mask, dist, p, seed, embers=(K['violet4'], K['violet3']), light=None):
    """Erase the burnt part of something already painted: pixels whose (dist + noise) is under p * reach turn back
    into the background painted below (they are cleared to `under`), the burning edge glows as embers."""
    light = L['violet'] if light is None else light
    rng = np.random.default_rng(seed)
    noise = rng.random((c.h, c.w)) * 2.5
    reach = (dist[mask].max() if mask.any() else 1) + 3
    d = dist + noise
    burnt = mask & (d < p * reach)
    edge = mask & ~burnt & (d < p * reach + 1.4)
    return burnt, edge, light


def bez(p0, p1, p2, u):
    return ((1 - u) ** 2 * p0[0] + 2 * (1 - u) * u * p1[0] + u * u * p2[0],
            (1 - u) ** 2 * p0[1] + 2 * (1 - u) * u * p1[1] + u * u * p2[1])


def junk_heap(c, g, t, f, rattle=0.0):
    """A heap of scrap: a cracked buckler, a broken sword stuck hilt up, a cog, a bottle, chain, nails, a red rag."""
    def jit(i):
        if rattle <= 0:
            return 0, 0
        r = np.random.default_rng(1203 + f * 7 + i)
        return int(round(r.uniform(-1, 1) * rattle)), -int(round(r.random() * rattle))

    base = c.poly([(10, 58), (18, 51), (28, 46), (40, 42), (52, 42), (62, 45), (72, 51), (80, 58)])
    c.put(outline_mask(base) & (c.y < 58), K['ink'])
    c.put(base, K2['smoke0'])
    c.put(base & ~shift(base, 0, 2), K2['smoke1'])
    c.put(base & ~shift(base, 0, 1), K2['smoke2'])
    rng = np.random.default_rng(1201)
    for i in range(14):                                         # odd bits poking out of the mound
        x, y = rng.uniform(16, 74), rng.uniform(48, 57)
        if base[int(y), int(x)]:
            c.put(c.rect(int(x), int(y), int(x) + int(rng.integers(1, 3)), int(y)), KR['iron2'] if i % 3 else KR['wood1'])
    # broken sword stuck hilt up
    dx, dy = jit(2)
    lab, u, v = dagger_labels(c, 47 + dx, 33 + dy, math.pi / 2 + 0.3, s=0.72)
    lab[u > 13] = 0
    paint_dagger(c, g, lab, u, t * 2, None, rune_gain=0.3)
    # cracked buckler leaning on the left
    dx, dy = jit(0)
    bx, by = 28 + dx, 47 + dy
    shield = (((c.x - bx) / 7.5) ** 2 + ((c.y - by) / 8.5) ** 2) <= 1
    shield &= ~((c.x > bx + 2) & (c.y < by - 3) & (c.x - bx > (c.y - by) * -0.6 + 1))   # a chunk broken off
    rim = shield & ~erode(erode(shield))
    c.put(outline_mask(shield), K['ink'])
    c.put(shield, KR['wood1'])
    c.put(shield & (c.x < bx - 1), KR['wood2'])
    c.put(rim, KR['iron2'])
    c.put(rim & (c.y < by), KR['iron3'])
    c.put(c.disc(bx, by, 1.6), K['steel2'])
    c.put(c.line(bx, by, bx - 3, by + 5) & shield, K['ink'])
    # cog on the right
    dx, dy = jit(1)
    cx, cy = 62 + dx, 46 + dy
    ang = np.arctan2(c.y - cy, c.x - cx)
    rr = np.hypot(c.x - cx, c.y - cy)
    cog = (rr <= 4.6 + 1.4 * ((np.floor((ang + math.pi) / (2 * math.pi) * 8 + t * 0) % 2) == 0)) & (rr > 1.6)
    c.put(outline_mask(cog), K['ink'])
    c.put(cog, KR['iron2'])
    c.put(cog & (c.x + c.y < cx + cy - 1), KR['iron3'])
    # bottle, chain, rag, nails
    dx, dy = jit(3)
    bott = c.poly([(36 + dx, 51 + dy), (40 + dx, 47 + dy), (43 + dx, 49 + dy), (39 + dx, 53 + dy)])
    c.put(outline_mask(bott), K['ink'])
    c.put(bott, K2['glass1'])
    c.put(c.pixel(40 + dx, 49 + dy), K2['glass2'])
    dx, dy = jit(4)
    rag = c.poly([(64 + dx, 53 + dy), (71 + dx, 51 + dy), (74 + dx, 55 + dy), (67 + dx, 57 + dy)])
    c.put(outline_mask(rag), K['ink'])
    c.put(rag, KR['crim2'])
    c.put(rag & (c.y < 53 + dy), KR['crim3'])
    dx, dy = jit(5)
    for i in range(3):
        link = ellipse_ring(c, 17 + i * 3 + dx, 55 - (i % 2) + dy, 1.6, 1.1, 0.5)
        c.put(link, KR['iron3'] if i % 2 else KR['iron2'])
    for i, (nx, ny, ex, ey) in enumerate(((52, 53, 55, 50), (44, 56, 48, 57))):
        dx, dy = jit(6 + i)
        c.put(c.line(nx + dx, ny + dy, ex + dx, ey + dy), KR['iron3'])
    # glints wander over the metal
    for i, (gx_, gy_) in enumerate(((26, 41), (63, 42), (47, 26), (21, 54))):
        if (f + i * 6) % 24 < 3:
            star4(c, g, gx_, gy_, 1, core=K['white'], col=K['steel3'], edge=K['steel2'], light=L['white'], gain=0.5)


# ---------------------------------------------------------------------------------------- Chatarra
H_TOP = (46.0, 40.0)


def chatarra_frame(f):
    t = f / FRAMES
    c, g = Canvas(AW, AH), Canvas(AW, AH)
    brick_wall(c, g, t, lamp=(76, 10))
    cobbles(c, 57)
    rattle = {5: 1.0, 6: 1.0, 7: 0.6, 8: 0.4, 12: 0.6, 13: 0.3}.get(f, 0.0)
    junk_heap(c, g, t, f, rattle)
    dust_puffs(c, H_TOP[0], 56, f, 5, 4, 1.8, rise=0.3, life=6, seed=1211)
    # 1) Despojo: a card already thrown away drops onto the heap
    if 1 <= f <= 5:
        k = (f - 1) / 4.0
        x, y = 66 - 20 * k, -8 + (H_TOP[1] - 2 + 8) * k * k
        paint_card(c, g, x, y, math.cos(k * math.pi * 3), face=True)
    # 2) a card springs out of the scrap (roba 1), spins up and goes to the hand
    if 8 <= f <= 17:
        k = ease_out(min(1.0, (f - 7) / 6.0))
        x, y = bez(H_TOP, (36.0, 8.0), (20.0, 14.0), k)
        spin = math.cos((f - 8) * 0.9) if f < 14 else 1.0
        fade = 1.0 if f <= 15 else 1 - (f - 15) / 3.0
        paint_card(c, g, x, y, spin, alpha=fade, glow=0.4)
        if f <= 13:
            for j in range(1, 4):
                px, py = bez(H_TOP, (36.0, 8.0), (20.0, 14.0), max(0.0, k - j * 0.06))
                m = c.pixel(px + (j % 2), py + 4)
                c.put(m, K['violet3'], 1 - j / 4)
                g.add(m, L['violet'], 0.5 * (1 - j / 4))
    if 14 <= f <= 17:
        star4(c, g, 20, 14, {14: 2, 15: 4, 16: 2, 17: 1}[f], col=K['violet3'], edge=K['violet2'], light=L['violet'])
    sparks(c, g, f, 16, 20, 14, 1213, 8, (0.6, 1.6), 5, VIOLET_SPARKS, L['violet'], gravity=0.05)
    # 3) Despojo: +1 de maná rises out of the heap
    if 11 <= f <= 18:
        k = ease_out((f - 11) / 7.0)
        mana_drop(c, g, round(H_TOP[0] + 6 + math.sin(k * math.pi * 2) * 1.5), round(H_TOP[1] - 4 - 22 * k),
                  0.8 + 0.5 * min(1.0, (f - 10) / 2.0), gain=1.0 + 0.5 * (f == 18))
    if f == 18:
        star4(c, g, H_TOP[0] + 6, H_TOP[1] - 26, 3, col=KR['mana3'], edge=KR['mana2'], light=LR['mana'])
    sparks(c, g, f, 18, H_TOP[0] + 6, H_TOP[1] - 26, 1215, 9, (0.5, 1.5), 5, (KR['mana3'], KR['mana2'], KR['mana1']),
           LR['mana'], gravity=-0.02)
    sparks(c, g, f, 5, H_TOP[0], H_TOP[1], 1217, 8, (0.8, 2.0), 5, WARM_SPARKS, L['warm'], gravity=0.2, spread=(0.3, 2.8))
    motes(c, g, t, 1219, 6, (2, 6, 86, 50), K['violet3'], KR['mana2'], L['violet'], gain=0.4)
    vignette(c, 0.72)
    return c, g


# ---------------------------------------------------------------------------------------- Deshacerse
D2_CARD = (44.0, 25.0)
D2_HOME = (12.0, 50.0)
D2_ANG = -0.8
D2_S = 0.62
D2_ARC_C, D2_ARC_R = (44.0, 49.0), 24.0
D2_HEADS = {7: math.radians(150), 8: math.radians(92), 9: math.radians(34)}
D2_CUT = 8
D2_SIZE = 1.5


def bridge_scene(c, g, t):
    gradient_bg(c, K['teal0'], K['teal2'], K['teal1'], 0.6)
    c.put(c.disc(44, 24, 28), K['teal3'], 0.25)
    c.put(c.disc(44, 24, 18), K['teal3'], 0.45)
    twinkling_stars(c, g, t, 1301, 12, (2, 2, 86, 30), avoid=(44, 24, 20))
    # far lanterns along the river bank
    for i, x in enumerate((8, 22, 64, 80)):
        lit = 0.6 + 0.4 * math.sin((t * (1 + i % 2) + i * 0.27) * math.pi * 2)
        m = c.rect(x, 43, x, 43)
        c.put(m, K['window'] if lit > 0.4 else K['window2'])
        g.add(m, L['warm'], 0.25 * lit)
    # stone bridge railing with balusters
    rail = c.rect(0, 50, AW - 1, 51) | c.rect(0, 58, AW - 1, AH - 1)
    for x in range(2, AW, 7):
        rail |= c.rect(x, 52, x + 2, 57) & ~c.rect(x, 54, x, 55) & ~c.rect(x + 2, 54, x + 2, 55)
    c.put(rail, K['roof'])
    c.put(rail & ~shift(rail, 0, 1), K['teal3'])


def card_halves(c, g, f, t):
    """The cut card: two halves drifting apart along the cut, burning from the cut into violet embers."""
    a = f - D2_CUT
    ang = math.radians(-35)                                    # the cut line through the card
    nx, ny = -math.sin(ang), math.cos(ang)
    p = min(1.0, a / 5.0)
    for side in (-1, 1):
        off = side * (1 + a * 0.9)
        cx = D2_CARD[0] + nx * off + side * a * 0.6
        cy = D2_CARD[1] + ny * off + a * 0.35 * (1 if side > 0 else 0.4)
        sd = (c.x - cx) * nx + (c.y - cy) * ny
        keep = (sd * side) > 0.3
        m = card_mask(c, cx, cy, 1.0, D2_SIZE) & keep
        burnt, edge, light = burn_away(c, g, m, np.abs(sd), p, 1311 + side)
        paint_card(c, g, cx, cy, 1.0, keep=keep & ~burnt, size=D2_SIZE)
        c.put(edge, K['violet4'])
        c.put(edge & (np.abs(sd) > 1.5), K['violet3'])
        g.add(edge, light, 0.9)


def deshacerse_frame(f):
    t = f / FRAMES
    c, g = Canvas(AW, AH), Canvas(AW, AH)
    bridge_scene(c, g, t)
    # the card: floats and turns, is cut, burns; a new one appears at the end of the loop
    bob = math.sin(t * math.pi * 4) * 1.0
    if f < D2_CUT:
        spin = 0.35 + 0.65 * abs(math.cos(t * math.pi * 2))
        paint_card(c, g, D2_CARD[0], D2_CARD[1] + bob, spin, glow=0.3, size=D2_SIZE)
    elif f <= D2_CUT + 5:
        card_halves(c, g, f, t)
    elif f >= 20:
        a = (f - 19) / 4.0
        spin = 0.35 + 0.65 * abs(math.cos(t * math.pi * 2))
        paint_card(c, g, D2_CARD[0], D2_CARD[1] + bob, spin, alpha=a, glow=0.3 * a, size=D2_SIZE)
    # embers spiral in and gather into a blue ward (4 de escudo)
    if 10 <= f <= 15:
        rng = np.random.default_rng(1313)
        k = ease_in_out((f - 9) / 6.0)
        for i in range(24):
            a0 = rng.uniform(0, 2 * math.pi)
            r0 = rng.uniform(10, 20)
            r = r0 * (1 - k)
            th = a0 + k * 3.0
            px, py = D2_CARD[0] + math.cos(th) * r, D2_CARD[1] + 4 + math.sin(th) * r * 0.8
            m = c.pixel(px, py) | (c.pixel(px + 1, py) if i % 3 == 0 else c.pixel(px, py))
            c.put(m, K['violet4'] if k < 0.5 else KB['b3'])
            g.add(m, L['violet'] if k < 0.5 else LB, 0.8)
    if 15 <= f <= 22:
        a = f - 15
        k = ease_out(min(1.0, a / 3.0))
        fade = 1.0 if a <= 4 else max(0.0, 1 - (a - 4) / 4.0)
        cx, cy = D2_CARD[0], D2_CARD[1] + 4
        for i, (r, w) in enumerate(((5 + 13 * k, 0.6), (3 + 9 * k, 0.5))):
            ring = diamond_ring(c, cx, cy, r, w)
            c.put(ring, KB['b2'] if i == 0 else KB['b1'], fade)
            c.put(ring & (((c.x + c.y + f * 2) % 7) < 3), KB['b3'], fade)
            g.add(ring, LB, (0.7 if i == 0 else 0.35) * fade)
        for i in range(4):                                     # four pips: 4 de escudo
            th = i * math.pi / 2 + t * math.pi * 4
            px, py = cx + math.cos(th) * (8 + 5 * k), cy + math.sin(th) * (6 + 4 * k)
            m = c.pixel(px, py) | c.pixel(px + 1, py)
            c.put(m, KB['b3'], fade)
            g.add(m, LB, 0.8 * fade)
    if f == 15:
        star4(c, g, D2_CARD[0], D2_CARD[1] + 4, 4, core=K['white'], col=KB['b3'], edge=KB['b2'], light=LB)
    # her dagger: rests low on the left, sweeps over the top through the card, comes back
    cpal = (K['violet0'], K['violet2'], K['violet3'], K['violet4'])
    if f in D2_HEADS:
        crescent(c, g, D2_ARC_C[0], D2_ARC_C[1], D2_ARC_R, D2_HEADS[f], 1.9, -1, 4.5, cpal, L['violet'], gain=1.3)
        arc_dagger(c, g, D2_ARC_C[0], D2_ARC_C[1], D2_ARC_R, D2_HEADS[f], D2_S, t, rune_gain=2.0)
    else:
        if 10 <= f <= 12:
            q = (f - 9) / 4.0
            crescent(c, g, D2_ARC_C[0], D2_ARC_C[1], D2_ARC_R, D2_HEADS[9], 1.9 * (1 - q * 0.6), -1, 4.5 * (1 - q),
                     cpal, L['violet'], a=1 - q, gain=1.0)
        if f <= 6 or f >= 17:
            gx_, gy_, ang = D2_HOME[0], D2_HOME[1] + (math.sin(t * math.pi * 4) * 0.8), D2_ANG
        elif f <= 6:
            gx_, gy_, ang = D2_HOME[0], D2_HOME[1], D2_ANG
        else:
            th = D2_HEADS[9]
            a0 = math.pi / 2 - th
            hx, hy = D2_ARC_C[0] + math.cos(th) * D2_ARC_R, D2_ARC_C[1] - math.sin(th) * D2_ARC_R
            sx, sy = hx - math.cos(a0) * 40 * D2_S, hy - math.sin(a0) * 40 * D2_S
            k = ease_in_out((f - 9) / 8.0)
            da = ((D2_ANG - a0 + math.pi) % (2 * math.pi)) - math.pi
            gx_ = sx + (D2_HOME[0] - sx) * k
            gy_ = sy + (D2_HOME[1] - sy) * k - math.sin(k * math.pi) * 6
            ang = a0 + da * k
        glint = -2 + (f - 1) * 10.0 if 1 <= f <= 5 else None
        dagger(c, g, round(gx_), round(gy_), ang, D2_S, t * 2, glint)
    if D2_CUT <= f <= D2_CUT + 2:
        star4(c, g, D2_CARD[0], D2_CARD[1] - 1, {0: 6, 1: 4, 2: 2}[f - D2_CUT], col=K['violet3'], edge=K['violet2'],
              light=L['violet'], gain=1.3)
    motes(c, g, t, 1315, 8, (2, 6, 86, 50), K['violet3'], KB['b2'], L['violet'], gain=0.45)
    vignette(c, 0.72)
    return c, g


# ---------------------------------------------------------------------------------------- Finta Doble
FD_REAL = (42.0, 49.0)        # guard of the real dagger (pointing up)
FD_REAL_ANG = -math.pi / 2 + 0.12
FD_S = 0.62
FD_SIGILS = ((20.0, 12.0), (68.0, 12.0))
FD_PICK = 1


def balcony_scene(c, g, t):
    gradient_bg(c, K['vio1'], K['vio2'], K['vio0'], 0.6)
    c.put(c.disc(44, 26, 30), K['vio3'], 0.22)
    c.put(c.disc(44, 26, 19), K['vio3'], 0.4)
    twinkling_stars(c, g, t, 1401, 14, (2, 2, 86, 40), avoid=(44, 26, 18), light=L['violet'])
    # stone balustrade
    bal = c.rect(0, 53, AW - 1, 54) | c.rect(0, 60, AW - 1, AH - 1)
    for x in range(1, AW, 6):
        bal |= c.disc(x + 1.5, 57, 1.8) | c.rect(x + 1, 55, x + 2, 59)
    c.put(bal, K['roof'])
    c.put(bal & ~shift(bal, 0, 1), K['vio3'])


def ghost_dagger_pose(side, f):
    """Guard position and angle of each violet ghost dagger (side -1 left, +1 right)."""
    rest = (44 + side * 26, 46.0)
    rest_ang = -math.pi / 2 - side * 0.35                      # tips leaning inwards
    cross_ang = -math.pi / 2 - side * 0.75                     # crossing in front of the real blade
    cross = (44 + side * 9, 46.0)
    if f <= 3 or f >= 20:
        return rest[0], rest[1] + math.sin((f / FRAMES + (0.25 if side > 0 else 0)) * math.pi * 4), rest_ang
    if f <= 7:
        k = ease_out((f - 3) / 4.0)
        return rest[0] + (cross[0] - rest[0]) * k, rest[1] - math.sin(k * math.pi) * 8, rest_ang + (cross_ang - rest_ang) * k
    if f <= 10:
        return cross[0] + side * (f - 7) * 2, cross[1], cross_ang
    k = ease_in_out((f - 10) / 10.0)
    sx, sy, sa = cross[0] + side * 6, cross[1], cross_ang
    return sx + (rest[0] - sx) * k, sy + (rest[1] - sy) * k, sa + (rest_ang - sa) * k


def finta_doble_frame(f):
    t = f / FRAMES
    c, g = Canvas(AW, AH), Canvas(AW, AH)
    balcony_scene(c, g, t)
    # two sigils high up: Combo picks one at random
    for i, (sx, sy) in enumerate(FD_SIGILS):
        lit = (f in (11, 13) and i == 0) or (f in (12, 14, 15, 16) and i == 1)
        broken = i == FD_PICK and 16 <= f <= 19
        if broken:
            continue
        a = 1.0 if lit else 0.5 if not (i == FD_PICK and 20 <= f <= 22) else 0.5 * (f - 19) / 3.0
        sigil(c, g, sx, sy, 5.5, 5.5, t, a, col=K['violet4'] if lit else K['violet2'], spin=2.0 if lit else 1.0)
    # the ghosts feint in from both sides and cross in front of the real dagger
    for side in (-1, 1):
        if side == 1 and 13 <= f <= 19:
            continue                                            # that one is off striking
        gx_, gy_, ga = ghost_dagger_pose(side, f)
        a = 0.85 if not (side == 1 and 20 <= f <= 22) else 0.85 * (f - 19) / 3.0
        if 4 <= f <= 7:
            px_, py_, pa_ = ghost_dagger_pose(side, f - 1)
            dagger(c, g, px_, py_, pa_, FD_S, t * 2, ghost=(K['violet2'], 0.35, L['violet']))
        dagger(c, g, gx_, gy_, ga, FD_S, t * 2, ghost=(K['violet3'], a, L['violet']))
    bob = math.sin(t * math.pi * 4) * 0.7
    glint = -2 + (f - 1) * 10.0 if 1 <= f <= 5 else (-2 + (f - 17) * 10.0 if 17 <= f <= 21 else None)
    dagger(c, g, FD_REAL[0], round(FD_REAL[1] + bob), FD_REAL_ANG, FD_S, t * 2, glint,
           rune_gain=1.8 if 7 <= f <= 9 else 1.0)
    # the clash raises a ward (3 de escudo)
    if 7 <= f <= 14:
        a = f - 7
        k = ease_out(min(1.0, a / 3.0))
        fade = 1.0 if a <= 3 else max(0.0, 1 - (a - 3) / 5.0)
        for i, (r, w) in enumerate(((5 + 12 * k, 0.6), (3 + 8 * k, 0.5))):
            ring = diamond_ring(c, 44, 36, r, w)
            c.put(ring, KB['b2'] if i == 0 else KB['b1'], fade)
            g.add(ring, LB, (0.7 if i == 0 else 0.35) * fade)
    if f == 7:
        star4(c, g, 44, 36, 5, core=K['white'], col=KB['b3'], edge=KB['b2'], light=LB)
    # Combo: the right ghost turns cyan and darts at the picked sigil
    tx, ty = FD_SIGILS[FD_PICK]
    if 13 <= f <= 15:
        k = (f - 12) / 3.0
        sx, sy, _ = ghost_dagger_pose(1, 12)
        stip = tip_of(sx, sy, ghost_dagger_pose(1, 12)[2], FD_S)
        x = stip[0] + (tx - stip[0]) * k
        y = stip[1] + (ty - stip[1]) * k
        ang = math.atan2(ty - stip[1], tx - stip[0])
        ux, uy = dir_of(ang)
        speed_lines(c, g, x - ux * 24, y - uy * 24, ux, uy, 0.9, 1403, count=4, spread=4, length=(5, 10))
        dagger(c, g, x - ux * 40 * FD_S, y - uy * 40 * FD_S, ang, FD_S, t * 2, ghost=(K['cyan3'], 1.0, L['cyan']))
    if 15 <= f <= 18:
        star4(c, g, tx, ty, {15: 6, 16: 4, 17: 3, 18: 1}[f], gain=1.3)
    sparks(c, g, f, 15, tx, ty, 1405, 10, (0.8, 2.2), 6, CYAN_SPARKS, L['cyan'], gravity=0.14)
    sparks(c, g, f, 15, tx, ty, 1407, 8, (0.6, 1.6), 5, VIOLET_SPARKS, L['violet'], gravity=0.05)
    motes(c, g, t, 1409, 7, (2, 6, 86, 52), K['violet3'], K['cyan3'], L['violet'], gain=0.45)
    vignette(c, 0.72)
    return c, g


# ---------------------------------------------------------------------------------------- Quiebro
Q_GUARD = (32.0, 50.0)
Q_ANG = -1.25
Q_TURN = -0.55
Q_S = 0.66
Q_HIT = 8


def arrow(c, g, tx, ty, ang, a=1.0, s=1.35):
    """An enemy arrow: (tx, ty) = the point of its head, flying along `ang`."""
    ux, uy = dir_of(ang)
    vx, vy = -uy, ux
    dx, dy = c.x - tx, c.y - ty
    u = -(dx * ux + dy * uy) / s                               # distance back from the point
    v = (dx * vx + dy * vy) / s
    head = (u >= 0) & (u <= 4) & (np.abs(v) <= u * 0.55 + 0.2)
    shaft = (u > 3) & (u <= 17) & (np.abs(v) <= 0.55)
    fletch = (u > 13) & (u <= 18) & (np.abs(v) <= 0.6 + (u - 13) * 0.45) & (np.abs(v) > 0.4)
    m = head | shaft | fletch
    c.put(outline_mask(m), K['ink'], a)
    c.put(shaft, KR['wood2'], a)
    c.put(fletch, K['violet2'], a)
    c.put(fletch & (v < 0), K['violet3'], a)
    c.put(head, K['steel2'], a)
    c.put(head & (v < 0), K['steel4'], a)
    return m


def wall_walk(c, g, t):
    gradient_bg(c, K['teal0'], K['teal2'], K['teal1'], 0.6)
    twinkling_stars(c, g, t, 1501, 14, (2, 2, 86, 18))
    # crenellated stone wall, moonlit from the upper left, arrow slits; walkway in front
    wall = c.rect(0, 24, AW - 1, 56)
    for x in range(0, AW, 10):
        wall |= c.rect(x, 18, x + 5, 24)
    lit = np.clip(1 - np.hypot((c.x - 18) / 60, (c.y - 20) / 40), 0, 1)
    c.put(wall, KR['stone1'])
    c.put(wall, KR['stone2'], np.clip(lit * 1.5, 0, 1))
    c.put(wall & ~shift(wall, 0, 1), KR['stone3'])
    for row, y in enumerate(range(28, 56, 5)):
        c.put(c.rect(0, y, AW - 1, y) & wall, KR['stone0'])
        off = 0 if row % 2 else 5
        for x in range(off, AW, 10):
            c.put(c.rect(x, y + 1, x, y + 4), KR['stone0'])
    for i, x in enumerate((12, 60)):
        slit = c.rect(x, 31, x + 1, 44) | c.rect(x - 2, 36, x + 3, 37)
        c.put(slit, K['ink'])
        lit_ = 0.5 + 0.5 * math.sin((t * (1 + i) + i * 0.4) * math.pi * 2)
        m = c.rect(x, 40, x + 1, 43)
        c.put(m, K['window2'], 0.8)
        g.add(m, L['warm'], 0.25 * lit_)
    c.put(c.rect(0, 56, AW - 1, AH - 1), K['roof'])
    c.put(c.rect(0, 56, AW - 1, 56), KR['stone3'])


def quiebro_frame(f):
    t = f / FRAMES
    c, g = Canvas(AW, AH), Canvas(AW, AH)
    wall_walk(c, g, t)
    # the dagger holds its guard, twists on the hit (the quiebro), settles back
    turn = {8: 0.6, 9: 1.0, 10: 1.0, 11: 0.85, 12: 0.6, 13: 0.4, 14: 0.25, 15: 0.12}.get(f, 0.0)
    ang = Q_ANG + (Q_TURN - Q_ANG) * turn
    bob = math.sin(t * math.pi * 4) * 0.7 if (f <= 5 or f >= 17) else 0.0
    gx_, gy_ = Q_GUARD[0] - turn * 2, Q_GUARD[1] + bob
    contact = tip_of(Q_GUARD[0], Q_GUARD[1], Q_ANG, Q_S, tip=30)
    # the arrow: comes in from the right, is turned away and tumbles off up and to the left
    if 3 <= f <= Q_HIT:
        k = (f - 2) / (Q_HIT - 2)
        ax, ay = 98 + (contact[0] + 2 - 98) * k, 22 + (contact[1] - 22) * k
        a_ang = math.atan2(contact[1] - 22, contact[0] + 2 - 98)
        if f < Q_HIT:
            ux, uy = dir_of(a_ang)
            speed_lines(c, g, ax - ux * 18, ay - uy * 18, ux, uy, 0.6, 1503, count=3, spread=3, length=(5, 9))
        arrow(c, g, ax, ay, a_ang)
    elif Q_HIT < f <= Q_HIT + 7:
        a = f - Q_HIT
        ax = contact[0] - a * 3.2
        ay = contact[1] - a * 3.6 + 0.25 * a * a
        arrow(c, g, ax, ay, math.radians(200) + a * 1.1, a=1.0 if a < 6 else 0.5)
    glint = -2 + (f - 18) * 10.0 if 18 <= f <= 22 else None
    dagger(c, g, round(gx_), round(gy_), ang, Q_S, t * 2, glint, rune_gain=1.8 if 6 <= f <= 10 else 1.0)
    if Q_HIT <= f <= Q_HIT + 2:
        star4(c, g, contact[0] + 1, contact[1], {0: 7, 1: 4, 2: 2}[f - Q_HIT], core=K['white'], col=KR['spark2'],
              edge=KR['spark1'], light=L['warm'], gain=1.3)
    falling_sparks(c, g, f, Q_HIT, contact[0], contact[1], 1505, 10, (1.0, 2.6), 7, WARM_SPARKS, L['warm'], floor=56,
                   spread=(-0.6, 2.2))
    # ward (6 de escudo): a blue diamond grows from the blade
    if Q_HIT <= f <= Q_HIT + 7:
        a = f - Q_HIT
        k = ease_out(min(1.0, a / 2.0))
        fade = 1.0 if a <= 3 else max(0.0, 1 - (a - 3) / 4.0)
        for i, (r, w) in enumerate(((6 + 12 * k, 0.6), (4 + 8 * k, 0.5))):
            ring = diamond_ring(c, contact[0], contact[1] + 2, r, w, squash=1.0)
            c.put(ring, KB['b2'] if i == 0 else KB['b1'], fade)
            g.add(ring, LB, (0.7 if i == 0 else 0.35) * fade)
    # Combo: a card flips up out of the dark (roba 1)
    if 13 <= f <= 20:
        k = ease_out(min(1.0, (f - 12) / 5.0))
        x, y = 66.0, 70 - 42 * k
        spin = math.cos((f - 13) * 0.8) if f < 18 else 1.0
        fade = 1.0 if f <= 18 else 1 - (f - 18) / 3.0
        paint_card(c, g, x, y, spin, alpha=fade, glow=0.4)
    if 18 <= f <= 20:
        star4(c, g, 66, 28, {18: 4, 19: 2, 20: 1}[f], col=K['violet3'], edge=K['violet2'], light=L['violet'])
    motes(c, g, t, 1507, 7, (2, 6, 86, 54), K['cyan3'], KB['b2'], L['cyan'], gain=0.45)
    vignette(c, 0.72)
    return c, g


# ---------------------------------------------------------------------------------------- Tirar y Cortar
TC_HOME = (14.0, 44.0)
TC_ANG = -0.45
TC_S = 0.64
TC_CARD_CUT = 7
TC_POST = (70.0, 34.0)
TC_HIT = 12


def training_post(c, g, x, y, notch=0.0, jolt=0):
    """A wooden training post on a stone base, two iron bands; `notch` 0..1 shows a fresh X cut."""
    x += jolt
    post = c.rect(int(x) - 5, int(y) - 22, int(x) + 5, 56)
    post &= ~(c.rect(int(x) - 5, int(y) - 22, int(x) - 4, int(y) - 21) | c.rect(int(x) + 4, int(y) - 22, int(x) + 5, int(y) - 21))
    c.put(outline_mask(post), K['ink'])
    c.put(post, KR['wood1'])
    c.put(post & (c.x < x - 2), KR['wood2'])
    c.put(post & (c.x > x + 3), KR['wood0'])
    c.put(post & ((np.floor(c.y / 4) + np.floor(c.x / 3)) % 5 == 0), KR['wood0'], 0.6)
    for by in (y - 16, y + 12):
        band = c.rect(int(x) - 6, int(by), int(x) + 6, int(by) + 1)
        c.put(band, KR['iron2'])
        c.put(band & (c.y < by + 1), KR['iron3'])
    base = c.rect(int(x) - 9, 54, int(x) + 9, 57)
    c.put(outline_mask(base) & (c.y < 57), K['ink'])
    c.put(base, KR['stone2'])
    c.put(base & (c.y < 55), KR['stone3'])
    if notch > 0:
        cut = c.line(x - 4, y - 4, x + 4, y + 4) | c.line(x + 4, y - 4, x - 4, y + 4)
        c.put(cut, KR['wood0'], notch)
        c.put(shift(cut, 0, 1) & post & ~cut, KR['leaf3'], notch * 0.8)


def tirar_y_cortar_frame(f):
    t = f / FRAMES
    c, g = Canvas(AW, AH), Canvas(AW, AH)
    gradient_bg(c, K['night0'], K['night2'], K['night1'], 0.58)
    twinkling_stars(c, g, t, 1601, 16, (2, 2, 86, 34))
    far = c.poly([(0, 46), (12, 46), (12, 40), (20, 40), (20, 44), (40, 44), (46, 38), (52, 44), (88, 44), (88, 64), (0, 64)])
    c.put(far, K['roof2'])
    c.put(far & ~shift(far, 0, 1), K['moon0'], 0.6)
    c.put(c.rect(0, 56, AW - 1, AH - 1), K['roof'])
    c.put(c.rect(0, 56, AW - 1, 56), K['night3'])
    notch = 1.0 if TC_HIT <= f <= 18 else max(0.0, 1 - (f - 18) / 4.0) if f > 18 else 0.0
    training_post(c, g, TC_POST[0], TC_POST[1], notch, jolt={12: 2, 13: 1, 14: -1}.get(f, 0))
    # 1) a card is tossed up (thrown away) ...
    cut_at = (40.0, 15.0)
    if 2 <= f < TC_CARD_CUT:
        k = (f - 1) / (TC_CARD_CUT - 1)
        x, y = bez((26.0, 66.0), (32.0, 6.0), cut_at, k)
        paint_card(c, g, x, y, math.cos(k * math.pi * 3), face=True)
    elif TC_CARD_CUT <= f <= TC_CARD_CUT + 5:              # ... and cut: two halves fall and burn
        a = f - TC_CARD_CUT
        for side in (-1, 1):
            cx, cy = cut_at[0] + side * (1 + a * 1.2), cut_at[1] + a * 1.4 + 0.2 * a * a
            sd = (c.x - cut_at[0] - side * (1 + a * 1.2)) * side
            keep = sd > 0.3
            m = card_mask(c, cx, cy, 1.0) & keep
            burnt, edge, light = burn_away(c, g, m, np.abs(sd), min(1.0, a / 5.0), 1603 + side,
                                           light=L2['spoil'])
            paint_card(c, g, cx, cy, 1.0, face=True, keep=keep & ~burnt)
            c.put(edge, K2['spoil3'])
            g.add(edge, L2['spoil'], 0.9)
    # 2) the dagger flicks up to cut the card, glows with Despojo, then lunges and splits the post
    if f <= 4 or f >= 22:
        gx_, gy_, ang = TC_HOME[0], TC_HOME[1] + math.sin(t * math.pi * 4) * 0.8, TC_ANG
    elif f <= 6:
        k = ease_out((f - 4) / 2.0)
        gx_, gy_, ang = TC_HOME[0] + 6 * k, TC_HOME[1] - 4 * k, TC_ANG - 0.5 * k
    elif f <= 10:
        gx_, gy_, ang = TC_HOME[0] + 6, TC_HOME[1] - 4, TC_ANG - 0.5 - (0.3 if f == 7 else 0.0)
    elif f <= 13:
        k = ease_out((f - 10) / 3.0)
        ex, ey = TC_POST[0] - 4 - math.cos(-0.15) * 40 * TC_S, TC_POST[1] - math.sin(-0.15) * 40 * TC_S
        gx_ = TC_HOME[0] + 6 + (ex - TC_HOME[0] - 6) * k
        gy_ = TC_HOME[1] - 4 + (ey - TC_HOME[1] + 4) * k
        ang = TC_ANG - 0.5 + (-0.15 - TC_ANG + 0.5) * k
    else:
        ex, ey = TC_POST[0] - 4 - math.cos(-0.15) * 40 * TC_S, TC_POST[1] - math.sin(-0.15) * 40 * TC_S
        k = ease_in_out((f - 13) / 9.0)
        gx_, gy_ = ex + (TC_HOME[0] - ex) * k, ey + (TC_HOME[1] - ey) * k - math.sin(k * math.pi) * 6
        ang = -0.15 + (TC_ANG + 0.15) * k
    spoil = 8 <= f <= 13
    if spoil:                                                   # orange glow of Despojo hugging the blade
        lab, u, v = dagger_labels(c, gx_, gy_, ang, s=TC_S)
        aura = dilate(dilate(lab > 0)) & ~(lab > 0)
        c.put(aura, K2['spoil1'], 0.7)
        c.put(aura & (np.abs(v) > 3.5), K2['spoil2'], 0.6)
        g.add(aura, L2['spoil'], 0.6)
        converge(c, g, f, *tip_of(gx_, gy_, ang, TC_S), 8, 3, 1605, 10, 14, (K2['spoil3'], K2['spoil2']), L2['spoil'])
    if TC_CARD_CUT - 1 <= f <= TC_CARD_CUT:
        cpal = (K['violet0'], K['violet2'], K['violet3'], K['violet4'])
        crescent(c, g, 30, 34, 20, math.radians(80 if f == TC_CARD_CUT else 120), 1.2, -1, 3.0, cpal, L['violet'], gain=1.1)
    if 11 <= f <= 13:
        ux, uy = dir_of(ang)
        speed_lines(c, g, *tip_of(gx_, gy_, ang, TC_S), ux, uy, 1.0 if f < 13 else 0.5, 1607, count=6, spread=6,
                    length=(8, 14), back=12)
    dagger(c, g, round(gx_), round(gy_), ang, TC_S, t * 2 + (1.5 if spoil else 0),
           -2 + (f - 1) * 10.0 if 1 <= f <= 4 else None, rune_gain=2.0 if spoil else 1.0)
    # the X of crescents on the post
    opal = (K2['spoil0'], K2['spoil2'], K2['spoil3'], K['white'])
    cpal = (K['cyan0'], K['cyan2'], K['cyan3'], K['cyan4'])
    if TC_HIT <= f <= TC_HIT + 4:
        q = (f - TC_HIT) / 5.0
        for sgn, pal, light in ((1, opal, L2['spoil']), (-1, cpal, L['cyan'])):
            # a straight slash across the post, thick in the middle, thinning to points
            ux, uy = 0.7071, sgn * 0.7071
            vx, vy = -uy, ux
            dx, dy = c.x - TC_POST[0], c.y - TC_POST[1]
            u = dx * ux + dy * uy
            v = dx * vx + dy * vy
            half = 13 * (1 - q * 0.3)
            w = np.clip(1 - (u / half) ** 2, 0, None) * 2.4 * (1 - q * 0.6)
            band = (np.abs(u) <= half) & (np.abs(v) <= w + 0.3)
            c.put(band, pal[1], 1 - q)
            c.put(band & (np.abs(v) <= w * 0.5), pal[2], 1 - q)
            c.put(band & (np.abs(v) <= w * 0.2) & (np.abs(u) < half * 0.6), pal[3], 1 - q)
            g.add(band, light, 1.3 * (1 - q))
    if TC_HIT <= f <= TC_HIT + 3:
        star4(c, g, TC_POST[0], TC_POST[1], {0: 8, 1: 6, 2: 4, 3: 2}[f - TC_HIT], gain=1.5)
    falling_sparks(c, g, f, TC_HIT, TC_POST[0], TC_POST[1], 1609, 10, (1.0, 2.6), 6, SPOIL_SPARKS, L2['spoil'],
                   floor=56, spread=(-1.0, 2.6))
    rng = np.random.default_rng(1611)
    for i in range(8):                                          # wood splinters
        a = f - TC_HIT
        if 0 < a <= 7:
            a0 = rng.uniform(-0.6, 3.6)
            sp = rng.uniform(0.8, 2.0)
            px = TC_POST[0] + math.cos(a0) * sp * a
            py = TC_POST[1] - math.sin(a0) * sp * a + 0.25 * a * a
            c.put(c.pixel(px, py) | c.pixel(px + 1, py), KR['wood2'] if i % 2 else KR['leaf3'], 1 - a / 8.0)
    if TC_HIT <= f <= TC_HIT + 1:
        shake(c, g, 1 if f == TC_HIT else -1, 0)
    motes(c, g, t, 1613, 6, (2, 6, 86, 52), K2['spoil2'], K['cyan3'], L2['spoil'], gain=0.4)
    vignette(c, 0.72)
    return c, g



# ---------------------------------------------------------------------------------------- Hoja Envenenada
HE_BOWL = (50.0, 46.0)        # centre of the poison's surface in the stone bowl
HE_ANG = 0.95                 # the blade dips in point first, down and to the right
HE_S = 0.62
HE_UP = (44.0, 33.0)          # where the point hangs above the bowl
HE_DOWN = (52.0, 50.0)        # where it goes under the surface


def apothecary(c, g, t):
    gradient_bg(c, K['night0'], K['vio1'], K['night1'], 0.6)
    # shelves with jars and bottles in silhouette, a few coloured glints
    rng = np.random.default_rng(1701)
    for y0 in (17, 33):
        shelf = c.rect(0, y0, AW - 1, y0 + 1)
        c.put(shelf, KR['wood1'])
        c.put(c.rect(0, y0, AW - 1, y0), KR['wood2'])
        x = 2.0
        while x < AW - 4:
            w = int(rng.integers(3, 7))
            h = int(rng.integers(5, 11))
            kind = int(rng.integers(0, 3))
            if kind == 0:
                m = c.rect(int(x), y0 - h, int(x) + w, y0 - 1)
            elif kind == 1:
                m = c.disc(x + w / 2, y0 - w / 2 - 0.5, w / 2 + 0.5) | c.rect(int(x + w / 2) - 1, y0 - w - 3, int(x + w / 2), y0 - w)
            else:
                m = c.rect(int(x), y0 - h, int(x) + w, y0 - 1) & ~c.rect(int(x), y0 - h, int(x), y0 - h)
            col = (K['violet1'], K['cyan0'], KR['crim1'], K2['pois1'])[int(rng.integers(0, 4))]
            c.put(outline_mask(m) & (c.y < y0), K['ink'])
            c.put(m, K['vio2'])
            c.put(m & (c.y > y0 - h * 0.55), col)                  # what is inside
            c.put(m & (c.x < x + 1.2), K['vio3'])
            gm = c.pixel(x + 1, y0 - h * 0.6)
            c.put(gm, K['moon1'])
            x += w + rng.uniform(2, 5)
    c.put(c.rect(0, 54, AW - 1, AH - 1), KR['wood1'])
    c.put(c.rect(0, 54, AW - 1, 54), KR['wood2'])
    c.put(c.rect(0, 58, AW - 1, 58), KR['wood0'])


def bowl_back(c, g, t, glow=0.0):
    """Stone bowl: rim and the poison's surface (bubbling), drawn before the blade."""
    cx, cy = HE_BOWL
    rim = (((c.x - cx) / 15) ** 2 + ((c.y - cy) / 4.2) ** 2) <= 1
    c.put(outline_mask(rim), K['ink'])
    c.put(rim, KR['stone2'])
    surf = (((c.x - cx) / 12.5) ** 2 + ((c.y - cy) / 3.0) ** 2) <= 1
    c.put(surf, K2['pois1'])
    c.put(surf & (c.y < cy - 0.5), K2['pois2'])
    c.put(surf & (c.y < cy - 1.5) & (c.x < cx), K2['pois3'])
    g.add(surf, L2['poison'], 0.25 + 0.35 * glow)
    rng = np.random.default_rng(1703)
    for i in range(6):                                          # bubbles swell and pop on the surface
        bx, by = cx + rng.uniform(-9, 9), cy + rng.uniform(-1.5, 1.5)
        ph = frac(t * (1 + i % 2) + rng.random())
        if ph < 0.6:
            r = 0.6 + ph * 1.5
            ring = ellipse_ring(c, bx, by, r, r * 0.7, 0.5) & surf
            c.put(ring, K2['pois4'])
            g.add(ring, L2['poison'], 0.4)


def bowl_front(c, g, t):
    cx, cy = HE_BOWL
    body = (((c.x - cx) / 15) ** 2 + ((c.y - cy) / 10) ** 2) <= 1
    body &= c.y > cy + 0.5
    body |= c.rect(int(cx) - 6, int(cy) + 9, int(cx) + 6, 55)
    c.put(outline_mask(body) & (c.y > cy + 1), K['ink'])
    c.put(body, KR['stone1'])
    c.put(body & (c.x < cx - 5), KR['stone2'])
    c.put(body & (c.x > cx + 9), KR['stone0'])
    lip = (np.abs((((c.x - cx) / 15) ** 2 + ((c.y - cy) / 4.2) ** 2) - 1) < 0.18) & (c.y > cy)
    c.put(lip, KR['stone3'])


def fumes(c, g, t, x0, x1, y0, seed, a=1.0):
    rng = np.random.default_rng(seed)
    for i in range(7):
        u = frac(t * (1 + i % 2) + rng.random())
        x = rng.uniform(x0, x1) + math.sin((u * 2 + i) * math.pi) * 2
        y = y0 - u * 24
        r = 1.2 + u * 3.0
        fa = math.sin(u * math.pi) * 0.55 * a
        m = c.disc(x, y, r)
        c.put(m, K2['pois1'], fa)
        c.put(m & c.disc(x - 0.6, y - 0.6, r * 0.6), K2['pois2'], fa * 0.8)
        g.add(m & c.disc(x, y, r * 0.5), L2['poison'], 0.15 * fa)


def coat(c, g, lab, u, amount, charges):
    """Green poison coating the blade (labels 1-4) and up to three glowing charges along the fuller."""
    blade = (lab >= 1) & (lab <= 5)
    c.put(blade, K2['pois2'], 0.7 * amount)
    c.put(blade & (lab == 1), K2['pois3'], amount)
    c.put(blade & (lab == 4), K2['pois1'], amount)
    g.add(blade, L2['poison'], 0.35 * amount)
    for i in range(charges):
        pip = (lab == 5) & (np.abs(u - (9 + i * 7)) <= 1.2)
        c.put(pip, K2['pois4'])
        g.add(pip, L2['poison'], 1.1)


def hoja_envenenada_frame(f):
    t = f / FRAMES
    c, g = Canvas(AW, AH), Canvas(AW, AH)
    apothecary(c, g, t)
    fumes(c, g, t, HE_BOWL[0] - 9, HE_BOWL[0] + 9, HE_BOWL[1] - 2, 1705)
    bowl_back(c, g, t, glow=1.0 if 6 <= f <= 9 else 0.0)
    # the point: hangs, dips under the surface, comes back up coated
    if f <= 4 or f >= 10:
        k = 0.0 if f <= 4 else (0.0 if f >= 12 else (12 - f) / 3.0)
        x, y = HE_UP[0] + (HE_DOWN[0] - HE_UP[0]) * k, HE_UP[1] + (HE_DOWN[1] - HE_UP[1]) * k
        y += math.sin(t * math.pi * 4) * 0.7 if f <= 4 or f >= 13 else 0
    else:
        k = ease_in_out((f - 4) / 4.0) if f <= 8 else 1.0
        x, y = HE_UP[0] + (HE_DOWN[0] - HE_UP[0]) * k, HE_UP[1] + (HE_DOWN[1] - HE_UP[1]) * k
    ux, uy = dir_of(HE_ANG)
    gx_, gy_ = x - ux * 40 * HE_S, y - uy * 40 * HE_S
    lab, u, v = dagger_labels(c, gx_, gy_, HE_ANG, s=HE_S)
    paint_dagger(c, g, lab, u, t * 2, -2 + (f - 20) * 11.0 if 20 <= f <= 23 else None, rune_gain=0.8)
    amount = 0.0 if f < 9 else 1.0 if f <= 19 else max(0.0, 1 - (f - 19) / 4.0)
    charges = 0 if f < 11 else min(3, 1 + (f - 11) // 2) if f <= 20 else 0
    if amount > 0:
        coat(c, g, lab, u, amount, charges)
    bowl_front(c, g, t)
    if 6 <= f <= 9:                                             # splash where the blade goes in
        rng = np.random.default_rng(1707)
        a = f - 6
        for i in range(10):
            a0 = rng.uniform(0.4, 2.7)
            sp = rng.uniform(0.8, 1.8)
            px = HE_DOWN[0] - 2 + math.cos(a0) * sp * a * 1.5
            py = HE_BOWL[1] - math.sin(a0) * sp * a * 1.5 + 0.3 * a * a
            m = c.pixel(px, py)
            c.put(m, K2['pois4'] if a < 2 else K2['pois3'])
            g.add(m, L2['poison'], 0.8 * (1 - a / 4))
    # three drops fall from the point back into the bowl (3 ataques envenenados)
    tip = tip_of(*(HE_UP[0] - ux * 40 * HE_S, HE_UP[1] - uy * 40 * HE_S), HE_ANG, HE_S)
    for born in (12, 15, 18):
        a = f - born
        if 0 <= a <= 3:
            py = tip[1] + 1 + a * a * 1.1 + a
            if py < HE_BOWL[1] - 1:
                m = c.pixel(tip[0], py) | c.pixel(tip[0], py + 1)
                c.put(m, K2['pois3'])
                g.add(m, L2['poison'], 0.9)
            else:
                ring = ellipse_ring(c, tip[0], HE_BOWL[1], 1 + a, 0.8 + a * 0.4, 0.5)
                c.put(ring, K2['pois4'], 0.9)
                g.add(ring, L2['poison'], 0.6)
        elif a == -1:
            m = c.pixel(tip[0], tip[1] + 1)
            c.put(m, K2['pois3'])
            g.add(m, L2['poison'], 0.7)
    motes(c, g, t, 1709, 7, (20, 10, 80, 44), K2['pois3'], K['violet3'], L2['poison'], gain=0.5)
    vignette(c, 0.72)
    return c, g


# ---------------------------------------------------------------------------------------- Veneno
V_TARGET = (62.0, 47.0)
V_FLASKS = ((2, 8, (60.0, 47.0)), (9, 15, (65.0, 46.0)))       # thrown, shatters, where (the second is Combo)


def flask(c, g, x, y, ang, a=1.0):
    """A small round poison flask with a cork, rotated by `ang` (its neck points along -ang's normal)."""
    ux, uy = math.cos(ang), math.sin(ang)
    dx, dy = c.x - x, c.y - y
    lx = dx * ux + dy * uy
    ly = -dx * uy + dy * ux
    body = np.hypot(lx, ly - 0.8) <= 3.6
    neck = (np.abs(lx) <= 1.1) & (ly < -2.2) & (ly >= -5.2)
    cork = (np.abs(lx) <= 1.4) & (ly < -5.2) & (ly >= -6.8)
    m = body | neck | cork
    c.put(outline_mask(m), K['ink'], a)
    c.put(body | neck, K2['glass1'], a)
    liquid = body & (ly > 0.0)
    c.put(liquid, K2['pois2'], a)
    c.put(liquid & (ly < 1.2), K2['pois3'], a)
    c.put(cork, KR['wood2'], a)
    c.put(body & (np.hypot(lx + 1.4, ly + 0.6) < 0.9), K2['glass2'], a)
    g.add(liquid, L2['poison'], 0.45 * a)
    return m


def swamp(c, g, t):
    gradient_bg(c, K['teal0'], K['teal2'], K['teal1'], 0.6)
    c.put(c.disc(20, 14, 18), K['teal3'], 0.25)
    moon = c.disc(20, 14, 5) & ~c.disc(22.5, 12.5, 4.5)
    c.put(moon, K['moon1'])
    twinkling_stars(c, g, t, 1801, 12, (30, 2, 86, 26))
    # dead trees and reeds in silhouette
    tree = c.poly([(4, 52), (6, 30), (3, 24), (7, 27), (9, 18), (10, 28), (14, 24), (11, 33), (11, 52)])
    tree |= c.poly([(80, 52), (81, 34), (77, 28), (82, 31), (84, 22), (85, 34), (86, 52)])
    c.put(tree, K['roof'])
    water = c.rect(0, 50, AW - 1, AH - 1)
    c.put(water, hexc('#0a1a1c'))
    for i, y in enumerate((52, 55, 59, 62)):
        off = (t * 88 * (1 if i % 2 else -1) + i * 23) % 22
        for x in np.arange(-22 + off, AW, 22):
            c.put(c.rect(int(x), y, int(x) + 6 + i, y), hexc('#17393a'))
    mound = (((c.x - V_TARGET[0]) / 16) ** 2 + ((c.y - 52) / 5) ** 2) <= 1
    c.put(outline_mask(mound) & (c.y < 52), K['ink'])
    c.put(mound, hexc('#16241c'))
    c.put(mound & (c.y < 49), hexc('#22382a'))
    for rx in (20, 26, 31, 46, 75):                              # reeds
        c.put(c.line(rx, 51, rx + 1, 42 + rx % 5), hexc('#1d2e24'))


def veneno_frame(f):
    t = f / FRAMES
    c, g = Canvas(AW, AH), Canvas(AW, AH)
    swamp(c, g, t)
    # a sigil on the mound: the poisoned enemy
    hot = 1.0 if 7 <= f <= 19 else 0.0
    sigil(c, g, V_TARGET[0], 50, 10, 2.6, t, 0.45 + 0.5 * hot, col=K2['pois3'] if hot else K['violet2'],
          light=L2['poison'] if hot else L['violet'])
    puddle_r = 0.0
    for i, (born, land, (lx, ly)) in enumerate(V_FLASKS):
        if born <= f < land:
            k = (f - born + 1) / (land - born + 1)
            x, y = bez((-4.0, 40.0 - i * 4), (30.0, -6.0 - i * 4), (lx, ly), k)
            flask(c, g, x, y, (f - born) * 1.3 + i)
            m = c.pixel(x - 4, y + 1)
            c.put(m, K2['pois3'], 0.7)
        a = f - land
        if 0 <= a <= 10:
            puddle_r = max(puddle_r, min(1.0, (a + 1) / 3.0) * (9 + 3 * i) * (1 if a < 4 else max(0.0, 1 - (a - 3) / 5.0)))
    if puddle_r > 0.5:
        pud = (((c.x - V_TARGET[0]) / puddle_r) ** 2 + ((c.y - 49) / (puddle_r * 0.3)) ** 2) <= 1
        c.put(pud, K2['pois1'])
        c.put(pud & (c.y < 48.6), K2['pois2'])
        g.add(pud, L2['poison'], 0.35)
        rng = np.random.default_rng(1803)
        for i in range(5):
            bx = V_TARGET[0] + rng.uniform(-0.7, 0.7) * puddle_r
            ph = frac(t * 2 + rng.random())
            if ph < 0.5:
                ring = ellipse_ring(c, bx, 48.5, 0.6 + ph * 2, 0.5 + ph, 0.5)
                c.put(ring, K2['pois4'])
    for i, (born, land, (lx, ly)) in enumerate(V_FLASKS):
        a = f - land
        if a == 0:
            star4(c, g, lx, ly, 6, core=K['white'], col=K2['pois4'], edge=K2['pois3'], light=L2['poison'], gain=1.3)
        sparks(c, g, f, land, lx, ly, 1805 + i, 12, (1.0, 2.6), 6, POISON_SPARKS, L2['poison'], gravity=0.22,
               spread=(0.2, 2.9))
        sparks(c, g, f, land, lx, ly, 1807 + i, 6, (1.0, 2.0), 4, (K['white'], K2['glass2'], K2['glass1']), L['white'],
               gravity=0.25, spread=(0.3, 2.8), gain=0.4)
        # the toxic cloud: puffs that swell, rise and thin out
        life = 9 - i                                            # the Combo cloud is gone before the loop wraps
        if 0 <= a < life:
            rng = np.random.default_rng(1809 + i)
            for j in range(6 + 2 * i):
                ox, oy = rng.uniform(-10, 10), rng.uniform(-3, 2)
                k = a / float(life)
                r = (2.5 + 4.5 * ease_out(min(1.0, a / 4.0))) * rng.uniform(0.7, 1.1) * (1 + 0.3 * i)
                px, py = lx + ox * (0.4 + k), ly - 2 + oy - a * 1.4 * rng.uniform(0.6, 1.2)
                fa = (1 - k) * 0.85
                m = c.disc(px, py, r)
                c.put(m, K2['pois1'], fa)
                c.put(m & c.disc(px - r * 0.3, py - r * 0.3, r * 0.65), K2['pois2'], fa)
                c.put(m & c.disc(px - r * 0.45, py - r * 0.45, r * 0.25), K2['pois3'], fa)
                g.add(m & c.disc(px, py, r * 0.6), L2['poison'], 0.25 * fa)
    motes(c, g, t, 1811, 8, (40, 14, 86, 50), K2['pois3'], K2['pois4'], L2['poison'], gain=0.5)
    vignette(c, 0.72)
    return c, g


# ---------------------------------------------------------------------------------------- Muro de Humo
MH_BOMB = (44.0, 50.0)
MH_POP = 5
MH_PUFFS = ((14, 44, 7), (24, 38, 8), (34, 42, 9), (44, 35, 9), (54, 41, 9), (64, 37, 8), (74, 43, 7),
            (20, 50, 6), (44, 47, 8), (68, 50, 6), (32, 30, 6), (56, 29, 6))


def timber_alley(c, g, t):
    gradient_bg(c, K['night0'], K['night2'], K['night1'], 0.6)
    twinkling_stars(c, g, t, 1901, 10, (2, 2, 86, 14))
    # timber-framed house fronts
    house = c.rect(0, 12, AW - 1, 52)
    c.put(house, hexc('#1c1426'))
    beams = np.zeros((AH, AW), bool)
    for x in range(0, AW, 16):
        beams |= c.rect(x, 12, x + 1, 52)
    beams |= c.rect(0, 12, AW - 1, 13) | c.rect(0, 31, AW - 1, 32)
    for x in range(0, AW, 16):
        beams |= c.line(x + 1, 31, x + 15, 14) & (c.y > 13)
    c.put(beams, KR['wood0'])
    c.put(beams & ~shift(beams, 0, 1), KR['wood1'])
    for i, x in enumerate((6, 38, 70)):
        lit = 0.6 + 0.4 * math.sin((t * (1 + i % 2) + i * 0.3) * math.pi * 2)
        w = c.rect(x, 18, x + 5, 25)
        c.put(outline_mask(w), K['ink'])
        c.put(w, K['window'] if i != 1 else K['window2'])
        c.put(c.rect(x + 2, 18, x + 3, 25) | c.rect(x, 21, x + 5, 22), KR['wood0'])
        g.add(w, L['warm'], 0.3 * lit)
    cobbles(c, 53)


def smoke_puff(c, g, x, y, r, a):
    m = c.disc(x, y, r)
    c.put(m, K2['smoke1'], a)
    c.put(m & c.disc(x - r * 0.25, y - r * 0.25, r * 0.8), K2['smoke2'], a)
    c.put(m & c.disc(x - r * 0.4, y - r * 0.45, r * 0.45), K2['smoke3'], a)
    c.put(m & c.disc(x - r * 0.5, y - r * 0.55, r * 0.18), K2['smoke4'], a)
    return m


def bomb(c, g, x, y, f, roll=0.0):
    body = c.disc(x, y, 4.2)
    c.put(outline_mask(body), K['ink'])
    c.put(body, K2['smoke0'])
    c.put(body & c.disc(x - 1.2 + math.cos(roll) * 0.8, y - 1.4 + math.sin(roll) * 0.4, 1.6), K2['smoke3'])
    c.put(c.pixel(x - 1.5 + math.cos(roll), y - 2), K2['smoke4'])
    cap = c.rect(int(x) - 1, int(y) - 6, int(x) + 1, int(y) - 5)
    c.put(cap, KR['iron2'])
    fuse = c.line(x, y - 6, x + 2, y - 9)
    c.put(fuse, KR['paper1'])
    sx, sy = x + 2, y - 9.5
    star4(c, g, sx, sy, 1 + (f % 2), core=K['white'], col=KR['spark2'], edge=KR['spark1'], light=L['warm'], gain=0.9)
    sparks(c, g, f, f - 1, sx, sy, 1903 + f, 3, (0.5, 1.2), 3, WARM_SPARKS, L['warm'], gravity=0.15)


def muro_de_humo_frame(f):
    t = f / FRAMES
    c, g = Canvas(AW, AH), Canvas(AW, AH)
    timber_alley(c, g, t)
    if f < MH_POP:
        bomb(c, g, MH_BOMB[0], MH_BOMB[1] - (1 if f % 2 else 0), f)
    elif f >= 18:
        k = ease_out((f - 17) / 6.0)
        x = -6 + (MH_BOMB[0] + 6) * k
        bomb(c, g, x, MH_BOMB[1], f, roll=-x / 4.2)
    # arrows fly at the wall of smoke and are stopped in it (14 de escudo)
    for i, (born, y) in enumerate(((9, 34), (12, 42))):
        a = f - born
        if 0 <= a <= 2:
            x = 98 - a * 14
            arrow(c, g, x, y, math.pi)
        elif a == 3:
            star4(c, g, 66, y, 3, core=K['white'], col=KB['b3'], edge=KB['b2'], light=LB, gain=1.0)
        sparks(c, g, f, born + 3, 66, y, 1905 + i, 7, (0.6, 1.6), 4, (KB['b3'], KB['b2'], KB['b1']), LB, gravity=0.05)
    # the smoke wall
    if f >= MH_POP:
        a = f - MH_POP
        rng = np.random.default_rng(1907)
        grow = ease_out(min(1.0, (a + 1) / 5.0))
        fade = 1.0 if f <= 16 else max(0.0, 1 - (f - 16) / 6.0)
        wall = np.zeros((AH, AW), bool)
        for j, (px, py, r) in enumerate(MH_PUFFS):
            ph = rng.random()
            spread = 0.25 + 0.75 * grow
            x = MH_BOMB[0] + (px - MH_BOMB[0]) * spread + math.sin((t * 2 + ph) * math.pi * 2) * 1.0
            y = MH_BOMB[1] + (py - MH_BOMB[1]) * spread - (0 if f <= 16 else (f - 16) * 1.2)
            rr = r * (0.3 + 0.7 * grow) * (1 + (0 if f <= 16 else (f - 16) * 0.05))
            wall |= smoke_puff(c, g, x, y, rr, fade)
        if 8 <= f <= 16:                                        # the ward glinting inside the smoke
            for j in range(8):
                gx_, gy_ = 16 + j * 8 + (j % 2) * 3, 36 + ((j * 5) % 3) * 4
                if (f + j) % 4 < 2 and wall[int(gy_), int(gx_)]:
                    dm = (np.abs(c.x - gx_) + np.abs(c.y - gy_)) <= 1.5
                    c.put(dm, KB['b3'] if (f + j) % 4 == 0 else KB['b2'])
                    g.add(dm, LB, 0.6)
    if f == MH_POP:
        flash = c.disc(*MH_BOMB, 7)
        c.put(flash, K['violet4'])
        c.put(c.disc(*MH_BOMB, 4), K['white'])
        g.add(flash, L['white'], 1.2)
    if MH_POP <= f <= MH_POP + 3:
        a = f - MH_POP
        ring = ellipse_ring(c, MH_BOMB[0], MH_BOMB[1], 6 + a * 7, 2 + a * 2.2, 0.6)
        c.put(ring, K2['smoke4'], 1 - a / 4)
    motes(c, g, t, 1909, 6, (2, 6, 86, 50), K2['smoke4'], KB['b2'], L['white'], gain=0.3)
    vignette(c, 0.72)
    return c, g


# ---------------------------------------------------------------------------------------- Contraataque
CA_GUARD = (24.0, 50.0)
CA_ANG = -0.95
CA_S = 0.66
CA_HANDS = (64.0, -50.0)
CA_REACH = 82.0
CA_ANGLES = {4: 72, 5: 70, 6: 66, 7: 86, 8: 106, 9: 100, 10: 93, 11: 86, 12: 80, 13: 76, 14: 74}
CA_REST = 74
CA_HIT = 8


def arena(c, g, t):
    gradient_bg(c, K['night0'], K['vio1'], K['night1'], 0.6)
    wall = c.rect(0, 0, AW - 1, 52)
    c.put(wall, KR['stone1'])
    for row, y in enumerate(range(3, 52, 6)):
        c.put(c.rect(0, y, AW - 1, y), KR['stone0'])
        off = 0 if row % 2 else 7
        for x in range(off, AW, 14):
            c.put(c.rect(x, y + 1, x, y + 5), KR['stone0'])
    for i, tx in enumerate((10, 78)):                           # torches: warm pools in three steps
        fl = 0.85 + 0.15 * math.sin((t * (3 + i) + i * 0.5) * math.pi * 2)
        for r, col in ((16 * fl, hexc('#1f1a2c')), (10 * fl, hexc('#2c2030')), (5 * fl, hexc('#3d2a2c'))):
            c.put(c.disc(tx, 14, r) & (c.y < 52), col)
        c.put(c.rect(tx - 1, 15, tx, 21), KR['wood1'])
        c.put(c.rect(tx - 2, 14, tx + 1, 15), KR['iron2'])
        sway = round(math.sin((t * 3 + i) * math.pi * 2) * 0.8)
        fl_m = c.poly([(tx + sway, 7), (tx + 2.5, 12), (tx + 1.5, 14), (tx - 2.5, 14), (tx - 3, 12)])
        c.put(fl_m, KR['spark1'])
        c.put(c.poly([(tx + sway * 0.5, 10), (tx + 1.2, 13), (tx - 1.5, 13.5)]), KR['spark2'])
        g.add(fl_m, L['warm'], 0.8 * fl)
    c.put(c.rect(0, 52, AW - 1, AH - 1), KR['stone0'])
    c.put(c.rect(0, 52, AW - 1, 52), KR['stone3'])
    c.put(c.rect(0, 53, AW - 1, 53), KR['stone2'])
    for x in range(5, AW, 13):
        c.put(c.rect(x, 54, x, 63), K['ink'])


def contraataque_frame(f):
    t = f / FRAMES
    c, g = Canvas(AW, AH), Canvas(AW, AH)
    arena(c, g, t)
    # the attacker's axe (off the picture, up and to the right): wind-up, swing, blocked, knocked back
    th = math.radians(CA_ANGLES.get(f, CA_REST))
    hx, hy = CA_HANDS[0] + math.cos(th) * CA_REACH, CA_HANDS[1] + math.sin(th) * CA_REACH
    if f in (7, 8):
        pth = math.radians(CA_ANGLES[f - 1])
        phx, phy = CA_HANDS[0] + math.cos(pth) * CA_REACH, CA_HANDS[1] + math.sin(pth) * CA_REACH
        paint_axe(c, g, axe_labels(c, (hx + phx) / 2, (hy + phy) / 2, (th + pth) / 2, s=1.3), a=0.35)
    paint_axe(c, g, axe_labels(c, hx, hy, th, s=1.3), curse=1.4 if 6 <= f <= 8 else 1.0)
    # her dagger: holds its guard, takes the blow, then ripostes up the haft at the attacker
    push = {8: -2, 9: -1}.get(f, 0)
    riposte = {10: 8, 11: 15, 12: 15, 13: 11, 14: 7, 15: 4, 16: 2}.get(f, 0)
    ang = CA_ANG + (0.12 if riposte > 0 else 0)
    ux, uy = dir_of(ang)
    gx_ = CA_GUARD[0] + push + ux * riposte
    gy_ = CA_GUARD[1] + uy * riposte + (math.sin(t * math.pi * 4) * 0.7 if f <= 5 or f >= 18 else 0)
    if 10 <= f <= 11:
        speed_lines(c, g, *tip_of(gx_, gy_, ang, CA_S), ux, uy, 0.9, 2001, count=5, spread=5, length=(7, 12), back=10)
    th12 = math.radians(CA_ANGLES[12])
    head12 = (CA_HANDS[0] + math.cos(th12) * CA_REACH, CA_HANDS[1] + math.sin(th12) * CA_REACH)
    hit2 = (head12[0] - math.cos(th12) * 20, head12[1] - math.sin(th12) * 20)   # up the haft, at the attacker's grip
    if 11 <= f <= 12:                                           # the riposte flies up the haft as a crescent
        start = tip_of(CA_GUARD[0] + ux * 15, CA_GUARD[1] + uy * 15, ang, CA_S)
        k = (f - 10) / 2.0
        wx, wy = start[0] + (hit2[0] - start[0]) * k, start[1] + (hit2[1] - start[1]) * k
        head = math.atan2(-(hit2[1] - start[1]), hit2[0] - start[0])
        cpal = (K['cyan0'], K['cyan2'], K['cyan3'], K['cyan4'])
        crescent(c, g, wx - math.cos(head) * 7, wy + math.sin(head) * 7, 7, head + 0.8, 1.6, -1, 2.5, cpal, L['cyan'],
                 gain=1.3)
    dagger(c, g, round(gx_), round(gy_), ang, CA_S, t * 2 + (1.5 if 7 <= f <= 12 else 0),
           -2 + (f - 1) * 10.0 if 1 <= f <= 4 else None, rune_gain=1.9 if 7 <= f <= 12 else 1.0)
    contact = tip_of(CA_GUARD[0], CA_GUARD[1], CA_ANG, CA_S, tip=33)
    if CA_HIT <= f <= CA_HIT + 2:
        star4(c, g, contact[0], contact[1], {0: 7, 1: 4, 2: 2}[f - CA_HIT], core=K['white'], col=KR['spark2'],
              edge=KR['spark1'], light=L['warm'], gain=1.3)
    falling_sparks(c, g, f, CA_HIT, contact[0], contact[1], 2003, 10, (1.0, 2.6), 7, WARM_SPARKS, L['warm'], floor=52,
                   spread=(-0.8, 2.4))
    if CA_HIT <= f <= CA_HIT + 6:                               # fully blocked: the blue ward (4 de escudo)
        a = f - CA_HIT
        k = ease_out(min(1.0, a / 2.0))
        fade = 1.0 if a <= 2 else max(0.0, 1 - (a - 2) / 4.0)
        for i, (r, w) in enumerate(((5 + 10 * k, 0.6), (3 + 7 * k, 0.5))):
            ring = diamond_ring(c, contact[0], contact[1] + 1, r, w, squash=1.0)
            c.put(ring, KB['b2'] if i == 0 else KB['b1'], fade)
            g.add(ring, LB, (0.7 if i == 0 else 0.35) * fade)
    # the riposte lands: 3 de daño back at the attacker
    if 12 <= f <= 15:
        star4(c, g, hit2[0], hit2[1], {12: 7, 13: 5, 14: 3, 15: 1}[f], core=K['white'], col=KR['crim5'],
              edge=KR['crim4'], light=LR['crimson'], gain=1.4)
    sparks(c, g, f, 12, hit2[0], hit2[1], 2005, 12, (0.8, 2.4), 6, (KR['crim5'], KR['crim4'], KR['crim2']), LR['crimson'],
           gravity=0.12)
    sparks(c, g, f, 12, hit2[0], hit2[1], 2007, 8, (0.8, 2.0), 5, CYAN_SPARKS, L['cyan'], gravity=0.1)
    if CA_HIT <= f <= CA_HIT + 1:
        shake(c, g, -1 if f == CA_HIT else 1, 0)
    motes(c, g, t, 2009, 6, (2, 6, 86, 50), K['cyan3'], KR['crim4'], L['cyan'], gain=0.4)
    vignette(c, 0.72)
    return c, g


# ---------------------------------------------------------------------------------------- Golpe de Gracia
GG_HELM = (46.0, 44.0)        # centre of the helm lying on the ground
GG_S = 0.6
GG_HIT = 9
GG_TOP = 35.0                 # top of the helm, where the point goes in


def helm_mask(c, x, y):
    dx, dy = c.x - x, c.y - y
    body = (np.abs(dx) <= 9) & (dy >= -6) & (dy <= 8)
    dome = (((dx) / 9) ** 2 + ((dy + 6) / 4) ** 2 <= 1) & (dy < -6)
    return body | dome


def paint_helm(c, g, x, y, t, alpha=1.0, eyes=1.0, cracks=0.0, keep=None):
    m = helm_mask(c, x, y)
    if keep is not None:
        m &= keep
    dx, dy = c.x - x, c.y - y
    c.put(outline_mask(m), K['ink'], alpha)
    c.put(m, KR['iron2'], alpha)
    c.put(m & (dx < -4), KR['iron3'], alpha)
    c.put(m & (dx > 5), KR['iron1'], alpha)
    c.put(m & (np.abs(dx) < 0.6) & (dy < 1), KR['iron3'], alpha)               # the ridge
    c.put(m & (np.abs(dy - 3) < 0.6), KR['iron1'], alpha)                      # band
    slit = m & (np.abs(dy + 1) <= 0.6) & (np.abs(dx) <= 7) & (np.abs(dx) > 0.7)
    c.put(slit, K['ink'], alpha)
    for hx_ in (-5, -3, 3, 5):
        c.put(m & (np.abs(dx - hx_) < 0.6) & (np.abs(dy - 5.5) < 0.6), K['ink'], alpha)
    c.put(m & c.disc(x + 5, y - 6, 1.6), KR['iron1'], alpha)                  # a dent
    if eyes > 0:
        fl = 0.7 + 0.3 * math.sin(t * math.pi * 2 * 3)
        for ex in (-3.5, 3.5):
            e = m & (np.abs(dx - ex) <= 1.2) & (np.abs(dy + 1) <= 0.6)
            c.put(e, KR['crim5'], alpha * eyes)
            g.add(e, LR['crimson'], 1.0 * fl * eyes * alpha)
    if cracks > 0:
        n = int(round(cracks * 6))
        pts = ((0, -10), (1, -6), (-1, -3), (1, 1), (0, 4), (1, 8), (0, 9))
        for (x0, y0), (x1, y1) in list(zip(pts, pts[1:]))[:n]:
            ln = c.line(x + x0, y + y0, x + x1, y + y1) & m
            c.put(ln, K['ink'], alpha)
            c.put(shift(ln, 1, 0) & m & ~ln, KR['crim3'], alpha * 0.8)
            g.add(ln, LR['crimson'], 0.4 * alpha)
    return m


def battlefield(c, g, t):
    gradient_bg(c, K['night0'], K['night2'], K['night1'], 0.6)
    c.put(c.disc(64, 18, 26), KR['crim0'], 0.3)
    c.put(c.disc(64, 18, 16), KR['crim0'], 0.55)
    moon = c.disc(64, 18, 6) & ~c.disc(66.5, 16.5, 5.2)
    c.put(moon, K['moon1'])
    c.put(moon & ~c.disc(64.5, 18.5, 5.4), K['moon2'])
    twinkling_stars(c, g, t, 2101, 12, (2, 2, 86, 30), avoid=(64, 18, 16))
    ground = c.poly([(0, 50), (20, 48), (50, 49), (88, 47), (88, 64), (0, 64)])
    c.put(ground, K['roof2'])
    c.put(ground & ~shift(ground, 0, 1), K['vio3'], 0.7)
    # spears and a torn banner stuck in the ground, far away
    for x, h, lean in ((10, 20, 2), (16, 16, -1), (78, 22, -2), (84, 15, 1)):
        c.put(c.line(x, 49, x + lean, 49 - h), K['roof'])
    flag = c.poly([(78 - 2, 27), (70, 28), (72, 31), (69, 34), (77, 33)])
    c.put(flag, KR['crim1'])


def golpe_de_gracia_frame(f):
    t = f / FRAMES
    c, g = Canvas(AW, AH), Canvas(AW, AH)
    battlefield(c, g, t)
    hx, hy = GG_HELM
    # the helm: still alive (its eyes burn), split by the blow, the halves fall away; another one is there again
    if f < GG_HIT or f >= 21:
        a = 1.0 if f < GG_HIT else (f - 20) / 3.0
        paint_helm(c, g, hx, hy, t, alpha=a, eyes=a)
    elif f <= GG_HIT + 1:
        paint_helm(c, g, hx, hy, t, cracks=(f - GG_HIT + 1) / 2.0, eyes=1.0 if f == GG_HIT else 0.5)
    else:
        a = f - GG_HIT - 1
        fade = 1.0 if a <= 4 else max(0.0, 1 - (a - 4) / 4.0)
        if fade > 0:
            for side in (-1, 1):
                ox = side * (1 + a * 1.1)
                oy = 0.15 * a * a
                keep = (c.x - (hx + ox)) * side > 0
                paint_helm(c, g, hx + ox, hy + oy, t, alpha=fade, eyes=0.0, keep=keep)
    # the soul leaves as a red wisp
    if GG_HIT + 2 <= f <= GG_HIT + 9:
        a = f - GG_HIT - 2
        rng = np.random.default_rng(2103)
        for j in range(8):
            px = hx + rng.uniform(-3, 3) + math.sin((a + j) * 0.9) * 2
            py = hy - 2 - a * 2.4 * rng.uniform(0.7, 1.2)
            r = 1.5 + a * 0.4
            fa = (1 - a / 8.0) * 0.8
            m = c.disc(px, py, r)
            c.put(m, KR['crim2'], fa)
            c.put(m & c.disc(px - 0.5, py - 0.5, r * 0.5), KR['crim4'], fa)
            g.add(m, LR['crimson'], 0.3 * fa)
    # her dagger: point down and to the left above the helm, draws back, plunges into the top, pulls out
    ang = math.pi / 2 + 0.7
    ux, uy = dir_of(ang)
    rest_tip = (56.0, 27.0)
    into = math.hypot(rest_tip[0] - (hx - 1), rest_tip[1] - (GG_TOP + 5))
    along = {6: -3.0, 7: -5.0, 8: -5.0}.get(f, 0.0)
    if GG_HIT <= f <= GG_HIT + 1:
        along = into
    elif GG_HIT + 2 <= f <= GG_HIT + 10:
        along = into * 0.85 * (1 - ease_in_out((f - GG_HIT - 2) / 8.0))
    bob = math.sin(t * math.pi * 4) * 0.7 if (f <= 5 or f >= 20) else 0
    tx, ty = rest_tip[0] + ux * along, rest_tip[1] + uy * along + bob
    gx_, gy_ = tx - ux * 40 * GG_S, ty - uy * 40 * GG_S
    lab, u, v = dagger_labels(c, gx_, gy_, ang, s=GG_S)
    if GG_HIT <= f <= GG_HIT + 1:
        lab[helm_mask(c, hx, hy) & (c.y > GG_TOP + 1)] = 0          # the point is inside the helm
        speed_lines(c, g, gx_, gy_, ux, uy, 0.7, 2105, count=4, spread=4, length=(6, 10))
    paint_dagger(c, g, lab, u, t * 2 + (1.5 if 6 <= f <= 10 else 0), -2 + (f - 1) * 10.0 if 1 <= f <= 4 else None,
                 rune_gain=1.9 if 6 <= f <= 10 else 1.0)
    if 7 <= f <= 8:
        converge(c, g, f, *tip_of(gx_, gy_, ang, GG_S), 7, 2, 2107, 10, 12, (K['cyan4'], K['cyan2']), L['cyan'])
    if GG_HIT <= f <= GG_HIT + 3:
        star4(c, g, hx, GG_TOP, {0: 8, 1: 6, 2: 4, 3: 2}[f - GG_HIT], gain=1.5)
    falling_sparks(c, g, f, GG_HIT, hx, GG_TOP, 2109, 12, (1.0, 2.6), 7, WARM_SPARKS, L['warm'], floor=50,
                   spread=(0.2, 2.9))
    # it was the killing blow: +1 de maná
    if 13 <= f <= 19:
        k = ease_out((f - 13) / 6.0)
        mana_drop(c, g, round(hx + 8 + math.sin(k * math.pi * 2)), round(hy - 6 - 22 * k), 0.8 + 0.5 * min(1.0, (f - 12) / 2.0),
                  gain=1.0 + 0.5 * (f == 19))
    if f == 19:
        star4(c, g, hx + 8, hy - 28, 3, col=KR['mana3'], edge=KR['mana2'], light=LR['mana'])
    sparks(c, g, f, 19, hx + 8, hy - 28, 2111, 9, (0.5, 1.5), 5, (KR['mana3'], KR['mana2'], KR['mana1']), LR['mana'],
           gravity=-0.02)
    if GG_HIT <= f <= GG_HIT + 1:
        shake(c, g, 0, 1 if f == GG_HIT else -1)
    motes(c, g, t, 2113, 6, (2, 6, 86, 48), KR['crim4'], K['cyan3'], LR['crimson'], gain=0.4)
    vignette(c, 0.72)
    return c, g

ILLUSTRATIONS = {
    'chatarra': chatarra_frame,
    'deshacerse': deshacerse_frame,
    'finta_doble': finta_doble_frame,
    'quiebro': quiebro_frame,
    'tirar_y_cortar': tirar_y_cortar_frame,
    'hoja_envenenada': hoja_envenenada_frame,
    'veneno': veneno_frame,
    'muro_de_humo': muro_de_humo_frame,
    'contraataque': contraataque_frame,
    'golpe_de_gracia': golpe_de_gracia_frame,
}


def render(card_id):
    fn = ILLUSTRATIONS[card_id]
    base, glow = [], []
    for f in range(FRAMES):
        b, gl = fn(f)
        base.append(b)
        glow.append(gl)
    return base, glow
