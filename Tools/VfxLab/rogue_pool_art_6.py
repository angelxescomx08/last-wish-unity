"""Animated pixel-art illustrations for the last batch of the rogue's loot cards (88x64, 24 frames at 12 fps, seamless):
her last two epics and her nine legendaries.

Same technique and helpers as rogue_pool_art.py .. rogue_pool_art_5.py (docs/art-style.md): every frame has a base
image (opaque pixel art, readable without bloom) and an emissive light map, baked for the game by
export_game_card_art.py. No characters: her daggers and cards, chains, a metronome, storms, and what they hit.

  rapina             Rapiña (poder: cada carta que descartes inflige 3 de daño a un enemigo al azar): each card she
                     throws on the discard heap comes back out as a violet blade at one of two sigils, chosen at random.
  sombra_gemela      Sombra Gemela (poder: la primera carta de cada turno que active su Combo lo activa dos veces): the
                     dagger and its violet twin cut the same crescent one after the other; the Combo sign lights twice.
  asesina            Asesina (poder: tus ataques rematan a los enemigos por debajo del 25 % de vida): a cut drops the
                     enemy's life bar under the skull notch; a crimson execution cut shatters its sigil.
  cadena_perfecta    Cadena Perfecta (poder: cada quinta carta de un turno se lanza una vez más): five links of a chain
                     light as five cards are played; the fifth is gold and its card is cast twice.
  lluvia_de_golpes   Lluvia de Golpes (5 de daño a todos por cada ataque en el descarte): three attack cards in the
                     discard each call down a rain of blades on the three enemies.
  mazo_impecable     Mazo Impecable (40 de daño si toda tu mano cuesta 1 o menos; Combo: +12): the four cards of her
                     hand show cheap gems one by one, all of them light gold and a huge golden blade falls.
  mil_cortes         Mil Cortes (por cada carta en tu descarte, 3 de daño a un enemigo al azar): under a blood moon the
                     cards of the discard flash one after another and each one is a cut on one of two enemies.
  nada_que_perder    Nada que Perder (poder: al inicio de cada turno, descarta una carta al azar y roba 2): a roulette
                     picks a card of her hand, it burns in a brazier and two cards rise out of the smoke.
  ritmo_letal        Ritmo Letal (poder: la primera carta de cada turno cuesta 1 menos): a metronome ticks in a clock
                     tower; on the first beat of the turn the card's mana gem chips one mana off.
  tormenta_de_acero  Tormenta de Acero (poder: al inicio de cada turno, 3 de daño a todos; Singular: 5): a storm cloud
                     rains daggers on the three enemies; with Singular the second rain is violet and heavier.
  tormenta_de_veneno Tormenta de Veneno (5 de Veneno a todos): a green cloud gathers over a swamp and pours poison on
                     the three enemies, whose sigils turn green and bubble.
"""
import math

import numpy as np

from pixelkit import Canvas, hexc, dilate, erode, shift
from rogue_art import (AW, AH, FRAMES, K, L, ease_out, ease_in_out, gradient_bg, vignette, star4, motes, outline_mask,
                       crescent, arc_dagger)
from mage_art import twinkling_stars, sparks, ellipse_ring
from warrior_art import shake, CYAN_SPARKS
from rogue_pool_art import (KR, LR, KB, LB, VIOLET_SPARKS, GOLD_SPARKS, dir_of, dagger, speed_lines, converge, mana_drop,
                            sigil)
from rogue_pool_art_2 import K2, L2, paint_card, POISON_SPARKS
from mage_art import flame
from rogue_pool_art_3 import K3, CRIM_SPARKS, plus_marks, slash_line, rising_card, CPAL, RPAL
from rogue_pool_art_4 import K4, GOLD, lerp
from rogue_pool_art_5 import (K5, LC, enemy_sigil, type_card, kind_card_rot, power_ring, combo_sign, falling_card,
                              big_card)

K6 = dict(K5)
K6.update({
    'chain0': hexc('#1e1c2a'), 'chain1': hexc('#3a3850'), 'chain2': hexc('#65637e'), 'chain3': hexc('#9e9cb8'),
    'blood0': hexc('#1a0610'), 'blood1': hexc('#2e0a18'), 'blood2': hexc('#4a1222'),
    'storm0': hexc('#0e0c1a'), 'storm1': hexc('#1c1830'), 'storm2': hexc('#2c2646'), 'storm3': hexc('#443c66'),
})
VPAL = (K['violet0'], K['violet2'], K['violet3'], K['violet4'])
WARM_SPARKS6 = (KR['spark2'], KR['spark1'], KR['spark0'])
K2_POIS0, K2_POIS1, K2_POIS2, K2_POIS3, K2_POIS4 = (K2['pois0'], K2['pois1'], K2['pois2'], K2['pois3'], K2['pois4'])
L2_POISON = L2['poison']
GPAL = (KR['gold0'], KR['gold2'], KR['gold3'], K['white'])


# ---------------------------------------------------------------------------------------- shared bits
def ghost_blade(c, g, x0, y0, x1, y1, u, col=None, light=None, s=0.5):
    """A shadow blade flying from (x0, y0) to (x1, y1), u 0..1, point first, with a dotted wake."""
    col = K['violet3'] if col is None else col
    light = L['violet'] if light is None else light
    ang = math.atan2(y1 - y0, x1 - x0)
    hx, hy = lerp(x0, x1, u), lerp(y0, y1, u)
    gx, gy = hx - math.cos(ang) * 40 * s, hy - math.sin(ang) * 40 * s
    m = c.line(x0, y0, gx, gy) & (((c.x + c.y) % 2) < 1)
    c.put(m, col, 0.7)
    g.add(m, light, 0.35)
    dagger(c, g, gx, gy, ang, s, 0.0, ghost=(col, 0.95, light))


def hp_bar(c, g, x, y, w, frac_, notch=None, flash=0.0):
    """An enemy's life bar: iron frame, crimson fill; notch = a fraction marked with a little skull."""
    frame = c.rect(int(x - w / 2) - 1, int(y) - 1, int(x + w / 2), int(y) + 2)
    c.put(outline_mask(frame), K['ink'])
    c.put(frame, KR['iron1'])
    inner = c.rect(int(x - w / 2), int(y), int(x + w / 2) - 1, int(y) + 1)
    c.put(inner, K6['blood1'])
    fill = inner & (c.x < x - w / 2 + w * frac_)
    c.put(fill, K3['crim3'] if flash <= 0 else K['white'])
    c.put(fill & (c.y < y + 1), K3['crim4'] if flash <= 0 else K['white'])
    g.add(fill, LR['crimson'], 0.25 + flash)
    if notch is not None:
        nx = x - w / 2 + w * notch
        c.put(c.rect(int(nx), int(y) - 2, int(nx), int(y) + 3), KR['gold2'])
        skull(c, g, nx, y - 5, lit=1.0 if frac_ <= notch else 0.0)


def skull(c, g, x, y, lit=0.0):
    x, y = int(x), int(y)
    m = c.rect(x - 2, y - 2, x + 2, y + 1) | c.rect(x - 1, y + 2, x + 1, y + 2)
    m &= ~(c.pixel(x - 1.5, y - 2.5) & False)
    eyes = c.pixel(x - 0.5, y + 0.5) | c.pixel(x + 1.5, y + 0.5)
    c.put(outline_mask(m), K['ink'])
    c.put(m, K4['bone2'] if lit < 0.5 else K3['crim5'])
    c.put(eyes, K['ink'])
    if lit > 0:
        g.add(m, LR['crimson'], 0.9 * lit)
    return m


# ---------------------------------------------------------------------------------------- Rapiña
RA_HEAP = (18.0, 52.0)
RA_SIGILS = ((60.0, 14.0), (74.0, 36.0))
# (a card is thrown on the heap, the blade leaves the heap, which enemy the roulette picks)
RA_THROWS = ((2, 6, 0), (12, 16, 1))
RA_FLIGHT = 3


def back_street(c, g, t):
    gradient_bg(c, K['night0'], K['night2'], K['night1'], 0.6)
    twinkling_stars(c, g, t, 6201, 10, (2, 2, 86, 18))
    houses = c.poly([(0, 24), (10, 16), (20, 24), (20, 20), (34, 20), (34, 14), (44, 8), (54, 14), (54, 22), (66, 22),
                     (66, 16), (76, 10), (88, 18), (88, 54), (0, 54)])
    c.put(houses, K['roof'])
    c.put(houses & ~shift(houses, 0, 1), K['night3'])
    for i, (wx, wy) in enumerate(((6, 30), (28, 28), (48, 20), (82, 26))):
        win = c.rect(wx, wy, wx + 1, wy + 2)
        c.put(win, K['window2'])
        g.add(win, L['warm'], 0.2)
    c.put(c.rect(0, 54, AW - 1, AH - 1), KR['tile1'])
    c.put(c.rect(0, 54, AW - 1, 54), K['night3'])
    for x in range(-4, AW, 11):
        c.put(c.rect(x, 58, x + 7, 58), KR['tile0'])


def rapina_frame(f):
    t = f / FRAMES
    c, g = Canvas(AW, AH), Canvas(AW, AH)
    back_street(c, g, t)
    power_ring(c, g, RA_HEAP[0], RA_HEAP[1] + 3, 12, 2.5, t, 0.85)
    for i, (sx, sy) in enumerate(RA_SIGILS):
        hit = -99
        for throw, launch, pick in RA_THROWS:
            if pick == i and f >= launch + RA_FLIGHT:
                hit = f - launch - RA_FLIGHT
        enemy_sigil(c, g, sx, sy, t, hit)
    # the discard heap: a few cards, and each new one thrown on it
    for k, (dx, ang, kind) in enumerate(((-5, -0.4, 'attack'), (0, 0.15, 'skill'), (5, 0.5, 'power'))):
        kind_card_rot(c, g, RA_HEAP[0] + dx, RA_HEAP[1] - 3 - (k % 2), ang, kind, size=0.85)
    for n, (throw, launch, pick) in enumerate(RA_THROWS):
        falling_card(c, g, f, throw, 40, 10, RA_HEAP[0] + 2, RA_HEAP[1] - 6, life=launch - throw - 1,
                     kind='skill' if n == 0 else 'attack')
        a = f - launch
        if a == 0:                                                    # the heap gives up a blade
            star4(c, g, RA_HEAP[0], RA_HEAP[1] - 10, 4, core=K['white'], col=K['violet4'], edge=K['violet3'], light=L['violet'])
            for j, (sx, sy) in enumerate(RA_SIGILS):                  # the roulette: both sigils flicker, one is chosen
                ring = ellipse_ring(c, sx, sy, 9, 9, 0.5)
                c.put(ring & (((c.x + c.y) % 3) < 1), K['violet3'] if j == pick else K['violet1'])
        if 0 <= a < RA_FLIGHT:
            tx, ty = RA_SIGILS[pick]
            ghost_blade(c, g, RA_HEAP[0], RA_HEAP[1] - 10, tx - 2, ty + 1, ease_in_out((a + 1) / RA_FLIGHT))
        h = a - RA_FLIGHT
        if 0 <= h <= 1:
            tx, ty = RA_SIGILS[pick]
            star4(c, g, tx - 1, ty, 5 if h == 0 else 3, core=K['white'], col=K['violet4'], edge=K['violet3'], light=L['violet'],
                  gain=1.3)
        sparks(c, g, f, launch + RA_FLIGHT, RA_SIGILS[pick][0], RA_SIGILS[pick][1], 6203 + pick, 8, (0.8, 2.0), 5,
               VIOLET_SPARKS, L['violet'], gravity=0.12)
    motes(c, g, t, 6205, 7, (4, 6, 84, 52), K['violet3'], K3['crim4'], L['violet'], gain=0.4)
    vignette(c, 0.72)
    return c, g


# ---------------------------------------------------------------------------------------- Sombra Gemela
SG_HOME = (22.0, 40.0)
SG_ANG = -0.7
SG_SIGIL = (66.0, 30.0)
SG_CUTS = (8, 10)              # the dagger's cut, then its twin's (the Combo, twice)
SG_ARC = ((63.0, 52.0), 24.0)
SG_HEADS = {0: math.radians(140), 1: math.radians(88), 2: math.radians(36)}
SG_TAIL = {0: 0.5, 1: 1.1, 2: 1.2}


def lake_night(c, g, t):
    gradient_bg(c, K['night0'], K['vio2'], K['night1'], 0.5)
    twinkling_stars(c, g, t, 6301, 14, (2, 2, 86, 26), light=L['violet'])
    moon = c.disc(16, 10, 5)
    c.put(moon, K['moon1'])
    c.put(c.disc(15, 9, 3), K['moon2'])
    g.add(moon, L['violet'], 0.3)
    shore = c.poly([(0, 34), (18, 31), (40, 34), (62, 30), (88, 33), (88, 38), (0, 38)])
    c.put(shore, K['roof'])
    water = c.rect(0, 38, AW - 1, AH - 1)
    c.put(water, K['vio1'])
    for i, y in enumerate((41, 45, 50, 56, 61)):
        off = (t * 88 * (1 if i % 2 else -1)) % 22
        for x in np.arange(-22 + off, AW, 22):
            c.put(c.rect(int(x), y, int(x) + 6 + i, y), K['vio2'])
    c.put(c.rect(14, 42, 18, 42) | c.rect(13, 47, 19, 47), K['moon0'])       # the moon in the water


def twin_pose(f, twin):
    """(guard x, guard y, angle, mode) of the dagger or its twin; mode 'arc' = riding the cut."""
    d = 2 if twin else 0
    cut = SG_CUTS[1] if twin else SG_CUTS[0]
    ox, oy = (-6, -5) if twin else (0, 0)
    if cut - 3 <= f < cut:
        k = ease_out((f - cut + 4) / 3.0)
        return SG_HOME[0] + ox - 3 * k, SG_HOME[1] + oy + 2 * k, SG_ANG - 0.6 * k, 'pose'
    if cut <= f <= cut + 2:
        return f - cut, None, None, 'arc'
    if cut + 3 <= f <= cut + 6:
        return SG_HOME[0] + ox, SG_HOME[1] + oy, SG_ANG, 'pose'
    return SG_HOME[0] + ox, SG_HOME[1] + oy + math.sin((f / FRAMES + d * 0.1) * math.pi * 4) * 0.8, SG_ANG, 'pose'


def sombra_gemela_frame(f):
    t = f / FRAMES
    c, g = Canvas(AW, AH), Canvas(AW, AH)
    lake_night(c, g, t)
    power_ring(c, g, 30, 56, 18, 3, t, 0.8)
    hit = max([f - cut - 1 for cut in SG_CUTS if f >= cut + 1], default=-99)
    enemy_sigil(c, g, SG_SIGIL[0], SG_SIGIL[1], t, hit)
    (acx, acy), ar = SG_ARC
    for twin in (True, False):
        x, y, ang, mode = twin_pose(f, twin)
        pal = VPAL if twin else CPAL
        light = L['violet'] if twin else L['cyan']
        if mode == 'arc':
            k = x
            th = SG_HEADS[k]
            crescent(c, g, acx, acy, ar, th, SG_TAIL[k], -1, 4.5 if k < 2 else 2.5, pal, light, a=1.0 if k < 2 else 0.6,
                     gain=1.4)
            if twin:
                arc_dagger(c, g, acx, acy, ar, th, 0.6, t, ghost=(K['violet3'], 0.95, L['violet']))
            else:
                arc_dagger(c, g, acx, acy, ar, th, 0.6, t, rune_gain=2.0)
        else:
            if twin:
                dagger(c, g, round(x), round(y), ang, 0.6, t * 2, ghost=(K['violet3'], 0.8, L['violet']))
            else:
                dagger(c, g, round(x), round(y), ang, 0.6, t * 2, -2 + (f - 18) * 10.0 if 18 <= f <= 22 else None)
    for k, cut in enumerate(SG_CUTS):
        a = f - cut - 1
        if 0 <= a <= 1:
            star4(c, g, SG_SIGIL[0] - 1, SG_SIGIL[1], 5 if a == 0 else 3, gain=1.3,
                  col=K['violet4'] if k else K['cyan3'], edge=K['violet3'] if k else K['cyan2'],
                  light=L['violet'] if k else L['cyan'])
        sparks(c, g, f, cut + 1, SG_SIGIL[0], SG_SIGIL[1], 6303 + k, 8, (0.8, 2.0), 5,
               VIOLET_SPARKS if k else CYAN_SPARKS, L['violet'] if k else L['cyan'], gravity=0.12)
    # the Combo sign: lit by the first cut, then a second one beside it (it happens twice)
    for k, cut in enumerate(SG_CUTS):
        a = f - cut
        if 0 <= a <= 9:
            fade = 1.0 if a < 7 else 1 - (a - 6) / 4.0
            combo_sign(c, g, 34 + k * 14, 13, fade, size=0.9)
            if a == 0:
                star4(c, g, 34 + k * 14, 13, 4, core=K['white'], col=K5['combo3'], edge=K5['combo2'], light=LC)
    motes(c, g, t, 6305, 7, (4, 6, 84, 50), K['violet3'], K5['combo2'], L['violet'], gain=0.45)
    vignette(c, 0.72)
    return c, g


# ---------------------------------------------------------------------------------------- Asesina
AN_SIGIL = (58.0, 26.0)
AN_BAR = (58.0, 42.0, 26.0)     # x, y, width of its life bar
AN_NOTCH = 0.25
AN_CUT = 6                      # an ordinary cut: the bar drops under the notch
AN_EXEC = 10                    # the execution
AN_BACK = (19, 22)              # a new enemy takes its place
AN_HOME = (16.0, 34.0)
AN_ANG = -0.25


def throne_room(c, g, t):
    gradient_bg(c, K6['blood0'], K6['blood2'], K6['blood1'], 0.6)
    for x in (4, 78):
        col = c.rect(x, 0, x + 6, 52)
        c.put(outline_mask(col), K['ink'])
        c.put(col, K6['blood1'])
        c.put(col & (c.x < x + 2), K6['blood2'])
    for x in range(16, 76, 10):                                       # torn banners
        b = c.poly([(x, 0), (x + 6, 0), (x + 6, 14), (x + 4, 12), (x + 2, 16), (x, 13)])
        c.put(b, K6['blood2'])
    c.put(c.rect(0, 52, AW - 1, AH - 1), K6['blood0'])
    c.put(c.rect(0, 52, AW - 1, 52), K6['blood2'])
    for x in range(-6, AW, 12):
        c.put(c.line(x, 53, x - 4, 63), K['ink'])


def asesina_frame(f):
    t = f / FRAMES
    c, g = Canvas(AW, AH), Canvas(AW, AH)
    throne_room(c, g, t)
    power_ring(c, g, AN_SIGIL[0], 56, 18, 3, t, 0.85, col=K3['crim3'], light=LR['crimson'])
    # its life: 40 %, an ordinary cut takes it to 20 % (under the skull), the execution empties it
    if f < AN_CUT:
        life = 0.4
    elif f < AN_EXEC:
        life = 0.2
    elif f < AN_BACK[0]:
        life = max(0.0, 0.2 - (f - AN_EXEC + 1) * 0.1)
    else:
        life = 0.4 * (f - AN_BACK[0] + 1) / (AN_BACK[1] - AN_BACK[0] + 1) if f <= AN_BACK[1] else 0.4
    ex = f - AN_EXEC
    gone = AN_EXEC + 1 <= f < AN_BACK[0]
    if not gone:
        hit = f - AN_CUT if AN_CUT <= f < AN_EXEC else ex if ex >= 0 else -99
        if f >= AN_BACK[0]:                                           # a new one forms, ring by ring
            k = (f - AN_BACK[0] + 1) / (AN_BACK[1] - AN_BACK[0] + 2)
            sigil(c, g, AN_SIGIL[0], AN_SIGIL[1], 6 * k + 1, 6 * k + 1, t * 1.5, 0.9, col=K3['crim3'],
                  light=LR['crimson'])
        else:
            enemy_sigil(c, g, AN_SIGIL[0], AN_SIGIL[1], t, hit)
    hp_bar(c, g, AN_BAR[0], AN_BAR[1], AN_BAR[2], life, AN_NOTCH, flash=0.6 if f in (AN_CUT, AN_EXEC) else 0.0)
    if AN_CUT + 1 <= f < AN_EXEC:                                     # the skull wakes: it can be finished off
        glow = c.disc(AN_BAR[0] - AN_BAR[2] / 2 + AN_BAR[2] * AN_NOTCH, AN_BAR[1] - 5, 3.5)
        g.add(glow, LR['crimson'], 0.3)
    if 0 <= ex <= 7:                                                  # the sigil shatters
        rng = np.random.default_rng(6401)
        for i in range(10):
            a0 = rng.uniform(0, 2 * math.pi)
            sp = rng.uniform(0.8, 1.8)
            px = AN_SIGIL[0] + math.cos(a0) * (3 + sp * ex)
            py = AN_SIGIL[1] + math.sin(a0) * (3 + sp * ex) + 0.2 * ex * ex
            m = c.line(px, py, px + math.cos(a0 + 1.2) * 2, py + math.sin(a0 + 1.2) * 2)
            c.put(m, K3['crim5'] if ex < 3 else K3['crim2'], 1 - ex / 9)
            g.add(m, LR['crimson'], 0.6 * (1 - ex / 8))
        if ex <= 6:
            skull(c, g, AN_SIGIL[0], AN_SIGIL[1] - ex * 0.6, lit=1.0)
    # the dagger: a quick cut, then the killing one (crimson runes, a long crescent)
    lunge = {4: -2.0, 5: -3.0, 6: 14.0, 7: 8.0, 8: -3.0, 9: -5.0, 10: 26.0, 11: 22.0, 12: 14.0, 13: 7.0, 14: 2.0}.get(f, 0.0)
    ux, uy = dir_of(AN_ANG)
    bob = math.sin(t * math.pi * 4) * 0.8 if not 4 <= f <= 14 else 0.0
    gx, gy = AN_HOME[0] + ux * lunge, AN_HOME[1] + uy * lunge + bob
    if f == AN_EXEC:
        for back, al in ((18, 0.3), (9, 0.55)):
            dagger(c, g, gx - ux * back, gy - uy * back, AN_ANG, 0.62, t * 2, ghost=(K3['crim5'], al, LR['crimson']))
        slash_line(c, g, AN_SIGIL[0], AN_SIGIL[1], -0.9, 13, 2.8, RPAL, LR['crimson'])
    if f == AN_CUT:
        slash_line(c, g, AN_SIGIL[0], AN_SIGIL[1], 0.6, 8, 1.4, CPAL, L['cyan'], a=0.9)
    lab = dagger(c, g, round(gx), round(gy), AN_ANG, 0.62, t * 2, -2 + (f - 18) * 10.0 if 18 <= f <= 22 else None,
                 rune_gain=1.6 if 8 <= f <= 13 else 1.0)
    if 8 <= f <= 13:                                                  # crimson runes for the kill
        fl = lab == 5
        c.put(fl, K3['crim4'])
        c.put(fl & (c.B < 0.4), K3['crim5'])
        g.add(fl, LR['crimson'], 1.2)
    if 0 <= ex <= 2:
        star4(c, g, AN_SIGIL[0], AN_SIGIL[1], {0: 7, 1: 5, 2: 3}[ex], core=K['white'], col=K3['crim5'], edge=K3['crim3'],
              light=LR['crimson'], gain=1.5)
    if ex == 0:
        shake(c, g, 1, 0)
    motes(c, g, t, 6403, 6, (4, 6, 84, 50), K3['crim4'], K['violet3'], LR['crimson'], gain=0.4)
    vignette(c, 0.72)
    return c, g


# ---------------------------------------------------------------------------------------- Cadena Perfecta
CP_Y = 26.0
CP_LINKS = (16.0, 30.0, 44.0, 58.0, 72.0)
CP_PLAYS = (2, 5, 8, 11, 14)    # five cards played; the fifth is cast once more (echo two frames later)
CP_ECHO = 2


def chain_link(c, g, x, y, lit, gold, t):
    """One oval chain link; lit 0..1 lights its rune; gold = the fifth link."""
    outer = (((c.x - x) / 6.5) ** 2 + ((c.y - y) / 4.2) ** 2) <= 1
    hole = (((c.x - x) / 4.0) ** 2 + ((c.y - y) / 1.8) ** 2) <= 1
    m = outer & ~hole
    c.put(outline_mask(m), K['ink'])
    if gold:
        c.put(m, KR['gold1'] if lit < 0.5 else KR['gold2'])
        c.put(m & (c.y < y - 1) & (c.x < x), KR['gold3'])
    else:
        c.put(m, K6['chain1'] if lit < 0.5 else K6['chain2'])
        c.put(m & (c.y < y - 1) & (c.x < x), K6['chain3'])
    if lit > 0:
        rune = m & (c.y > y + 1.5) & (np.abs(c.x - x) < 2.5)
        c.put(rune, KR['gold3'] if gold else K['cyan3'])
        g.add(rune | (m & (c.y < y - 2)), GOLD if gold else L['cyan'], 0.9 * lit)
    return m


def vault(c, g, t):
    gradient_bg(c, K['night0'], K['teal2'], K['night1'], 0.6)
    for row, y in enumerate(range(0, 52, 7)):
        off = 0 if row % 2 else 7
        for x in range(-14 + off, AW, 14):
            c.put(c.rect(x + 1, y + 1, x + 13, y + 6), K['teal1'] if (x + row) % 3 else K['teal2'])
    for x in (4, 84):                                                 # the chain's anchors
        c.put(c.rect(x - 2, int(CP_Y) - 3, x + 2, int(CP_Y) + 3), KR['iron2'])
    c.put(c.rect(0, 52, AW - 1, AH - 1), K['teal0'])
    c.put(c.rect(0, 52, AW - 1, 52), K['teal3'])


def cadena_perfecta_frame(f):
    t = f / FRAMES
    c, g = Canvas(AW, AH), Canvas(AW, AH)
    vault(c, g, t)
    power_ring(c, g, 44, 56, 26, 3.5, t, 0.8, col=KR['gold1'], light=GOLD)
    sag = lambda x: CP_Y + 3 * math.sin((x - 4) / 80 * math.pi)
    # the small connecting links between the big ones
    for xa in [4.0] + list(CP_LINKS):
        xb = xa + 7
        if xb > 84:
            continue
        m = c.rect(int(xa + 5), int(sag(xa + 7)) - 1, int(xa + 8), int(sag(xa + 7)) + 1)
        c.put(m, K6['chain1'])
    rest_dim = f >= 19
    for k, (x, played) in enumerate(zip(CP_LINKS, CP_PLAYS)):
        lit = 0.0 if f < played or rest_dim else 1.0 if f - played <= 1 else 0.6
        if k == 4 and played <= f <= played + 4:
            lit = 1.0
        y = sag(x) + (1 if f == played else 0)
        chain_link(c, g, x, round(y), lit, k == 4, t)
        # the card played this turn passes over its link
        a = f - played
        if -2 <= a <= 0:
            u = (a + 3) / 3.0
            paint_card(c, g, x, lerp(58, y - 9, u), 1.0, glow=0.5)
        elif 1 <= a <= 3 and not (k == 4):
            u = a / 3.0
            paint_card(c, g, x + 3 * u, y - 9 - 8 * u, 1.0, alpha=1.0 if a < 3 else 0.5)
        if a == 0:
            star4(c, g, x, y - 1, 3 if k < 4 else 5, core=K['white'], col=KR['gold3'] if k == 4 else K['cyan3'],
                  edge=KR['gold2'] if k == 4 else K['cyan2'], light=GOLD if k == 4 else L['cyan'])
    # the fifth card is cast twice: it splits into itself and a gold echo that fly up together
    fifth = f - CP_PLAYS[4]
    if 1 <= fifth <= 6:
        x, y = CP_LINKS[4], sag(CP_LINKS[4]) - 9
        u = fifth / 6.0
        paint_card(c, g, x - 7 * u, y - 10 * u, 1.0, glow=0.8)
        if fifth >= CP_ECHO:
            ue = (fifth - CP_ECHO + 1) / (7 - CP_ECHO)
            m = paint_card(c, g, x + 7 * ue, y - 10 * ue, 1.0)
            c.put(m, KR['gold2'])
            c.put(erode(m), KR['gold1'])
            g.add(outline_mask(m), GOLD, 0.9)
            if fifth == CP_ECHO:
                star4(c, g, x, y, 6, core=K['white'], col=KR['gold3'], edge=KR['gold2'], light=GOLD, gain=1.4)
    sparks(c, g, f, CP_PLAYS[4] + CP_ECHO, CP_LINKS[4], sag(CP_LINKS[4]) - 9, 6501, 12, (0.8, 2.2), 6, GOLD_SPARKS, GOLD,
           gravity=0.06)
    motes(c, g, t, 6503, 6, (4, 6, 84, 50), K['cyan3'], KR['gold2'], L['cyan'], gain=0.4)
    vignette(c, 0.72)
    return c, g


# ---------------------------------------------------------------------------------------- Lluvia de Golpes
LG_PILE = (14.0, 50.0)
LG_SIGILS = ((42.0, 48.0), (60.0, 46.0), (78.0, 48.0))
LG_VOLLEYS = (3, 9, 15)         # each attack card in the discard calls down a volley
LG_FALL = 3


def storm_field(c, g, t, flash):
    gradient_bg(c, K6['storm0'], K6['storm2'], K6['storm1'], 0.6)
    rng = np.random.default_rng(6601)
    for i in range(7):                                                # heavy clouds
        x = (rng.uniform(0, 100) + t * 44 * (1 if i % 2 else -1)) % 110 - 12
        y = rng.uniform(2, 12)
        cl = (((c.x - x) / rng.uniform(12, 20)) ** 2 + ((c.y - y) / 4.0) ** 2) <= 1
        c.put(cl, K6['storm3'] if flash else K6['storm2'])
        c.put(cl & (c.y > y + 1), K6['storm1'])
    c.put(c.rect(0, 52, AW - 1, AH - 1), K4['heath1'])
    c.put(c.rect(0, 52, AW - 1, 52), K4['heath2'])
    for x in range(0, AW, 7):
        c.put(c.line(x, 54, x + 1, 51), K4['heath2'])


def ground_sigil(c, g, x, y, t, hit):
    """An enemy's sigil on the ground (seen at a slant), crimson, struck = flash."""
    hurt = 0 <= hit <= 3
    ring = ellipse_ring(c, x, y, 7, 2.2, 0.55)
    ang = np.arctan2((c.y - y) / 2.2, (c.x - x) / 7)
    ring &= ((ang / (2 * math.pi) * 10 + t * 2) % 1) < 0.7
    c.put(ring, K['white'] if hit == 0 else K3['crim4'] if hurt else K3['crim3'])
    g.add(ring, LR['crimson'], 0.6 + (0.6 if hurt else 0.0))
    eye = c.rect(int(x) - 1, int(y) - 7, int(x) + 1, int(y) - 6)
    c.put(eye, K3['crim5'])
    g.add(eye, LR['crimson'], 0.8)


def lluvia_de_golpes_frame(f):
    t = f / FRAMES
    c, g = Canvas(AW, AH), Canvas(AW, AH)
    flash = any(f == v for v in LG_VOLLEYS)
    storm_field(c, g, t, flash)
    hits = [f - v - LG_FALL for v in LG_VOLLEYS if f >= v + LG_FALL]
    hit = min(hits) if hits else -99
    for (sx, sy) in LG_SIGILS:
        ground_sigil(c, g, sx, sy, t, hit)
    # the discard: three attack cards; each lights in turn and sends its light up into the clouds
    for k, v in enumerate(LG_VOLLEYS):
        lit = v <= f <= v + 1
        x, y = LG_PILE[0] - 4 + k * 4, LG_PILE[1] - k * 2
        type_card(c, g, x, y, 'attack', glow=1.0 if lit else 0.0)
        if lit:
            beam = c.line(x, y - 7, x + 10, 8) & (((c.x + c.y + f) % 2) < 1)
            c.put(beam, K3['crim4'])
            g.add(beam, LR['crimson'], 0.6)
    # each volley: blades fall from the clouds on all three enemies (5 de daño a todos)
    rng = np.random.default_rng(6603)
    for k, v in enumerate(LG_VOLLEYS):
        a = f - v
        if 0 <= a < LG_FALL + 1:
            for j, (sx, sy) in enumerate(LG_SIGILS):
                for n in range(2):
                    ox = rng.uniform(-4, 4) + (n - 0.5) * 5
                    u = min(1.0, (a + 1) / (LG_FALL + 0.0) - n * 0.15)
                    if u <= 0:
                        continue
                    hx, hy = sx + ox - 6 * (1 - u), lerp(4, sy - 1, u)
                    ang = math.pi / 2 - 0.2
                    gx, gy = hx - math.cos(ang) * 40 * 0.42, hy - math.sin(ang) * 40 * 0.42
                    if u < 1:
                        dagger(c, g, gx, gy, ang, 0.42, t * 2, rune_gain=1.3)
                        m = c.line(gx - 3, gy - 10, gx, gy - 2) & (((c.x + c.y) % 2) < 1)
                        c.put(m, K['cyan3'])
                        g.add(m, L['cyan'], 0.4)
        h = f - v - LG_FALL
        for j, (sx, sy) in enumerate(LG_SIGILS):
            if 0 <= h <= 1:
                star4(c, g, sx, sy - 2, 4 if h == 0 else 2, gain=1.2)
            sparks(c, g, f, v + LG_FALL, sx, sy - 1, 6605 + k * 10 + j, 5, (0.7, 1.6), 4, CYAN_SPARKS, L['cyan'],
                   gravity=0.2, spread=(0.3, math.pi - 0.3))
    if flash:                                                         # lightning in the clouds as the volley is called
        bolt = c.line(30, 0, 27, 6) | c.line(27, 6, 31, 9) | c.line(31, 9, 28, 14)
        c.put(bolt, K['white'])
        g.add(dilate(bolt), L['violet'], 0.6)
    motes(c, g, t, 6607, 6, (4, 6, 84, 50), K['cyan3'], K3['crim4'], L['cyan'], gain=0.4)
    vignette(c, 0.72)
    return c, g


# ---------------------------------------------------------------------------------------- Mazo Impecable
MI_HAND = ((14.0, 52.0), (26.0, 52.0), (38.0, 52.0), (50.0, 52.0))
MI_KINDS = ('attack', 'skill', 'attack', 'power')
MI_CHECKS = (3, 4, 5, 6)        # each card's gem shows it costs 1 or less
MI_ALL = 7                      # the whole hand lights gold
MI_FALL = (8, 10)               # the golden blade falls; hits on 10
MI_COMBO = 13                   # Combo: +12
MI_SIGIL = (70.0, 34.0)


def gold_hall(c, g, t):
    gradient_bg(c, K['night0'], K['vio2'], K['vio1'], 0.6)
    for x in (2, 82):
        col = c.rect(x, 0, x + 4, 58)
        c.put(col, KR['gold0'])
        c.put(col & (c.x < x + 1), KR['gold1'])
    arch = ellipse_ring(c, 44, 30, 30, 26, 0.8) & (c.y < 30)
    c.put(arch, KR['gold1'])
    c.put(c.rect(0, 58, AW - 1, AH - 1), K['vio0'])
    c.put(c.rect(0, 58, AW - 1, 58), KR['gold1'])


def mazo_impecable_frame(f):
    t = f / FRAMES
    c, g = Canvas(AW, AH), Canvas(AW, AH)
    gold_hall(c, g, t)
    hit = f - MI_FALL[1]
    combo = f - MI_COMBO
    enemy_sigil(c, g, MI_SIGIL[0], MI_SIGIL[1], t, hit if hit >= 0 and combo < 0 else combo if combo >= 0 else -99, r=8)
    # her hand: four cards, each with a cheap gem (one mana or none) that lights in turn; then all of them, gold
    for k, ((x, y), kind) in enumerate(zip(MI_HAND, MI_KINDS)):
        checked = f >= MI_CHECKS[k] and f < 20
        allgold = MI_ALL <= f <= MI_FALL[1] + 2
        type_card(c, g, x, y, kind, glow=1.0 if allgold else 0.0)
        gem = (x - 4, y - 7)
        mana_drop(c, g, gem[0], gem[1], 0.75, gain=1.2 if checked else 0.3)
        if f == MI_CHECKS[k]:
            star4(c, g, gem[0], gem[1], 3, core=K['white'], col=KR['mana3'], edge=KR['mana2'], light=LR['mana'])
        if allgold:
            m = outline_mask(dilate(c.rect(int(x) - 5, int(y) - 7, int(x) + 5, int(y) + 7)))
            c.put(m, KR['gold3'])
            g.add(m, GOLD, 0.7)
    if f == MI_ALL:
        for (x, y) in MI_HAND:
            star4(c, g, x, y - 9, 3, core=K['white'], col=KR['gold3'], edge=KR['gold2'], light=GOLD)
    # a huge golden blade falls on the enemy (40 de daño)
    f0, f1 = MI_FALL
    if f0 <= f <= f1 + 3:
        if f < f1:
            u = (f - f0 + 1) / (f1 - f0 + 1)
            gy = lerp(-40, MI_SIGIL[1] - 36, ease_in_out(u))
        else:
            gy = MI_SIGIL[1] - 36 + (2 if f == f1 else 0)
        gx = MI_SIGIL[0]
        lab = dagger(c, g, gx, round(gy), math.pi / 2, 0.95, t * 2, rune_gain=2.0,
                     cut=(MI_SIGIL[1] + 2 - gy) / 0.95 if f >= f1 else None)
        fl = lab == 5
        c.put(fl, KR['gold2'])
        c.put(fl & (c.B < 0.5), KR['gold3'])
        g.add(fl, GOLD, 1.3)
        c.put((lab == 1), KR['gold3'])
        g.add(dilate(lab > 0) & ~(lab > 0), GOLD, 0.35)
        if f < f1:
            speed_lines(c, g, gx, gy, 0.0, 1.0, 1.0, 6701 + f, count=6, spread=8, length=(8, 14))
    if 0 <= hit <= 3:
        star4(c, g, MI_SIGIL[0], MI_SIGIL[1], {0: 10, 1: 7, 2: 4, 3: 2}[hit], core=K['white'], col=KR['gold3'],
              edge=KR['gold2'], light=GOLD, gain=1.6)
        for rr in (6 + hit * 6, 3 + hit * 4):
            ring = ellipse_ring(c, MI_SIGIL[0], MI_SIGIL[1], rr, rr * 0.8, 0.6)
            c.put(ring, KR['gold3'], 1 - hit / 4)
            g.add(ring, GOLD, 0.8 * (1 - hit / 4))
    if hit == 0:
        shake(c, g, 1, 1)
    elif hit == 1:
        shake(c, g, -1, 0)
    sparks(c, g, f, MI_FALL[1], MI_SIGIL[0], MI_SIGIL[1], 6703, 16, (1.0, 3.0), 7, GOLD_SPARKS, GOLD, gravity=0.15)
    # Combo: a crimson cut across it and the "+" of the extra 12
    if 0 <= combo <= 1:
        slash_line(c, g, MI_SIGIL[0], MI_SIGIL[1], 0.8, 12, 2.4 * (1 - combo * 0.5), RPAL, LR['crimson'])
        star4(c, g, MI_SIGIL[0] + 2, MI_SIGIL[1] - 2, 5 if combo == 0 else 3, core=K['white'], col=K3['crim5'],
              edge=K3['crim3'], light=LR['crimson'])
    plus_marks(c, g, MI_SIGIL[0] + 9, MI_SIGIL[1] - 12, combo - 1 if combo >= 1 else -1, count=3)
    motes(c, g, t, 6705, 7, (4, 6, 84, 50), KR['gold2'], KR['mana2'], GOLD, gain=0.4)
    vignette(c, 0.72)
    return c, g


# ---------------------------------------------------------------------------------------- Mil Cortes
MC_PILE = (14.0, 50.0)
MC_SIGILS = ((52.0, 22.0), (72.0, 38.0))
MC_FIRST, MC_COUNT = 3, 10      # ten cards in the discard: one cut per frame from frame 3
MC_PICKS = (0, 1, 1, 0, 1, 0, 0, 1, 0, 1)


def blood_moon(c, g, t):
    gradient_bg(c, K6['blood0'], K6['blood2'], K6['blood1'], 0.6)
    moon = c.disc(66, 12, 8)
    c.put(c.disc(66, 12, 12), K6['blood2'])
    c.put(moon, K3['crim3'])
    c.put(c.disc(64, 10, 5), K3['crim4'])
    g.add(moon, LR['crimson'], 0.35)
    twinkling_stars(c, g, t, 6801, 8, (2, 2, 50, 20), light=LR['crimson'])
    hills = c.poly([(0, 44), (20, 38), (40, 42), (60, 36), (88, 40), (88, 64), (0, 64)])
    c.put(hills, K['roof'])
    c.put(hills & ~shift(hills, 0, 1), K6['blood2'])
    c.put(c.rect(0, 54, AW - 1, AH - 1), K['roof'])


def mil_cortes_frame(f):
    t = f / FRAMES
    c, g = Canvas(AW, AH), Canvas(AW, AH)
    blood_moon(c, g, t)
    hits = {0: -99, 1: -99}
    for n in range(MC_COUNT):
        if f >= MC_FIRST + n:
            hits[MC_PICKS[n]] = f - MC_FIRST - n
    for i, (sx, sy) in enumerate(MC_SIGILS):
        enemy_sigil(c, g, sx, sy, t, hits[i])
    # the discard pile: its cards flash one after the other, each a cut
    a = f - MC_FIRST
    for k, kind in enumerate(('skill', 'attack', 'power', 'attack', 'skill')):
        kind_card_rot(c, g, MC_PILE[0] + (k - 2) * 2, MC_PILE[1] - k * 2, (k - 2) * 0.12, kind, size=0.95)
    if 0 <= a < MC_COUNT:
        top = c.rect(int(MC_PILE[0]) - 5, int(MC_PILE[1]) - 15, int(MC_PILE[0]) + 5, int(MC_PILE[1]) - 2)
        c.put(outline_mask(top) & (((c.x + c.y + f) % 2) < 1), K3['crim5'])
        g.add(outline_mask(top), LR['crimson'], 0.6)
    # the cuts: thin slashes on the chosen enemy, the last few still fading; and a streak from the pile to it
    rng = np.random.default_rng(6803)
    angs = rng.uniform(-1.4, 1.4, MC_COUNT)
    for n in range(MC_COUNT):
        age = f - MC_FIRST - n
        if not 0 <= age <= 5:
            continue
        sx, sy = MC_SIGILS[MC_PICKS[n]]
        ox, oy = rng.uniform(-3, 3), rng.uniform(-3, 3)
        q = age / 5.0
        slash_line(c, g, sx + ox, sy + oy, angs[n], 9 * (1 - q * 0.4), 1.4 * (1 - q), RPAL if n % 3 else CPAL,
                   LR['crimson'] if n % 3 else L['cyan'], a=1 - q)
        if age == 0:
            m = c.line(MC_PILE[0] + 4, MC_PILE[1] - 12, sx + ox, sy + oy) & (((c.x + c.y) % 3) < 1)
            c.put(m, K3['crim4'])
            g.add(m, LR['crimson'], 0.4)
            star4(c, g, sx + ox, sy + oy, 3, core=K['white'], col=K3['crim5'], edge=K3['crim3'], light=LR['crimson'])
    if 0 <= a < MC_COUNT and a % 3 == 0:
        shake(c, g, 1 if a % 2 else -1, 0)
    sparks(c, g, f, MC_FIRST + MC_COUNT - 1, MC_SIGILS[0][0], MC_SIGILS[0][1], 6805, 8, (0.8, 2.0), 5, CRIM_SPARKS,
           LR['crimson'], gravity=0.12)
    sparks(c, g, f, MC_FIRST + MC_COUNT - 1, MC_SIGILS[1][0], MC_SIGILS[1][1], 6807, 8, (0.8, 2.0), 5, CRIM_SPARKS,
           LR['crimson'], gravity=0.12)
    # her dagger, in the middle of it all
    dagger(c, g, 36, round(30 + math.sin(t * math.pi * 4) * 0.8), -0.55, 0.6, t * 2,
           -2 + (f - 16) * 10.0 if 16 <= f <= 20 else None, rune_gain=1.8 if 0 <= a < MC_COUNT else 1.0)
    motes(c, g, t, 6809, 6, (4, 6, 84, 50), K3['crim4'], K3['crim5'], LR['crimson'], gain=0.4)
    vignette(c, 0.72)
    return c, g


# ---------------------------------------------------------------------------------------- Nada que Perder
NP_BRAZIER = (44.0, 46.0)
NP_HAND = ((24.0, 14.0), (36.0, 12.0), (52.0, 12.0), (64.0, 14.0))
NP_KINDS = ('skill', 'attack', 'power', 'attack')
NP_PICK = 2                     # the roulette stops on this card
NP_ROULETTE = (1, 4)
NP_THROW = 5                    # thrown into the fire
NP_BURN = 8
NP_RISE = (11, 13)              # roba 2


def ruined_camp(c, g, t):
    gradient_bg(c, K['night0'], K['night2'], K['night1'], 0.6)
    twinkling_stars(c, g, t, 6901, 12, (2, 2, 86, 24))
    for x, h in ((4, 26), (78, 30)):                                  # broken walls
        w = c.poly([(x - 4, 54), (x - 4, 54 - h), (x, 54 - h + 4), (x + 4, 54 - h + 1), (x + 6, 54)])
        c.put(outline_mask(w), K['ink'])
        c.put(w, K3['temple1'])
        c.put(w & (c.x < x - 2), K3['temple2'])
    c.put(c.rect(0, 54, AW - 1, AH - 1), K4['heath1'])
    c.put(c.rect(0, 54, AW - 1, 54), K4['heath2'])


def brazier(c, g, t, flare):
    x, y = NP_BRAZIER
    legs = c.line(x - 7, 56, x - 4, y + 4) | c.line(x + 7, 56, x + 4, y + 4) | c.line(x, 57, x, y + 4)
    c.put(legs, KR['iron1'])
    bowl = (((c.x - x) / 9.0) ** 2 + ((c.y - y) / 4.5) ** 2) <= 1
    bowl &= c.y >= y - 1
    c.put(outline_mask(bowl), K['ink'])
    c.put(bowl, KR['iron2'])
    c.put(bowl & (c.y < y + 1), KR['iron3'])
    for r, tone in ((16, hexc('#2a1e2c')), (10, hexc('#3a2430'))):      # its glow on the ground and the walls, in steps
        c.put(c.disc(x, y + 6, r) & (c.y > 54) & ~legs, tone)
    flame(c, g, x, y - 1, 10 + 6 * flare, 4 + 1.5 * flare, t, 0.3, gain=1.0 + 0.6 * flare)


def nada_que_perder_frame(f):
    t = f / FRAMES
    c, g = Canvas(AW, AH), Canvas(AW, AH)
    ruined_camp(c, g, t)
    power_ring(c, g, NP_BRAZIER[0], 58, 18, 3, t, 0.85, col=KR['spark1'], light=L['warm'])
    burn = f - NP_BURN
    flare = 1.0 if 0 <= burn <= 2 else 0.5 if burn == 3 else 0.0
    brazier(c, g, t, flare)
    # her hand: a roulette runs over it and picks one; that card is thrown in the fire
    r0, r1 = NP_ROULETTE
    for k, ((x, y), kind) in enumerate(zip(NP_HAND, NP_KINDS)):
        picked = k == NP_PICK
        if picked and NP_THROW <= f < 22:
            continue
        lit = (r0 <= f <= r1 and (f - r0) % 4 == k) or (picked and f == NP_THROW - 1)
        type_card(c, g, x, y - (1 if lit else 0), kind, glow=1.0 if lit else 0.0)
        if picked and f == 22:
            star4(c, g, x, y - 8, 2, core=K['white'], col=KR['spark2'], edge=KR['spark1'], light=L['warm'])
    a = f - NP_THROW
    if 0 <= a < NP_BURN - NP_THROW:                                   # it tumbles into the flames
        u = (a + 1) / (NP_BURN - NP_THROW)
        x, y = NP_HAND[NP_PICK]
        kind_card_rot(c, g, lerp(x, NP_BRAZIER[0], u), lerp(y, NP_BRAZIER[1] - 6, u * u), u * 2.5, NP_KINDS[NP_PICK],
                      size=0.95)
    if burn == 0:
        star4(c, g, NP_BRAZIER[0], NP_BRAZIER[1] - 8, 5, core=K['white'], col=KR['spark2'], edge=KR['spark1'], light=L['warm'],
              gain=1.3)
    sparks(c, g, f, NP_BURN, NP_BRAZIER[0], NP_BRAZIER[1] - 8, 6903, 14, (0.8, 2.0), 7, WARM_SPARKS6, L['warm'],
           gravity=-0.05, spread=(0.4, math.pi - 0.4))
    if 1 <= burn <= 6:                                                # smoke rises
        for j in range(3):
            r = 2 + burn * 0.8
            m = c.disc(NP_BRAZIER[0] + (j - 1) * 4, NP_BRAZIER[1] - 14 - burn * 2 - j, r)
            c.put(m, K6['storm2'], 0.8 - burn * 0.1)
            c.put(m & c.disc(NP_BRAZIER[0] + (j - 1) * 4 - 1, NP_BRAZIER[1] - 15 - burn * 2 - j, r * 0.6), K6['storm3'],
                  0.8 - burn * 0.1)
    for k, born in enumerate(NP_RISE):                                # roba 2: two cards out of the smoke
        rising_card(c, g, f, born, NP_BRAZIER[0] + (k * 2 - 1) * 9, NP_BRAZIER[1] - 16, NP_BRAZIER[1] - 36, life=7)
    motes(c, g, t, 6905, 7, (4, 6, 84, 50), KR['spark1'], K['violet3'], L['warm'], gain=0.4)
    vignette(c, 0.72)
    return c, g


# ---------------------------------------------------------------------------------------- Ritmo Letal
RL_PIVOT = (34.0, 47.0)         # the metronome's pendulum turns about this point
RL_TICKS = (3, 9, 15, 21)       # the beats; the first one of the turn makes the card cheaper
RL_CHIP = 4
RL_CARD = (66.0, 32.0)
RL_GEM = (61.0, 22.0)


def clock_tower(c, g, t):
    gradient_bg(c, K['night0'], K['teal2'], K['night1'], 0.6)
    face = c.disc(68, 14, 10)                                         # a great clock face behind
    c.put(face, K['teal2'])
    c.put(ellipse_ring(c, 68, 14, 10, 10, 0.6), K['teal3'])
    for k in range(12):
        a = k * math.pi / 6
        c.put(c.pixel(68 + math.cos(a) * 8, 14 + math.sin(a) * 8), K['moon0'])
    hand = c.line(68, 14, 68 + math.cos(t * math.pi * 2 - math.pi / 2) * 7, 14 + math.sin(t * math.pi * 2 - math.pi / 2) * 7)
    c.put(hand, K['moon1'])
    for x in range(0, AW, 16):                                        # gears turning in the shadows
        ring = ellipse_ring(c, x, 58, 7, 7, 0.8)
        ang = np.arctan2(c.y - 58, c.x - x)
        c.put(ring & (((ang / (2 * math.pi) * 10 + t * (1 if x % 32 else -1)) % 1) < 0.5), KR['iron1'])
    c.put(c.rect(0, 55, AW - 1, AH - 1) & (c.y > 60), KR['iron0'])
    desk = c.rect(0, 48, AW - 1, 52)
    c.put(desk, KR['wood1'])
    c.put(c.rect(0, 48, AW - 1, 48), KR['wood2'])


def metronome(c, g, swing, tick):
    """A wooden metronome: a tall pyramid with a brass pendulum that swings (swing = its angle)."""
    x, y = RL_PIVOT
    body = c.poly([(x - 9, 48), (x + 9, 48), (x + 4, 18), (x - 4, 18)])
    c.put(outline_mask(body), K['ink'])
    c.put(body, KR['wood1'])
    c.put(body & (c.x < x - 3), KR['wood2'])
    front = c.poly([(x - 5, 46), (x + 5, 46), (x + 2.5, 22), (x - 2.5, 22)])
    c.put(front, KR['wood0'])
    for k in range(6):                                                # the scale behind the pendulum
        yy = 26 + k * 3
        c.put(c.rect(int(x) - 1, yy, int(x) + 1, yy) & front, KR['gold1'])
    ux, uy = math.sin(swing), -math.cos(swing)
    rod = c.line(x, y - 1, x + ux * 28, y - 1 + uy * 28)
    c.put(rod, KR['gold2'] if not tick else KR['gold3'])
    bob = c.rect(int(x + ux * 18) - 2, int(y - 1 + uy * 18) - 1, int(x + ux * 18) + 2, int(y - 1 + uy * 18) + 1)
    c.put(outline_mask(bob), K['ink'])
    c.put(bob, KR['gold2'])
    if tick:
        g.add(rod | bob, GOLD, 0.6)
    return x + ux * 28, y - 1 + uy * 28


def ritmo_letal_frame(f):
    t = f / FRAMES
    c, g = Canvas(AW, AH), Canvas(AW, AH)
    clock_tower(c, g, t)
    power_ring(c, g, RL_PIVOT[0], 50, 14, 2.5, t, 0.85, col=KB['b2'], light=LB)
    # the pendulum: two full swings a loop, a beat at each end
    swing = 0.42 * math.cos((t - RL_TICKS[0] / FRAMES) * math.pi * 4)
    tick = any(abs(f - k) == 0 for k in RL_TICKS)
    tip = metronome(c, g, swing, tick)
    if tick:
        for k, rr in enumerate((4, 7)):                               # the beat: rings from the tip
            ring = ellipse_ring(c, tip[0], tip[1], rr, rr, 0.5)
            c.put(ring & (((c.x + c.y) % 3) < 2), KR['gold3'] if f == RL_TICKS[0] else KR['gold1'])
            g.add(ring, GOLD, 0.5 if f == RL_TICKS[0] else 0.2)
    # the card: the first beat of the turn chips one mana off its gem (1 less); a new turn refills it at the end
    big = (f < RL_CHIP) or f >= 22
    big_card(c, g, RL_CARD[0], RL_CARD[1], size=1.6)
    left = 2 if big else 1
    mana_drop(c, g, RL_GEM[0], RL_GEM[1], 1.15 if left == 2 else 0.9, gain=1.0 + (0.8 if f == RL_CHIP else 0.0))
    for k in range(left):
        c.put(c.pixel(RL_GEM[0] - 0.5 + (k - (left - 1) / 2) * 2, RL_GEM[1] + 0.5), K['white'])
    a = f - RL_CHIP
    if 0 <= a <= 5:
        u = a / 5.0
        mana_drop(c, g, round(RL_GEM[0] + 4 + 4 * u), round(RL_GEM[1] - 3 - 10 * ease_out(u)), 0.7 * (1 - 0.5 * u), gain=1 - u)
        minus = c.rect(int(RL_GEM[0]) - 7, int(RL_GEM[1]) - 1, int(RL_GEM[0]) - 5, int(RL_GEM[1]) - 1)
        c.put(minus, KB['b3'], 1 - 0.6 * u)
        g.add(minus, LB, 0.8 * (1 - u))
        line = c.line(tip[0], tip[1], RL_GEM[0], RL_GEM[1]) & (((c.x + c.y) % 3) < 1)
        if a <= 1:
            c.put(line, KR['gold3'])
            g.add(line, GOLD, 0.4)
    if f == RL_CHIP:
        star4(c, g, RL_GEM[0], RL_GEM[1], 5, core=K['white'], col=KR['mana3'], edge=KR['mana2'], light=LR['mana'], gain=1.3)
    if 18 <= f <= 21:
        converge(c, g, f, RL_GEM[0], RL_GEM[1], 18, 4, 7001, 6, 8, (KR['mana3'], KR['mana2']), LR['mana'])
    motes(c, g, t, 7003, 6, (4, 6, 84, 46), KB['b2'], KR['gold2'], LB, gain=0.35)
    vignette(c, 0.72)
    return c, g


# ---------------------------------------------------------------------------------------- Tormenta de Acero
TA_SIGILS = ((22.0, 48.0), (44.0, 46.0), (66.0, 48.0))
TA_RAINS = ((3, False), (13, True))   # (frame the rain starts, Singular = violet and heavier)
TA_FALL = 4


def tormenta_de_acero_frame(f):
    t = f / FRAMES
    c, g = Canvas(AW, AH), Canvas(AW, AH)
    flash = any(f == r for r, _ in TA_RAINS)
    storm_field(c, g, t, flash)
    power_ring(c, g, 44, 57, 30, 3, t, 0.75)
    hit = max([f - r - TA_FALL for r, _ in TA_RAINS if f >= r + TA_FALL], default=-99)
    for (sx, sy) in TA_SIGILS:
        ground_sigil(c, g, sx, sy, t, hit)
    rng = np.random.default_rng(7101)
    for r, singular in TA_RAINS:
        a = f - r
        n = 4 if singular else 3
        s = 0.5 if singular else 0.42
        for j, (sx, sy) in enumerate(TA_SIGILS):
            for k in range(n):
                ox = rng.uniform(-5, 5)
                delay = rng.uniform(0, 0.4)
                u = (a + 1) / TA_FALL - delay
                if not 0 < u < 1:
                    continue
                ang = math.pi / 2 + 0.35
                hx, hy = sx + ox + (1 - u) * 14, lerp(2, sy - 1, u)
                gx, gy = hx - math.cos(ang) * 40 * s, hy - math.sin(ang) * 40 * s
                if singular:
                    lab = dagger(c, g, gx, gy, ang, s, t * 2, rune_gain=1.5)
                    fl = lab == 5
                    c.put(fl, K['violet3'])
                    g.add(fl, L['violet'], 1.0)
                else:
                    dagger(c, g, gx, gy, ang, s, t * 2, rune_gain=1.3)
        h = f - r - TA_FALL
        for j, (sx, sy) in enumerate(TA_SIGILS):
            if 0 <= h <= 1:
                star4(c, g, sx, sy - 2, (5 if singular else 4) if h == 0 else 2, gain=1.3,
                      col=K['violet4'] if singular else K['cyan3'], edge=K['violet3'] if singular else K['cyan2'],
                      light=L['violet'] if singular else L['cyan'])
            sparks(c, g, f, r + TA_FALL, sx, sy - 1, 7103 + r * 10 + j, 6, (0.7, 1.8), 5,
                   VIOLET_SPARKS if singular else CYAN_SPARKS, L['violet'] if singular else L['cyan'], gravity=0.2,
                   spread=(0.3, math.pi - 0.3))
        if singular and h >= 1:
            plus_marks(c, g, TA_SIGILS[2][0] + 6, TA_SIGILS[2][1] - 10, h - 1, count=2, col=K['violet4'], light=L['violet'])
    if flash:
        for bx in (20, 62):
            bolt = c.line(bx, 0, bx - 3, 6) | c.line(bx - 3, 6, bx + 1, 9) | c.line(bx + 1, 9, bx - 2, 14)
            c.put(bolt, K['white'])
            g.add(dilate(bolt), L['violet'], 0.6)
    if hit == 0:
        shake(c, g, 0, 1)
    motes(c, g, t, 7105, 6, (4, 6, 84, 50), K['cyan3'], K['violet3'], L['cyan'], gain=0.4)
    vignette(c, 0.72)
    return c, g


# ---------------------------------------------------------------------------------------- Tormenta de Veneno
TV_SIGILS = ((20.0, 50.0), (44.0, 48.0), (68.0, 50.0))
TV_GATHER = (1, 5)
TV_POUR = (6, 15)
TV_SOAK = 9                     # the sigils turn green from here


def swamp_night(c, g, t):
    gradient_bg(c, K['night0'], K['teal2'], K['night1'], 0.6)
    for x, h in ((6, 26), (16, 18), (74, 24), (84, 30)):              # dead trees
        tr = c.rect(x, 54 - h, x + 1, 54) | c.line(x, 54 - h + 6, x - 4, 54 - h + 1) | c.line(x + 1, 54 - h + 9, x + 5, 54 - h + 4)
        c.put(tr, K['roof'])
    c.put(c.rect(0, 54, AW - 1, AH - 1), K2_POIS0)
    c.put(c.rect(0, 54, AW - 1, 54), K2_POIS1)
    for i, y in enumerate((57, 61)):
        off = (t * 44 * (1 if i else -1)) % 16
        for x in np.arange(-16 + off, AW, 16):
            c.put(c.rect(int(x), y, int(x) + 5, y), K2_POIS1)


def tormenta_de_veneno_frame(f):
    t = f / FRAMES
    c, g = Canvas(AW, AH), Canvas(AW, AH)
    swamp_night(c, g, t)
    soak = 0.0 if f < TV_SOAK else 1.0 if f <= 18 else max(0.0, 1 - (f - 18) / 5.0)
    for i, (sx, sy) in enumerate(TV_SIGILS):                         # the enemies: crimson, then poisoned green
        ring = ellipse_ring(c, sx, sy, 7, 2.2, 0.55)
        ang = np.arctan2((c.y - sy) / 2.2, (c.x - sx) / 7)
        ring &= ((ang / (2 * math.pi) * 10 + t * 2) % 1) < 0.7
        c.put(ring, K3['crim3'] if soak < 0.5 else K2_POIS3)
        g.add(ring, LR['crimson'] if soak < 0.5 else L2_POISON, 0.6)
        eye = c.rect(int(sx) - 1, int(sy) - 7, int(sx) + 1, int(sy) - 6)
        c.put(eye, K3['crim5'] if soak < 0.5 else K2_POIS4)
        g.add(eye, LR['crimson'] if soak < 0.5 else L2_POISON, 0.8)
        if soak > 0:                                                  # bubbles rising off them
            for b in range(3):
                u = ((t * 3 + b / 3 + i * 0.21) % 1)
                bx, by = sx + (b - 1) * 4, sy - 2 - u * 12
                m = c.disc(bx, by, 0.9 if u < 0.6 else 0.6)
                c.put(m, K2_POIS3, soak)
                g.add(m, L2_POISON, 0.5 * soak * (1 - u))
    # the cloud: gathers, pours, thins out
    g0, g1 = TV_GATHER
    size = 0.0 if f < g0 else min(1.0, (f - g0 + 1) / (g1 - g0 + 1)) if f <= TV_POUR[1] else max(0.0, 1 - (f - TV_POUR[1]) / 6.0)
    if size > 0:
        rng = np.random.default_rng(7201)
        for k in range(9):
            x = 8 + k * 9 + rng.uniform(-2, 2) + math.sin((t * 2 + k * 0.3) * math.pi * 2)
            y = 10 + rng.uniform(-3, 3)
            r = (4 + rng.uniform(0, 3)) * size
            m = c.disc(x, y, r)
            c.put(m, K2_POIS1)
            c.put(m & c.disc(x - r * 0.3, y - r * 0.3, r * 0.7), K2_POIS2)
            c.put(m & c.disc(x - r * 0.4, y - r * 0.5, r * 0.3), K2_POIS3)
            g.add(m & c.disc(x - r * 0.4, y - r * 0.5, r * 0.3), L2_POISON, 0.25)
        c.put(c.rect(4, 10, 84, 12) & (c.B < 0.0), K2_POIS1)
    p0, p1 = TV_POUR
    if p0 <= f <= p1:                                                # green rain (5 de Veneno a todos)
        rng = np.random.default_rng(7203)
        for k in range(26):
            x0 = rng.uniform(6, 82)
            ph = rng.random()
            u = ((f - p0) / 3.0 + ph) % 1
            y = 14 + u * 40
            x = x0 - u * 3
            m = c.line(x, y, x + 1, y - 3)
            c.put(m, K2_POIS3 if k % 3 else K2_POIS4)
            g.add(m, L2_POISON, 0.4)
    for i, (sx, sy) in enumerate(TV_SIGILS):
        if f == TV_SOAK:
            star4(c, g, sx, sy - 2, 4, core=K['white'], col=K2_POIS4, edge=K2_POIS3, light=L2_POISON)
        sparks(c, g, f, TV_SOAK, sx, sy - 1, 7205 + i, 6, (0.6, 1.4), 5, POISON_SPARKS, L2_POISON, gravity=0.1,
               spread=(0.3, math.pi - 0.3))
    motes(c, g, t, 7207, 7, (4, 6, 84, 50), K2_POIS3, K['cyan3'], L2_POISON, gain=0.4)
    vignette(c, 0.72)
    return c, g


ILLUSTRATIONS = {
    'rapina': rapina_frame,
    'sombra_gemela': sombra_gemela_frame,
    'asesina': asesina_frame,
    'cadena_perfecta': cadena_perfecta_frame,
    'lluvia_de_golpes': lluvia_de_golpes_frame,
    'mazo_impecable': mazo_impecable_frame,
    'mil_cortes': mil_cortes_frame,
    'nada_que_perder': nada_que_perder_frame,
    'ritmo_letal': ritmo_letal_frame,
    'tormenta_de_acero': tormenta_de_acero_frame,
    'tormenta_de_veneno': tormenta_de_veneno_frame,
}


def render(card_id):
    fn = ILLUSTRATIONS[card_id]
    base, glow = [], []
    for f in range(FRAMES):
        b, gl = fn(f)
        base.append(b)
        glow.append(gl)
    return base, glow
