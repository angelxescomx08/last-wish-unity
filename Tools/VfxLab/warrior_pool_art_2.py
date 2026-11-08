"""Animated pixel-art illustrations for the warrior's uncommon loot cards (88x64, 24 frames at 12 fps, seamless).

Same method as warrior_pool_art.py (its shared vocabulary: her blood, Furia, Aguante, Fuerza, healing, block) and
docs/art-style.md. No figure: her gear on its own, now with the rest of her kit drawn as single silhouettes: her war
horn, her greataxe, her gauntlet, her closed helm (T visor and crimson plume, on a post: it is empty) and a gold cup.

  fortaleza_interior  Fortaleza Interior (poder: cada vez que te curas, 2 de escudo): her life-heart in the window of a
                      little keep on a rune circle; green healing falls into it and the battlements raise a blue ward.
  grito_batalla       Grito de Batalla (roba 2; Aguante: +1 Fuerza): her horn sounds from the ramparts, two cards fly
                      out of the sound; her shield's gold pulse turns into crimson chevrons.
  grito_guerra        Grito de Guerra (+1 de maná por enemigo vivo): her banner's war cry reaches three enemy sigils and
                      each sends a mana crystal back to it.
  hachazo_temerario   Hachazo Temerario (24; pierdes 5 de vida): the raised greataxe drips her blood, then splits an
                      enemy totem in two; a new one rises.
  puno_hierro         Puño de Hierro (6; +1 Fuerza): her gauntlet punches a sigil-marked block out of a wall; crimson
                      chevrons rise from the knuckles.
  respiro             Respiro (8 de escudo; te curas 7): a camp at night; the fire's embers turn green and fill her
                      heart, the shield leaning on the planted sword raises a ward.
  rugido              Rugido (+1 Fuerza; Furia: 2 más): her helm on a post in an arena; her blood on it, the visor
                      flares crimson and roars, three chevrons.
  sangre_hirviente    Sangre Hirviente (poder: al empezar tu turno pierdes 2 de vida y +1 Fuerza): a gold cup of her
                      blood on a rune circle; the hourglass turns, it boils over and a chevron rises from the steam.
  sangria             Sangría (pierdes 4 de vida; +3 Fuerza): her blood runs down an altar channel into the planted
                      sword, the fuller fills crimson from the point up, three chevrons.
  sed_batalla         Sed de Batalla (10; te curas el daño hecho): in a graveyard the sword runs an enemy sigil
                      through and its life flows back along the blade into her heart.
"""
import math

import numpy as np

from pixelkit import Canvas, hexc, dilate, erode, shift
from rogue_art import (AW, AH, FRAMES, K, L, ease_out, ease_in_out, frac, gradient_bg, vignette, star4, motes,
                       outline_mask, crescent)
from mage_art import twinkling_stars, sparks, ellipse_ring, flame
from warrior_art import (KW, LW, shake, falling_sparks, dust_puffs, rising_chevrons, blade_point, WARM_SPARKS,
                         CYAN_SPARKS)
from rogue_pool_art import KB, LB, GOLD_SPARKS, mana_drop, speed_lines, converge, diamond_ring, sigil
from rogue_pool_art_3 import plus_marks, rising_card, ward_rings
from rogue_pool_art_4 import hourglass
from rogue_pool_art_5 import enemy_sigil, power_ring
from warrior_pool_art import (KP, LP, BLOOD_SPARKS, HEAL_SPARKS, blood_drop, drip, splash, life_heart, heal_crosses,
                              blood_aura, gold_pulse, faint_ward, foe_sigil, sword, shield, shield_gem, contact_shadow,
                              floor_slabs)

KP2 = dict(KP)
KP2.update({
    'horn0': hexc('#3a2c2a'), 'horn1': hexc('#7a6658'), 'horn2': hexc('#b8a48c'), 'horn3': hexc('#e6d8bc'),
    'totem0': hexc('#120a10'), 'totem1': hexc('#241418'), 'totem2': hexc('#3a2420'), 'totem3': hexc('#563628'),
    'ember0': hexc('#5a1a0c'), 'ember1': hexc('#c0461a'), 'ember2': hexc('#ff9a3c'),
    'grass0': hexc('#0a1410'), 'grass1': hexc('#12241a'), 'grass2': hexc('#1c3826'),
})
CRIM_SPARKS = (KW['crim5'], KW['crim4'], KW['crim2'])


def sage(f, born):
    return (f - born + 12) % FRAMES - 12


def fury_star(c, g, x, y, arm, gain=1.3):
    star4(c, g, x, y, arm, core=K['white'], col=KW['crim5'], edge=KW['crim4'], light=LW['crimson'], gain=gain)


def gold_star(c, g, x, y, arm, gain=1.2):
    star4(c, g, x, y, arm, core=K['white'], col=KW['gold3'], edge=KW['gold2'], light=LW['gold'], gain=gain)


def heal_star(c, g, x, y, arm, gain=1.2):
    star4(c, g, x, y, arm, core=K['white'], col=KP['heal3'], edge=KP['heal2'], light=LP['heal'], gain=gain)


def tube_samples(p0, p1, p2, w0, w1, n=48, power=1.8):
    pts = []
    for i in range(n + 1):
        u = i / n
        x = (1 - u) ** 2 * p0[0] + 2 * (1 - u) * u * p1[0] + u * u * p2[0]
        y = (1 - u) ** 2 * p0[1] + 2 * (1 - u) * u * p1[1] + u * u * p2[1]
        tx = 2 * (1 - u) * (p1[0] - p0[0]) + 2 * u * (p2[0] - p1[0])
        ty = 2 * (1 - u) * (p1[1] - p0[1]) + 2 * u * (p2[1] - p1[1])
        ln = math.hypot(tx, ty) or 1
        pts.append((x, y, -ty / ln, tx / ln, w0 + (w1 - w0) * u ** power, u))
    return pts


# ---------------------------------------------------------------------------------------- her kit
def war_horn(c, g, p0, p1, p2, flare=0.0, a=1.0):
    """Her war horn: a curved bone horn from the mouthpiece p0 to the bell p2 (control point p1), gold bands, a
    crimson cord; flare 0..1 lights the bell when it sounds. Returns the bell centre."""
    pts = tube_samples(p0, p1, p2, 1.1, 4.6)
    best = np.full((AH, AW), 1e9)
    uu = np.zeros((AH, AW))
    vv = np.zeros((AH, AW))
    ww = np.ones((AH, AW))
    for x, y, nx, ny, w, u in pts:
        d = np.hypot(c.x - x, c.y - y) - w
        better = d < best
        best = np.where(better, d, best)
        uu = np.where(better, u, uu)
        vv = np.where(better, (c.x - x) * nx + (c.y - y) * ny, vv)
        ww = np.where(better, w, ww)
    m = best <= 0
    c.put(outline_mask(m), K['ink'], a)
    rel = vv / np.maximum(ww, 0.5)
    c.put(m, KP2['horn1'], a)
    c.put(m & (rel < 0.25), KP2['horn2'], a)
    c.put(m & (rel < -0.45), KP2['horn3'], a)
    c.put(m & (rel > 0.65), KP2['horn0'], a)
    for ub in (0.18, 0.5, 0.78):
        band = m & (np.abs(uu - ub) < 0.035)
        c.put(band, KW['gold2'], a)
        c.put(band & (rel < -0.2), KW['gold3'], a)
    rim = m & (uu > 0.95)
    c.put(rim, KW['gold2'], a)
    c.put(rim & (rel < 0), KW['gold3'], a)
    bx, by, nx, ny, w, _ = pts[-1]
    tx, ty = ny, -nx
    mouth = (np.abs((c.x - bx) * nx + (c.y - by) * ny) <= w - 0.8) & (np.abs((c.x - bx) * tx + (c.y - by) * ty) <= 0.9)
    c.put(mouth, K['ink'], a)
    if flare > 0:
        c.put(mouth, KW['gold3'], flare)
        g.add(dilate(mouth), LW['gold'], 0.9 * flare)
    mx, my = pts[int(0.5 * 48)][:2]                                     # the cord hanging from the middle band
    cord = c.line(pts[9][0], pts[9][1] + 1, mx - 2, my + 6) | c.line(mx - 2, my + 6, pts[37][0], pts[37][1] + 1)
    c.put(cord & ~m, KW['crim3'], a)
    c.put(c.pixel(mx - 2, my + 6) | c.pixel(mx - 2, my + 7), KW['crim4'], a)
    return bx, by


def sound_rings(c, g, x, y, age, life=7, ang=0.0, spread=0.75, col=None, light=None, gain=1.0, step=4.0):
    """Arcs of sound spreading from (x, y) towards `ang` (screen radians)."""
    col = KW['gold3'] if col is None else col
    light = LW['gold'] if light is None else light
    if not 0 <= age <= life:
        return
    dx, dy = c.x - x, c.y - y
    th = np.arctan2(dy, dx) - ang
    th = (th + math.pi) % (2 * math.pi) - math.pi
    cone = np.abs(th) <= spread
    r0 = np.hypot(dx, dy)
    for j in range(3):
        r = 3 + (age - j * 1.5) * step
        if r < 3:
            continue
        k = 1 - (age - j * 1.5) / (life + 1)
        if k <= 0:
            continue
        ring = cone & (np.abs(r0 - r) <= 0.6)
        c.put(ring, col, min(1.0, k * 1.3))
        g.add(ring, light, 0.7 * k * gain)


def greataxe(c, g, hx, hy, ang, t, s=1.0, fury=0.0, cut=None):
    """Her greataxe held at (hx, hy) (the hands on the haft), the head along `ang`, its edge on the left of the
    direction of the haft (towards a downward swing). Returns the centre of the edge."""
    ux, uy = math.cos(ang), math.sin(ang)
    vx, vy = uy, -ux                                                   # edge side
    dx, dy = c.x - hx, c.y - hy
    u = (dx * ux + dy * uy) / s
    v = (dx * vx + dy * vy) / s
    haft = (u >= -10) & (u <= 32) & (np.abs(v) <= 1.3)
    wrap = haft & (u >= -6) & (u <= 4) & ((np.floor(u * 0.8) % 2) == 0)
    butt = (np.hypot(u + 11.5, v) <= 1.9)
    uc = 27.0
    du = u - uc
    blade = (v >= 0.5) & (v <= 11.5 - (du / 6.5) ** 2 * 2.5) & (np.abs(du) <= 3.0 + v * 0.42)
    socket = (np.abs(du) <= 3.0) & (np.abs(v) <= 2.6)
    spike = (v <= -1.5) & (v >= -6.5) & (np.abs(du) <= 1.6 * (1 + (v + 1.5) / 5.0))
    head = blade | socket | spike
    m = haft | butt | head
    if cut is not None:
        m &= cut
    c.put(outline_mask(m), K['ink'])
    c.put(haft & m, KP['oak2'])
    c.put(haft & m & (v < -0.3), KP['oak3'])
    c.put(wrap & m, KW['crim3'])
    c.put(wrap & m & (v < 0), KW['crim4'])
    c.put(butt & m, KW['gold2'])
    c.put(head & m, K['steel1'])
    edge_v = 11.5 - (du / 6.5) ** 2 * 2.5
    c.put(blade & m & (du < 0), K['steel2'])
    c.put(blade & m & (v > edge_v - 2.2), K['steel3'])
    c.put(blade & m & (v > edge_v - 1.0), K['steel4'])
    c.put(blade & m & (du > 2.2), K['steel0'])
    c.put(socket & m, KW['iron2'])
    c.put(head & m & (np.abs(v - 1.6) < 0.6) & ~socket, KW['gold2'])
    rune = blade & m & (np.abs(du) / 1.5 + np.abs(v - 6) / 2.2 <= 1)
    c.put(rune, KW['crim5'] if fury > 0.5 else K['cyan2'])
    g.add(rune, LW['crimson'] if fury > 0.5 else L['cyan'], 0.8)
    return hx + ux * uc * s + vx * 11 * s, hy + uy * uc * s + vy * 11 * s


def gauntlet(c, g, x, y, fury=0.0, flash=0.0, rune=1.0, s=1.0):
    """Her armoured gauntlet on its own, seen from the side, punching to the right; (x, y) = the knuckles' middle."""
    dx, dy = (c.x - x) / s, (c.y - y) / s
    fist = (dx >= -7) & (dx <= 1) & (dy >= -5) & (dy <= 4)
    fist |= (np.hypot(dx - 0.5, dy + 0.5) <= 4.6) & (dx >= 0)
    back = (dx >= -9) & (dx <= -3) & (dy >= -6) & (dy <= 2.5)
    thumb = (dx >= -6) & (dx <= 0) & (dy >= 2) & (dy <= 5.5)
    cuff = (dx >= -18) & (dx <= -8) & (np.abs(dy + 0.5) <= 5.5 + (-8 - dx) * 0.15)
    m = fist | back | thumb | cuff
    c.put(outline_mask(m), K['ink'])
    c.put(cuff, K['steel1'])
    c.put(cuff & (dy < -2), K['steel2'])
    c.put(cuff & (dy < -4), K['steel3'])
    c.put(cuff & (np.abs(dx + 8.5) < 0.8), KW['gold2'])
    c.put(cuff & (np.abs(dx + 17.5) < 0.8), KW['gold1'])
    c.put(cuff & (dx < -16.5) & (np.abs(dy + 0.5) < 3.5), KW['grip0'])            # the hollow inside the cuff
    hand = (fist | back | thumb) & ~cuff
    c.put(hand, K['steel1'])
    c.put(hand & (dy < -1), K['steel2'])
    c.put(hand & (dy < -3.5), K['steel3'])
    c.put(hand & (dy > 3), K['steel0'])
    for fy in (-2.5, 0.0, 2.5):                                        # finger segments curled round
        c.put(fist & (np.abs(dy - fy) < 0.5) & (dx > -2), K['steel0'])
    knuck = fist & (dx > 1) & (dx < 4.6) & (dy < -3)
    c.put(knuck, K['steel4'])
    r = back & (np.abs(dx + 6) / 1.4 + np.abs(dy + 2.5) / 2.0 <= 1)
    c.put(r, KW['crim5'] if fury > 0.5 else K['cyan2'])
    g.add(r, LW['crimson'] if fury > 0.5 else L['cyan'], 0.8 * rune)
    if flash > 0:
        c.put(fist & (dx > -1), K['white'], flash)
        g.add(fist & (dx > -1), L['white'], 0.6 * flash)
    return m


def helm(c, g, x, y, glow=0.0, fury=0.0, a=1.0, plume=0.0, t=0.0, s=1.0):
    """Her closed helm seen from the front: dome with a ridge, T visor, gold brow band with a cyan gem, cheek plates,
    crimson plume; glow 0..1 lights the visor slit (cyan, crimson with Furia)."""
    dx, dy = (c.x - x) / s, (c.y - y) / s
    dome = (np.hypot(dx / 8.0, (dy + 2) / 9.0) <= 1) & (dy <= 2)
    cheeks = (dy > 2) & (dy <= 9) & (np.abs(dx) <= 8 - (dy - 2) * 0.45)
    m = dome | cheeks
    slit = ((np.abs(dy + 0.5) <= 0.6) & (np.abs(dx) <= 5.5)) | ((np.abs(dx) <= 0.6) & (dy >= -0.5) & (dy <= 6.5))
    band = m & (np.abs(dy + 4.5) <= 0.8)
    gem = (np.abs(dx) / 1.6 + np.abs(dy + 4.5) / 1.6) <= 1
    c.put(outline_mask(m), K['ink'], a)
    c.put(m, K['steel1'], a)
    c.put(m & (dx + dy * 0.6 < -2), K['steel2'], a)
    c.put(m & (dx + dy * 0.6 < -6), K['steel3'], a)
    c.put(m & (dx > 4), K['steel0'], a)
    c.put(m & (np.abs(dx) < 0.7) & (dy < -5.5), K['steel4'], a)
    c.put(band, KW['gold2'], a)
    c.put(band & (dx < 0), KW['gold3'], a)
    c.put(gem, K['cyan2'], a)
    c.put(gem & (dx + dy + 4.5 < 0), K['cyan4'], a)
    g.add(gem, L['cyan'], 0.7 * a)
    for rx, ry in ((-6, 4), (6, 4), (-6.5, -1.5), (6.5, -1.5)):
        c.put(c.pixel(x + rx * s, y + ry * s) & m & ~slit, KW['gold1'], a)
    c.put(slit, K['ink'], a)
    if glow > 0:
        col = KW['crim5'] if fury > 0.5 else K['cyan3']
        light = LW['crimson'] if fury > 0.5 else L['cyan']
        c.put(slit, col, min(1.0, glow * 1.4) * a)
        g.add(slit, light, 1.2 * glow * a)
        g.add(dilate(slit) & ~slit, light, 0.35 * glow * a)
    # the plume: from the crown back and down to the left, waving
    pl = np.zeros((AH, AW), bool)
    for i in range(16):
        u = i / 15
        px = x - (1 + u * 13 + plume * u * 3) * s
        py = y + (-11 + u * u * 10 + math.sin(u * 5 - t * 2 * math.pi * 2) * u * 1.4) * s
        pl |= c.disc(px, py, (2.4 - u * 1.3) * s)
    pl &= ~m
    c.put(outline_mask(pl) & ~m, K['ink'], a)
    c.put(pl, KW['crim3'], a)
    c.put(pl & ~shift(pl, 0, 1), KW['crim4'], a)
    c.put(pl & ~shift(pl, 0, -1), KW['crim2'], a)
    return m


def chalice(c, g, x, y, fill=0.8, boil=0.0, t=0.0, tilt=0.0):
    """A gold cup of her blood on a short stem; (x, y) = the middle of its rim. boil 0..1 makes it bubble."""
    dx, dy = c.x - x, c.y - y
    bowl = (dy >= 0) & (dy <= 8) & (np.abs(dx) <= 7.5 - (dy / 8.0) ** 2 * 5.0)
    rim = np.hypot(dx / 7.5, dy / 1.8) <= 1
    stem = (np.abs(dx) <= 1.2) & (dy > 8) & (dy <= 13)
    knot = np.hypot(dx, dy - 10.5) <= 1.8
    foot = (dy > 13) & (dy <= 15) & (np.abs(dx) <= 5.0 - (15 - dy))
    m = bowl | rim | stem | knot | foot
    c.put(outline_mask(m), K['ink'])
    c.put(m, KW['gold1'])
    c.put(m & (dx < 1), KW['gold2'])
    c.put(m & (dx < -4), KW['gold3'])
    c.put(m & (dx > 5), KW['gold0'])
    c.put(bowl & (np.abs(dy - 4) < 0.6) & (np.abs(dx) < 6), KW['gold0'])
    c.put(bowl & (np.abs(dx + 0.5) / 1.6 + np.abs(dy - 5) / 1.4 <= 1), KW['crim4'])     # a garnet set in it
    surf = np.hypot(dx / 6.4, dy / 1.2) <= 1
    c.put(surf, KW['crim2'])
    c.put(surf & (dx < -1), KW['crim3'])
    g.add(surf, LW['crimson'], 0.25 + 0.4 * boil)
    if boil > 0:
        rng = np.random.default_rng(907)
        for i in range(6):
            ph = rng.random()
            u = frac(t * 4 + ph)
            bx = x + rng.uniform(-5, 5)
            r = 0.6 + u * 1.2 * boil
            b = c.disc(bx, y - 0.5 - u * 1.2 * boil, r) & ~c.disc(bx, y - 0.5 - u * 1.2 * boil, r - 0.7)
            c.put(b, KW['crim4'], boil)
            g.add(b, LW['crimson'], 0.4 * boil)
    return m


# ---------------------------------------------------------------------------------------- Fortaleza Interior
FI_KEEP = (44.0, 34.0)
FI_FLOOR = 54


def sanctum_hill(c, g, t):
    """A hill at night with a soft green aurora, the round keep standing on a rune circle."""
    gradient_bg(c, K['night0'], K['teal1'], K['night1'], 0.6)
    for i, y in enumerate((9, 14)):                                    # a faint green aurora breathing
        wav = np.sin((c.x / 14.0 + t * 2 * (1 if i else -1)) * math.pi) * 2.5
        band = np.abs(c.y - y - wav) < 0.9 - i * 0.3
        c.put(band, KP['heal0'] if i else KP['heal1'])
        g.add(band, LP['heal'], 0.04)
    twinkling_stars(c, g, t, 951, 16, (2, 2, 86, 32), avoid=(44, 24, 18))
    hill = c.poly([(0, 50), (16, 46), (32, 47), (44, 45), (60, 47), (74, 45), (88, 48), (88, 64), (0, 64)])
    c.put(hill, KP2['grass1'])
    c.put(hill & ~shift(hill, 0, 1), KP2['grass2'])
    c.put(c.rect(0, FI_FLOOR, AW - 1, AH - 1), KP2['grass0'])
    for x in (6, 15, 29, 63, 77, 84):
        c.put(c.line(x, FI_FLOOR + 2, x + 1, FI_FLOOR), KP2['grass2'])


def keep(c, g, x, y, lit=0.0, flash=0.0):
    """A small round stone keep with battlements and a tall arched window in the middle (the heart sits in it)."""
    dx, dy = c.x - x, c.y - y
    body = (np.abs(dx) <= 12) & (dy >= -11) & (dy <= FI_FLOOR - y)
    crown = (np.abs(dx) <= 14) & (dy >= -15) & (dy < -11)
    merlons = crown & (dy < -13) & ((np.floor((dx + 14) / 3.5) % 2) == 1)
    crown &= ~merlons
    m = body | crown
    win = (np.abs(dx) <= 6) & (dy >= -4) & (dy <= 9) | (np.hypot(dx, dy + 4) <= 6) & (dy < -4)
    c.put(outline_mask(m), K['ink'])
    c.put(m, KW['stone3'])
    c.put(m & (dx < -6), K['steel0'])
    c.put(m & (dx > 6), KW['stone2'])
    c.put(m & (dx > 10), KW['stone1'])
    for row, yy in enumerate(range(int(y) - 10, FI_FLOOR, 4)):
        c.put(body & (np.abs(c.y - yy) < 0.5), KW['stone1'])
        off = 0 if row % 2 else 3
        c.put(body & (np.abs(c.y - yy - 2) < 2) & ((np.floor(dx + 12 + off) % 6) == 0), KW['stone1'])
    c.put(crown & (np.abs(dy + 11.5) < 0.6), KW['stone1'])
    c.put(dilate(win) & ~win & m, KW['stone3'])
    c.put(win, KW['stone0'])
    c.put(win, KP['heal0'], 0.6 * lit)
    g.add(win, LP['heal'], 0.12 * lit)
    door = (np.abs(dx) <= 3) & (dy >= 14) & (dy <= FI_FLOOR - y)
    c.put(door, KP['oak1'])
    c.put(door & (np.abs(dx) < 0.5), KP['oak0'])
    if flash > 0:
        top = m & ~shift(m, 0, 1)
        c.put(top, KB['b3'], flash)
        c.put(outline_mask(m), KB['b2'], flash * 0.8)
        g.add(top | outline_mask(m), LB, 0.8 * flash)
    return m


def fortaleza_interior_frame(f):
    t = f / FRAMES
    c, g = Canvas(AW, AH), Canvas(AW, AH)
    sanctum_hill(c, g, t)
    x, y = FI_KEEP
    power_ring(c, g, x, FI_FLOOR - 0.5, 19, 2.8, t, 0.5, col=KP['heal2'], light=LP['heal'])
    heal = 8
    a_h = f - heal
    lit = 1.0 if 0 <= a_h <= 8 else 0.25
    wflash = {11: 1.0, 12: 0.7, 13: 0.45, 14: 0.25}.get(f, 0.0)
    keep(c, g, x, y, lit=lit, flash=wflash)
    hy = y + 2 + math.sin(t * 2 * math.pi * 2) * 0.6
    green = {8: 1.0, 9: 0.8, 10: 0.6, 11: 0.4, 12: 0.2}.get(f, 0.0)
    fill = 0.55 if f < heal or f >= 21 else 0.55 + 0.35 * ease_out(min(1.0, (f - heal + 1) / 2.0))
    if 19 <= f <= 20:
        fill = 0.9 - 0.35 * (f - 18) / 3
    life_heart(c, g, x, hy, 0.85, fill=fill, green=green * 0.7, beat=1.0 if f in (9, 13) else 0.0)
    # healing falls into the window: three green crosses dropping from above
    for j, born in enumerate((3, 4, 5)):
        a = f - born
        if 0 <= a <= 3:
            yy = -2 + a * 9 + j
            xx = x + (j - 1) * 4 * (1 - a / 4)
            m = c.pixel(xx, yy) | c.pixel(xx - 1, yy) | c.pixel(xx + 1, yy) | c.pixel(xx, yy - 1) | c.pixel(xx, yy + 1)
            c.put(m, KP['heal3'])
            c.put(c.pixel(xx, yy), KP['heal4'])
            g.add(m, LP['heal'], 0.9)
            c.put(c.line(xx, yy - 2, xx, yy - 4), KP['heal2'], 0.6)
    if f == heal:
        heal_star(c, g, x, hy, 6)
    heal_crosses(c, g, f, heal, x, hy - 3, count=3, life=7, spread=5, seed=961)
    # ...and the keep answers with a ward: +2 de escudo
    if 0 <= f - 11 <= 7:
        ward_rings(c, g, x, y + 2, f - 11, size=21, squash=1.0, life=8)
    if f == 11:
        star4(c, g, x, y - 15, 6, core=K['white'], col=KB['b3'], edge=KB['b2'], light=LB, gain=1.2)
    sparks(c, g, f, 11, x, y - 13, 963, 10, (0.6, 1.4), 6, (KB['b3'], KB['b2'], KB['b1']), LB, gravity=-0.03,
           gain=0.7)
    motes(c, g, t, 967, 7, (16, 8, 72, 50), KP['heal3'], KB['b2'], LP['heal'], gain=0.4)
    vignette(c, 0.72)
    return c, g


# ---------------------------------------------------------------------------------------- Grito de Batalla
GB_P0, GB_P1, GB_P2 = (22.0, 30.0), (30.0, 34.0), (44.0, 21.0)
GB_SHIELD = (16.0, 45.0)


def rampart_night(c, g, t):
    """On the ramparts: crenellations in front, a valley with distant fires, two crimson banners on poles."""
    gradient_bg(c, K['night0'], K['night2'], K['night1'], 0.6)
    twinkling_stars(c, g, t, 971, 18, (2, 2, 86, 30), avoid=(66, 12, 8))
    c.put(c.disc(66, 12, 4.5), K['moon1'])
    c.put(c.disc(66, 12, 4.5) & ~c.disc(67.5, 11, 4.0), K['moon2'])
    g.add(c.disc(66, 12, 4.5), L['blue'], 0.2)
    valley = c.poly([(0, 38), (20, 35), (40, 38), (60, 34), (88, 37), (88, 64), (0, 64)])
    c.put(valley, K['vio0'])
    c.put(valley & ~shift(valley, 0, 1), K['vio3'], 0.6)
    for i, (x, y) in enumerate(((58, 40), (66, 42), (78, 39))):
        lit = 0.6 + 0.4 * math.sin((t * (1 + i % 2) + i * 0.3) * 2 * math.pi)
        m = c.pixel(x, y)
        c.put(m, K['window'] if lit > 0.5 else K['window2'])
        g.add(m, L['warm'], 0.3 * lit)
    wall = c.rect(0, 50, AW - 1, AH - 1)
    for x in range(0, AW, 10):
        wall |= c.rect(x, 45, x + 5, 50)
    c.put(wall, KW['stone1'])
    c.put(wall & ~shift(wall, 0, 1), KW['stone3'])
    c.put(wall & (c.y > 51) & ((np.floor(c.x / 7) + np.floor(c.y / 4)) % 2 == 0) & ((np.floor(c.y) % 4) == 0), KW['stone0'])
    tower = c.rect(80, 30, 86, 45) | c.rect(79, 28, 87, 30)
    for x in (79, 82, 85):
        tower |= c.rect(x, 26, x + 1, 28)
    c.put(tower, K['roof2'])
    lit = 0.6 + 0.4 * math.sin(t * 4 * math.pi + 0.7)
    c.put(c.rect(83, 34, 83, 35), K['window'] if lit > 0.5 else K['window2'])
    g.add(c.rect(83, 34, 83, 35), L['warm'], 0.3 * lit)


def grito_batalla_frame(f):
    t = f / FRAMES
    c, g = Canvas(AW, AH), Canvas(AW, AH)
    rampart_night(c, g, t)
    blow = 5
    sh_x, sh_y = GB_SHIELD
    slab = shield(c, g, sh_x, sh_y, W=8.0, H=20.0, gem_gain=1.0 + (1.5 if 12 <= f <= 14 else 0))
    faint_ward(c, g, slab > 0, t, a=0.6)
    bob = math.sin(t * 2 * math.pi * 2) * 0.6
    lift = {3: -1, 4: -1.5, 5: 0.5, 6: 1, 7: 1, 8: 0.5, 9: 0.5}.get(f, 0)
    p0 = (GB_P0[0], GB_P0[1] + bob + lift)
    p1 = (GB_P1[0], GB_P1[1] + bob + lift)
    p2 = (GB_P2[0], GB_P2[1] + bob + lift)
    sounding = 5 <= f <= 11
    converge(c, g, f, p0[0], p0[1], 2, 3, 977, 8, 9, (K['steel3'], K['cyan3']), L['cyan'])
    bx, by = war_horn(c, g, p0, p1, p2, flare=1.0 if sounding else 0.0)
    if sounding:
        for k in range(3):
            sound_rings(c, g, bx + 2, by, (f - blow) - k * 2.2, life=7, ang=-0.45, spread=0.65, step=5.0)
    if f == blow:
        gold_star(c, g, bx + 1, by, 7)
    for j, born in enumerate((7, 10)):                               # roba 2: cards flung out of the sound
        a = f - born
        if 0 <= a <= 7:
            k = ease_out(min(1.0, (a + 1) / 6.0))
            x = bx + 6 + k * (16 + j * 6)
            y = by - 2 + (j * 8 - 4) * k - 6 * math.sin(k * math.pi)
            from rogue_pool_art_2 import paint_card
            fade = 1.0 if a < 6 else 1 - (a - 5) / 3
            paint_card(c, g, x, y, abs(math.cos(a * 0.9)) if a < 5 else 1.0, alpha=fade, glow=0.4)
            if a == 5:
                star4(c, g, x, y - 7, 3, col=K['violet3'], edge=K['violet2'], light=L['violet'])
    # Aguante: the shield's gold pulse climbs to the horn and turns into Fuerza
    gx0, gy0 = shield_gem(sh_x, sh_y, 20.0)
    gold_pulse(c, g, gx0, gy0, p0[0] + 4, p0[1] + 3, {12: 0.0, 13: 0.5, 14: 1.0}.get(f, -1))
    if f == 15:
        fury_star(c, g, p1[0], p1[1] - 2, 6)
    for side, born in ((-1, 15), (1, 16)):
        rising_chevrons(c, g, f, p1[0] + side * 7, p1[1] - 6, born, 5, KW['crim5'], LW['crimson'], count=2, gain=0.9)
    motes(c, g, t, 983, 6, (4, 6, 84, 44), KW['gold3'], K['cyan3'], LW['gold'], gain=0.4)
    vignette(c, 0.72)
    return c, g


# ---------------------------------------------------------------------------------------- Grito de Guerra
GG_POLE = 15.0
GG_SIGILS = ((60.0, 18.0), (74.0, 32.0), (58.0, 42.0))


def gg_banner(c, g, t, glow):
    x = GG_POLE
    pole = c.rect(int(x), 6, int(x) + 1, 52)
    c.put(outline_mask(pole), K['ink'])
    c.put(pole, KP['oak2'])
    c.put(c.rect(int(x) - 1, 4, int(x) + 2, 6), KW['gold2'])
    c.put(c.pixel(x, 3) | c.pixel(x + 1, 3), KW['gold3'])
    cloth = np.zeros((AH, AW), bool)
    rows = {}
    for yy in range(8, 34):
        k = (yy - 8) / 26.0
        wv = math.sin(((yy - 8) / 9.0 - t * 2) * 2 * math.pi) * (0.3 + 1.8 * k)
        x0, x1 = x + 2, x + 15 + wv
        if yy >= 29:
            notch = (yy - 28) * 1.2
            for xx in range(int(x0), int(round(x1)) + 1):
                if xx < x1 - notch or xx < x0 + 3:
                    cloth[yy, xx] = True
        else:
            cloth[yy, int(x0):int(round(x1)) + 1] = True
        rows[yy] = wv
    c.put(outline_mask(cloth), K['ink'])
    c.put(cloth, KW['crim3'])
    for yy, wv in rows.items():
        if wv < -0.8:
            c.put(cloth & (np.abs(c.y - yy - 0.5) < 0.5), KW['crim2'])
    c.put(cloth & (c.y < 9.5), KW['gold1'])
    cx, cy = x + 8.5, 19
    emb = cloth & (((np.abs(c.x - cx) < 0.7) & (np.abs(c.y - cy) < 6)) | ((np.abs(c.y - cy + 2.5) < 0.7) & (np.abs(c.x - cx) < 3.5)))
    c.put(emb, KW['gold2'] if glow < 0.5 else KW['gold3'])
    g.add(emb, LW['gold'], 0.2 + glow)
    return cx, cy


def grito_guerra_frame(f):
    t = f / FRAMES
    c, g = Canvas(AW, AH), Canvas(AW, AH)
    crimson_dusk(c, g, t)
    shout = 5
    glow = {3: 0.4, 4: 0.8, 5: 1.0, 6: 0.8, 7: 0.5}.get(f, 0.0)
    cx, cy = gg_banner(c, g, t, glow)
    for k in range(2):
        sound_rings(c, g, cx + 6, cy, (f - shout) - k * 2.5, life=8, ang=0.0, spread=0.9,
                    col=KW['crim4'], light=LW['crimson'], step=6.0)
    if f == shout:
        fury_star(c, g, cx, cy, 7)
    hits = (8, 9, 10)
    for i, (sx, sy) in enumerate(GG_SIGILS):
        hit = hits[i]
        foe_sigil(c, g, sx, sy, t + i * 0.3, hit=(f - hit) if f >= hit else -99, r=5.5)
        a = f - hit
        if 1 <= a <= 6:                                               # a mana crystal flies back to the banner
            k = ease_in_out(a / 6.0)
            mx = sx + (cx + 15 - sx) * k
            my = sy + (7 + i * 6 - sy) * k - math.sin(k * math.pi) * 8
            mana_drop(c, g, mx, my, size=1.0)
            tr = c.line(mx, my, mx + (sx - cx) * 0.08, my + 2)
            c.put(tr, KP['mana2'], 0.5)
        if a >= 7 and f <= 20:                                         # the crystals hang by the banner top
            hx, hy = cx + 15, 7 + i * 6
            bob = math.sin((t * 2 + i * 0.3) * 2 * math.pi) * 0.7
            mana_drop(c, g, hx, hy + bob, size=1.0, gain=1.2 if a == 7 else 0.8)
            if a == 7:
                star4(c, g, hx, hy + bob, 3, core=K['white'], col=KP['mana3'], edge=KP['mana2'], light=LP['mana'])
        if f == 21 and True:
            hx, hy = cx + 15, 7 + i * 6
            sparks(c, g, f, 21, hx, hy, 991 + i, 5, (0.5, 1.0), 3, (KP['mana3'], KP['mana2'], KP['mana1']), LP['mana'],
                   gravity=-0.05, gain=0.8)
    motes(c, g, t, 993, 7, (22, 6, 86, 48), KW['crim4'], KP['mana2'], LW['crimson'], gain=0.4)
    vignette(c, 0.72)
    return c, g


def crimson_dusk(c, g, t):
    """A battlefield at the last light: violet sky with a low crimson band, broken ground, mist."""
    gradient_bg(c, K['vio1'], K['vio2'], KP['blood1'], 0.6)
    c.put(np.abs(c.y - 44) < 3, KW['crim1'], 0.3)
    twinkling_stars(c, g, t, 995, 12, (24, 2, 86, 26), light=L['violet'])
    hills = c.poly([(0, 47), (18, 44), (36, 46), (56, 43), (74, 45), (88, 43), (88, 64), (0, 64)])
    c.put(hills, K['vio0'])
    c.put(hills & ~shift(hills, 0, 1), KP['blood2'], 0.7)
    c.put(c.rect(0, 52, AW - 1, AH - 1), KP['blood0'])
    c.put(c.rect(0, 52, AW - 1, 52), KP['blood2'])
    for x in (30, 44, 82):
        c.put(c.line(x, 51, x + 2, 43), K['roof'])


# ---------------------------------------------------------------------------------------- Hachazo Temerario
HT_HANDS = (27.0, 46.0)
HT_IDLE = math.radians(-70)
HT_RAISE = math.radians(-128)
HT_HIT = math.radians(-8)
HT_TOTEM = (64.0, 51.0)            # the totem's base


def dark_wood(c, g, t):
    """A forest clearing at night: black pines, a blue moon behind them, a ring of stones."""
    gradient_bg(c, K['teal0'], K['teal2'], K['night1'], 0.6)
    c.put(c.disc(44, 12, 7), K['moon0'], 0.5)
    c.put(c.disc(44, 12, 5), K['moon1'])
    g.add(c.disc(44, 12, 5), L['blue'], 0.2)
    twinkling_stars(c, g, t, 1001, 10, (2, 2, 86, 22), avoid=(44, 12, 8))
    for i, x in enumerate((4, 12, 26, 34, 52, 78, 86)):
        h = 26 + (i * 7) % 12
        tree = np.zeros((AH, AW), bool)
        for k in range(4):
            yb = 48 - k * h / 5.0
            tree |= c.poly([(x - 6 + k, yb), (x, yb - h / 3.0), (x + 6 - k, yb)])
        c.put(tree, K['roof2'] if i % 2 else K['roof'])
    ground = c.rect(0, 51, AW - 1, AH - 1)
    c.put(ground, K['night0'])
    c.put(c.rect(0, 51, AW - 1, 51), K['teal3'])
    for x in (8, 40, 82):
        st = c.disc(x, 52, 2.2) & (c.y < 53)
        c.put(st, KW['stone2'])


def totem(c, g, x, base, t, keep=None, dx=0.0, dy=0.0, rot=0.0, eyes=1.0, a=1.0):
    """An enemy totem: a carved post with two faces stacked, violet war paint and glowing eyes."""
    cx, cy = x + dx, base + dy
    co, si = math.cos(rot), math.sin(rot)
    lx = (c.x - cx) * co + (c.y - cy) * si
    ly = -(c.x - cx) * si + (c.y - cy) * co
    m = (np.abs(lx) <= 6.5) & (ly <= 0) & (ly >= -30)
    m |= (np.abs(lx) <= 8.5) & (ly <= -13) & (ly >= -16)          # carved wings between the faces
    m |= (np.abs(lx) <= 3) & (ly < -30) & (ly >= -34 + np.abs(lx))  # pointed top
    if keep is not None:
        m &= keep
    if not m.any():
        return m
    c.put(outline_mask(m), K['ink'], a)
    c.put(m, KP2['totem2'], a)
    c.put(m & (lx < -2.5), KP2['totem3'], a)
    c.put(m & (lx > 3.5), KP2['totem1'], a)
    for fy in (-8.0, -23.0):
        brow = m & (np.abs(ly - fy + 3.5) < 0.6) & (np.abs(lx) < 5)
        mouth = m & (np.abs(ly - fy - 3) < 0.8) & (np.abs(lx) < 3)
        c.put(brow | mouth, KP2['totem0'], a)
        paint = m & (np.abs(ly - fy) < 4.5) & (np.abs(np.abs(lx) - 5) < 0.8)
        c.put(paint, K['violet1'], a)
        for ex in (-2.2, 2.2):
            e = m & (np.abs(lx - ex) < 1.0) & (np.abs(ly - fy + 1) < 0.8)
            c.put(e, K['violet3'] if eyes > 0.3 else KP2['totem0'], a)
            g.add(e, L['violet'], 0.9 * eyes * a)
    return m


def ht_angle(f):
    keys = {6: -82, 7: -98, 8: -112, 9: -119, 10: -121, 11: -62, 12: -8, 13: -4, 14: -6, 15: -8}
    if f <= 5:
        return HT_IDLE
    if f in keys:
        return math.radians(keys[f])
    return HT_HIT + (HT_IDLE - HT_HIT) * ease_in_out((f - 15) / 7.0) if f <= 22 else HT_IDLE


def hachazo_temerario_frame(f):
    t = f / FRAMES
    c, g = Canvas(AW, AH), Canvas(AW, AH)
    dark_wood(c, g, t)
    hit = 12
    age = f - hit
    tx, base = HT_TOTEM
    if f < hit:
        totem(c, g, tx, base, t)
    elif age <= 6:                                                     # split: two halves fall apart
        k = age / 6.0
        left = c.x < tx + 0.5
        totem(c, g, tx, base, t, keep=left, dx=-age * 1.2, dy=0, rot=-0.12 * age, eyes=max(0.0, 1 - age / 3), a=1)
        totem(c, g, tx, base, t, keep=~left, dx=age * 1.2, dy=0, rot=0.12 * age, eyes=max(0.0, 1 - age / 3), a=1)
    elif age >= 7:                                                     # a new totem rises from the ground
        k = ease_out(min(1.0, (age - 6) / 5.0))
        totem(c, g, tx, base, t, keep=c.y <= base, dy=34 * (1 - k), eyes=k)
        if age <= 9:
            dust_puffs(c, tx, base - 1, f, hit + 7, 3, 1.6, rise=0.4, life=5, seed=1011, big=0.8)
    hx, hy = HT_HANDS
    tremble = (1 if f % 2 else -1) * 1.0 if 8 <= f <= 10 else 0.0
    hy += math.sin(t * 2 * math.pi * 2) * 0.7 if (f <= 5 or f >= 21) else 0.0
    ang = ht_angle(f)
    if f == 12:
        R = 38
        crescent(c, g, hx, hy, R, -ang, math.radians(120), -1, 11.0,
                 (K['cyan1'], K['cyan2'], K['steel3'], K['white']), L['cyan'], gain=1.5)
    elif 13 <= f <= 15:
        q = (f - 13) / 3.0
        crescent(c, g, hx, hy, 38, -HT_HIT, math.radians(110) * (1 - 0.6 * q), -1, 9 * (1 - 0.6 * q),
                 (K['cyan1'], K['cyan2'], K['steel3'], K['white']), L['cyan'], a=0.8 - 0.5 * q, dash=f >= 14,
                 gain=1.2 * (1 - q))
    ex, ey = greataxe(c, g, hx + tremble, hy, ang, t, s=1.25, fury=1.0 if 8 <= f <= 13 else 0.0)
    # the price: her blood runs off the wrapped haft (the grip) while the axe is held up
    for j, born in enumerate((7, 8, 9)):
        a = f - born
        if 0 <= a <= 4:
            x0 = hx + math.cos(ang) * (-2 + j * 2) + (j - 1)
            y0 = hy + math.sin(ang) * (-2 + j * 2) + 2
            blood_drop(c, g, x0, y0 + a * a * 1.2 + a, 0.9, stretch=1 + 0.2 * a)
        splash(c, g, f, born + 5, hx + (j - 1) * 2, 52, seed=1013 + j, count=5)
    if f == 10:
        star4(c, g, ex, ey, 4, core=K['white'], col=KW['crim5'], edge=KW['crim4'], light=LW['crimson'], gain=1.2)
    ix, iy = tx, base - 18
    if f == hit:
        c.put(c.disc(ix, iy, 8), K['cyan2'], 0.6)
        c.put(c.disc(ix, iy, 4.5), K['cyan4'])
        c.put(c.disc(ix, iy, 2.2), K['white'])
        g.add(c.disc(ix, iy, 9), L['cyan'], 1.4)
    if 0 <= age <= 3:
        star4(c, g, ix, iy, {0: 14, 1: 9, 2: 5, 3: 2}[age], gain=1.5)
    if 0 <= age <= 8:
        rng = np.random.default_rng(1021)
        for i in range(12):                                            # wood chips
            a0 = rng.uniform(0.2, math.pi - 0.2)
            spd = rng.uniform(1.2, 2.6)
            x = ix + math.cos(a0) * spd * age
            y = iy - math.sin(a0) * spd * age + 0.3 * age * age
            if y < 52 and age > 0:
                m = c.rect(int(x), int(y), int(x) + (i % 2), int(y))
                c.put(m, KP2['totem3'] if i % 2 else KP2['totem2'], 1 - age / 9)
    sparks(c, g, f, hit, ix, iy, 1023, 8, (0.8, 1.8), 6, (K['violet4'], K['violet3'], K['violet1']), L['violet'],
           gravity=-0.04, gain=0.8)
    dust_puffs(c, tx, base - 1, f, hit, 5, 2.2, rise=0.5, life=7, seed=1025)
    motes(c, g, t, 1027, 6, (4, 6, 84, 46), K['cyan3'], KW['crim4'], L['cyan'], gain=0.4)
    vignette(c, 0.72)
    if f == hit:
        shake(c, g, 0, 2)
    elif f == hit + 1:
        shake(c, g, 1, -1)
    return c, g


# ---------------------------------------------------------------------------------------- Puño de Hierro
PH_FIST = (31.0, 30.0)
PH_WALL = 54


def dungeon_wall(c, g, t, x0, crack, jolt):
    """A training hall: big blocks; the wall on the right begins at x0; one block carries an enemy sigil."""
    gradient_bg(c, K['teal0'], K['teal1'], K['night0'], 0.6)
    back = c.rect(0, 0, AW - 1, 51)
    c.put(back, KW['stone0'])
    for row, y in enumerate(range(2, 51, 8)):
        c.put(c.rect(0, y, x0 - 1, y), K['ink'], 0.6)
        for x in range(0 if row % 2 else 9, x0, 18):
            c.put(c.rect(x, y + 1, x, y + 7), K['ink'], 0.6)
    lit = np.clip(1 - np.hypot(c.x - 20, c.y - 14) / 30, 0, 1)
    c.put(back & (c.x < x0), KW['stone1'], np.clip(lit * 1.5, 0, 1))
    c.put(c.rect(8, 10, 9, 15), KW['iron1'])
    flame(c, g, 9, 9, 5, 1.8, t, 0.2, gain=0.8, side=False)
    wall = c.rect(x0, 0, AW - 1, 51)
    c.put(wall, KW['stone2'])
    c.put(c.rect(x0, 0, x0, 51), KW['stone3'])
    for row, y in enumerate(range(0, 51, 10)):
        c.put(c.rect(x0, y, AW - 1, y), KW['stone0'])
        for x in range(x0 + (0 if row % 2 else 8), AW, 16):
            c.put(c.rect(x, y + 1, x, y + 9), KW['stone0'])
    # the marked block
    bx0, by0 = x0, 21
    blk = c.rect(bx0 + 1 + jolt, by0, bx0 + 15 + jolt, by0 + 18)
    c.put(blk, KW['stone3'])
    c.put(outline_mask(blk), KW['stone0'])
    foe_sigil(c, g, bx0 + 8 + jolt, by0 + 9, t, hit=-99 if crack <= 0 else int(crack * 0) + 99, r=4.5, a=0.8)
    if crack > 0:
        rng = np.random.default_rng(1031)
        cm = np.zeros((AH, AW), bool)
        for a0 in (-2.4, -1.5, -0.6, 0.4, 1.3, 2.3):
            x, y = bx0 + 1 + jolt, by0 + 9
            ln = (4 + 8 * crack) * rng.uniform(0.7, 1.1)
            for s_ in range(3):
                nx = x + abs(math.cos(a0)) * ln / 3 + rng.uniform(-0.6, 0.6)
                ny = y + math.sin(a0) * ln / 3 + rng.uniform(-0.6, 0.6)
                cm |= c.line(x, y, nx, ny)
                x, y = nx, ny
        c.put(cm & (c.x >= x0), K['ink'], min(1.0, crack * 1.5))
        c.put(shift(cm, 0, -1) & ~cm & (c.x >= x0), KW['stone3'], min(1.0, crack))
    floor_slabs(c, 52)


def puno_hierro_frame(f):
    t = f / FRAMES
    c, g = Canvas(AW, AH), Canvas(AW, AH)
    hit = 11
    age = f - hit
    crack = 0.0 if f < hit else (1.0 if age <= 8 else max(0.0, 1 - (age - 8) / 4.0))
    jolt = 1 if age in (0, 1) else 0
    dungeon_wall(c, g, t, PH_WALL, crack, jolt)
    x, y = PH_FIST
    dx = {8: -3, 9: -6, 10: 6, 11: 15, 12: 15, 13: 14, 14: 12, 15: 9, 16: 6, 17: 4, 18: 2, 19: 1}.get(f, 0)
    bob = math.sin(t * 2 * math.pi * 2) * 0.7 if (f <= 7 or f >= 20) else 0.0
    fx, fy = x + dx, y + bob
    if f == 10:
        speed_lines(c, g, fx - 8, fy, 1, 0, 1.0, 1033, count=7, spread=6, length=(5, 11), back=20)
    if 8 <= f <= 9:
        converge(c, g, f, fx, fy, 8, 2, 1035, 8, 9, (K['steel3'], K['cyan3']), L['cyan'])
    fury = 1.0 if 13 <= f <= 20 else 0.0
    gauntlet(c, g, fx, fy, fury=fury, flash={11: 0.6, 12: 0.2}.get(f, 0.0), rune=1.5 if fury else 1.0, s=1.4)
    ix, iy = PH_WALL + 1, fy
    if f == hit:
        c.put(c.disc(ix, iy, 7), KW['spark1'], 0.6)
        c.put(c.disc(ix, iy, 4), KW['spark2'])
        c.put(c.disc(ix, iy, 2), K['white'])
        g.add(c.disc(ix, iy, 8), L['warm'], 1.3)
    if 0 <= age <= 3:
        star4(c, g, ix, iy, {0: 12, 1: 8, 2: 5, 3: 2}[age], core=K['white'], col=KW['spark2'], edge=KW['spark1'],
              light=L['warm'], gain=1.4)
    if 0 <= age <= 7:
        rng = np.random.default_rng(1037)
        for i in range(10):                                            # stone chips
            a0 = rng.uniform(1.8, 4.4)
            spd = rng.uniform(1.0, 2.4)
            px = ix + math.cos(a0) * spd * age
            py = iy - math.sin(a0) * spd * age + 0.3 * age * age
            if py < 52 and age > 0:
                m = c.rect(int(px), int(py), int(px) + (i % 2), int(py) + ((i + age) % 2))
                c.put(outline_mask(m), K['ink'], 0.7)
                c.put(m, KW['stone3'] if i % 3 else K['dust1'])
    dust_puffs(c, ix - 2, iy + 2, f, hit + 1, 4, 1.6, rise=0.3, life=7, seed=1039)
    # +1 Fuerza: the rune on the back of the hand turns crimson and chevrons rise off the knuckles
    if f == 13:
        fury_star(c, g, fx - 8, fy - 4, 6)
    for side, born in ((-1, 13), (1, 14)):
        rising_chevrons(c, g, f, fx - 6 + side * 6, fy - 12, born, 5, KW['crim5'], LW['crimson'], count=2, gain=0.9)
    motes(c, g, t, 1041, 6, (4, 6, 52, 48), K['cyan3'], KW['spark1'], L['cyan'], gain=0.4)
    vignette(c, 0.72)
    if f == hit:
        shake(c, g, 1, 0)
    return c, g


# ---------------------------------------------------------------------------------------- Respiro
RP_FIRE = (44.0, 52.0)
RP_HEART = (44.0, 18.0)


def night_camp(c, g, t):
    """A camp under the stars in the hills: dark pines, a bedroll, warm light round the fire."""
    gradient_bg(c, K['night0'], K['night2'], K['teal1'], 0.6)
    twinkling_stars(c, g, t, 1051, 20, (2, 2, 86, 34), avoid=(44, 18, 12))
    hills = c.poly([(0, 42), (14, 38), (28, 41), (46, 37), (62, 40), (78, 36), (88, 39), (88, 64), (0, 64)])
    c.put(hills, K['teal0'])
    c.put(hills & ~shift(hills, 0, 1), K['teal3'], 0.6)
    for i, x in enumerate((6, 13, 79, 85)):
        h = 18 + (i * 5) % 7
        tree = c.poly([(x - 5, 48), (x, 48 - h), (x + 5, 48)])
        c.put(tree, K['roof'])
    ground = c.rect(0, 48, AW - 1, AH - 1)
    c.put(ground, KP2['grass0'])
    c.put(c.rect(0, 48, AW - 1, 48), KP2['grass1'])
    fx, fy = RP_FIRE
    warm = np.clip(1 - np.hypot((c.x - fx) / 30, (c.y - fy) / 14), 0, 1)
    c.put(ground & (warm > 0.3), KP['oak1'], np.clip(warm * 1.2, 0, 1))
    c.put(ground & (warm > 0.6), KP['oak2'], np.clip(warm * 1.2 - 0.6, 0, 1))
    roll = c.rect(62, 53, 78, 56)
    c.put(outline_mask(roll), K['ink'])
    c.put(roll, KW['crim2'])
    c.put(c.rect(62, 53, 78, 53), KW['crim3'])
    c.put(c.rect(65, 53, 65, 56) | c.rect(75, 53, 75, 56), KP['oak2'])


def respiro_frame(f):
    t = f / FRAMES
    c, g = Canvas(AW, AH), Canvas(AW, AH)
    night_camp(c, g, t)
    fx, fy = RP_FIRE
    # the planted sword and the shield leaning on it, on the left
    sword(c, g, 20, 28, math.pi / 2 + 0.08, t * 2, blade=26)
    ward_age = f - 13
    slab = shield(c, g, 23, 44, W=8.0, H=20.0, gem_gain=1.0 + (1.4 if 12 <= f <= 14 else 0),
                  flash={13: 0.4, 14: 0.15}.get(f, 0.0))
    # the fire: stones, logs, flame
    stones = np.zeros((AH, AW), bool)
    for i in range(7):
        a0 = math.pi * (i / 6.0)
        stones |= c.disc(fx + math.cos(a0) * 7, fy + 1 - math.sin(a0) * 1.5, 1.6)
    c.put(outline_mask(stones), K['ink'])
    c.put(stones, KW['stone2'])
    logs = c.line(fx - 5, fy + 1, fx + 4, fy - 1) | c.line(fx - 4, fy - 1, fx + 5, fy + 1)
    c.put(logs | shift(logs, 0, 1), KP['oak2'])
    flame(c, g, fx, fy - 1, 9, 3.2, t, 0.1, gain=1.0)
    g.add(c.disc(fx, fy - 4, 12) & ~c.disc(fx, fy - 4, 7), L['warm'], 0.07)
    # embers rise; between 4 and 10 they turn green and gather into her heart above the fire
    rng = np.random.default_rng(1061)
    for i in range(12):
        ph = rng.random()
        u = frac(t * (1 if i % 3 else 2) + ph)
        ex = fx + rng.uniform(-3, 3) + math.sin((u * 2 + ph) * 2 * math.pi) * 1.5
        ey = fy - 6 - u * 30
        heal = 4 <= f <= 10 and i % 2 == 0
        if heal:
            k = ease_in_out(min(1.0, (f - 3) / 5.0))
            ex = ex + (RP_HEART[0] - ex) * k * 0.6
        a = math.sin(u * math.pi)
        if a < 0.2:
            continue
        m = c.pixel(ex, ey)
        c.put(m, KP['heal3'] if heal else KP2['ember2'], a)
        g.add(m, LP['heal'] if heal else L['warm'], 0.7 * a)
    hx, hy = RP_HEART
    hy += math.sin(t * 2 * math.pi * 2) * 0.7
    fill = 0.4
    if f >= 8:
        fill = 0.4 + 0.5 * ease_out(min(1.0, (f - 7) / 3.0))
    if f >= 20:
        fill = 0.9 - 0.5 * ease_in_out((f - 19) / 4.0)
    green = {8: 1.0, 9: 0.8, 10: 0.6, 11: 0.4, 12: 0.2}.get(f, 0.0)
    life_heart(c, g, hx, hy, 1.0, fill=fill, green=green * 0.7, beat=1.0 if f in (9, 15) else 0.0)
    if f == 8:
        heal_star(c, g, hx, hy, 6)
    heal_crosses(c, g, f, 8, hx, hy - 3, count=4, life=8, spread=7, seed=1063)
    if 0 <= ward_age <= 7:
        ward_rings(c, g, 22, 40, ward_age, size=16, squash=1.1, life=8)
    motes(c, g, t, 1067, 5, (8, 6, 80, 40), KP['heal3'], KW['spark1'], LP['heal'], gain=0.3)
    vignette(c, 0.72)
    return c, g


# ---------------------------------------------------------------------------------------- Rugido
RG_HELM = (44.0, 25.0)


def arena_night(c, g, t):
    """A ruined arena at night: tiers of seats in silhouette, two braziers, sand floor."""
    gradient_bg(c, K['night0'], K['night2'], K['night1'], 0.6)
    twinkling_stars(c, g, t, 1071, 14, (2, 2, 86, 16), avoid=(44, 14, 14))
    for i, y in enumerate((20, 27, 34)):
        tier = c.rect(0, y, AW - 1, y + 6) & ~c.rect(30, 0, 57, 64)
        c.put(tier, K['roof2'] if i % 2 else K['roof'])
        c.put(tier & (np.abs(c.y - y) < 0.6), K['vio3'], 0.6)
        for x in range(2 + i * 3, AW, 9):
            c.put(c.rect(x, y + 1, x, y + 5) & tier, K['ink'], 0.7)
    gate = c.rect(30, 16, 57, 46) | (c.disc(43.5, 16, 13.5) & (c.y < 16))
    c.put(gate, K['vio0'])
    c.put(outline_mask(gate) & ~gate, K['vio3'], 0.7)
    for x in (14.0, 74.0):
        bowl = c.rect(int(x) - 3, 40, int(x) + 3, 42) | c.rect(int(x) - 1, 42, int(x) + 1, 49)
        c.put(outline_mask(bowl), K['ink'])
        c.put(bowl, KW['iron2'])
        flame(c, g, x, 39, 6, 2.4, t, x * 0.01, gain=0.9, side=False)
    sand = c.rect(0, 49, AW - 1, AH - 1)
    c.put(sand, KP['oak1'])
    c.put(c.rect(0, 49, AW - 1, 49), KP['oak3'])
    for i in range(16):
        x = (i * 23) % AW
        c.put(c.pixel(x, 52 + (i * 7) % 11), KP['oak2'])


def rugido_frame(f):
    t = f / FRAMES
    c, g = Canvas(AW, AH), Canvas(AW, AH)
    arena_night(c, g, t)
    roar = 7
    x, y = RG_HELM
    jolt = {7: -1, 8: -1, 9: 0}.get(f, 0)
    # the post and crossbar it sits on (an armour stand: the helm is empty)
    post = c.rect(int(x) - 1, int(y) + 11, int(x) + 1, 50)
    bar = c.rect(int(x) - 10, int(y) + 14, int(x) + 10, int(y) + 15)
    st = post | bar
    c.put(outline_mask(st), K['ink'])
    c.put(st, KP['oak2'])
    c.put(st & ~shift(st, 0, 1), KP['oak3'])
    contact_shadow(c, x, 50, 6)
    fury = 1.0 if 4 <= f <= 19 else 0.0
    glow = 0.35 + 0.15 * math.sin(t * 4 * math.pi)
    glow = {6: 0.8, 7: 1.0, 8: 1.0, 9: 1.0, 10: 0.9, 11: 0.8}.get(f, glow if not fury else 0.7)
    plume = {7: 1.0, 8: 1.5, 9: 1.2, 10: 0.8, 11: 0.4}.get(f, 0.0)
    hm = helm(c, g, x, y + jolt, glow=glow, fury=fury, plume=plume, t=t, s=1.3)
    blood_aura(c, g, hm, {4: 0.4, 5: 0.8, 6: 1.0}.get(f, 0.0), t, width=1)
    land = drip(c, g, f, 0, x + 4.5, -3, y - 9, fall=3, s=1.3, swell=1, seed=1073)
    if land == 0:
        fury_star(c, g, x + 4, y - 9, 5)
    # the roar: shock rings round the visor, dust off the floor, sand thrown
    for k in range(3):
        a = (f - roar) - k * 1.5
        if 0 <= a <= 6:
            ring = ellipse_ring(c, x, y + jolt, 9 + a * 5, (9 + a * 5) * 0.8, 0.55) & ~hm
            kk = 1 - a / 7
            c.put(ring, KW['crim4'], kk)
            g.add(ring, LW['crimson'], 0.6 * kk)
    dust_puffs(c, x, 49, f, roar, 6, 2.6, rise=0.4, life=7, seed=1075)
    if f == roar:
        fury_star(c, g, x, y, 9, gain=1.5)
    # Fuerza: one chevron for the roar, two more for the Furia (bigger, later)
    rising_chevrons(c, g, f, x, y - 18, roar + 1, 6, KW['crim5'], LW['crimson'], count=1, gain=1.0)
    for side, born in ((-1, roar + 3), (1, roar + 4)):
        rising_chevrons(c, g, f, x + side * 15, y - 10, born, 6, KW['crim5'], LW['crimson'], count=1, gain=1.0)
    sparks(c, g, f, roar, x, y - 6, 1077, 10, (0.8, 1.8), 7, CRIM_SPARKS, LW['crimson'], gravity=-0.02, gain=0.8)
    motes(c, g, t, 1079, 6, (6, 6, 82, 46), KW['crim4'], KW['spark1'], LW['crimson'], gain=0.4)
    vignette(c, 0.72)
    if f == roar:
        shake(c, g, 1, 0)
    elif f == roar + 1:
        shake(c, g, -1, 0)
    return c, g


# ---------------------------------------------------------------------------------------- Sangre Hirviente
SH_CUP = (44.0, 30.0)
SH_GLASS = (16.0, 34.0)
SH_FLOOR = 52


def forge_night(c, g, t):
    """A dark forge: brick hood, a glowing hearth far back, an anvil in silhouette, an iron table in front."""
    gradient_bg(c, K['night0'], K['vio1'], K['night0'], 0.6)
    back = c.rect(0, 0, AW - 1, SH_FLOOR - 1)
    c.put(back, KP['brick1'])
    for row, y in enumerate(range(2, SH_FLOOR, 5)):
        c.put(c.rect(0, y, AW - 1, y), KP['brick0'])
        for x in range(0 if row % 2 else 5, AW, 10):
            c.put(c.rect(x, y + 1, x, y + 4), KP['brick0'])
    hearth = c.rect(62, 30, 82, 45) | (c.disc(72, 30, 10) & (c.y < 30))
    c.put(outline_mask(hearth) & ~hearth, KP['brick3'])
    c.put(hearth, K['ink'])
    glow = 0.7 + 0.3 * math.sin(t * 4 * math.pi)
    coals = hearth & (c.y > 40)
    c.put(coals, KP2['ember0'])
    c.put(coals & ((np.floor(c.x * 1.7 + c.y) % 3) == 0), KP2['ember1'])
    g.add(coals, L['warm'], 0.35 * glow)
    flame(c, g, 72, 41, 5, 3.0, t, 0.4, gain=0.6, side=False)
    anvil = c.poly([(58, 51), (60, 47), (56, 46), (56, 44), (70, 44), (74, 45), (66, 46), (64, 51)])
    c.put(anvil, K['roof'])
    table = c.rect(0, SH_FLOOR, AW - 1, AH - 1)
    c.put(table, KW['iron1'])
    c.put(c.rect(0, SH_FLOOR, AW - 1, SH_FLOOR), KW['iron3'])
    c.put(c.rect(0, SH_FLOOR + 1, AW - 1, SH_FLOOR + 1), KW['iron2'])


def sangre_hirviente_frame(f):
    t = f / FRAMES
    c, g = Canvas(AW, AH), Canvas(AW, AH)
    forge_night(c, g, t)
    x, y = SH_CUP
    power_ring(c, g, x, SH_FLOOR - 0.5, 15, 2.4, t, 0.5, col=KW['crim3'], light=LW['crimson'])
    # the hourglass turns: a new turn begins (frames 3-7)
    gx, gy = SH_GLASS
    if f < 3:                                                          # the old turn runs out
        hourglass(c, g, gx, gy, 0.0, 0.04, stream=False)
    elif f < 8:                                                        # turned over: a new turn
        hourglass(c, g, gx, gy, math.pi * ease_in_out((f - 2) / 5.0), 0.04, stream=False)
    else:                                                              # and its sand runs down
        hourglass(c, g, gx, gy, 0.0, 0.96 - 0.92 * (f - 8) / 15.0, stream=True)
    if f == 7:
        star4(c, g, gx, gy - 9, 4, core=K['white'], col=KW['gold3'], edge=KW['gold2'], light=LW['gold'])
    boil = {7: 0.3, 8: 0.6, 9: 1.0, 10: 1.0, 11: 1.0, 12: 0.8, 13: 0.6, 14: 0.4}.get(f, 0.15)
    rattle = 1 if f in (9, 11) else 0
    chalice(c, g, x + rattle, y + 3, boil=boil, t=t)
    contact_shadow(c, x, SH_FLOOR - 0.5, 6)
    # it boils over: a drop runs down the side and falls (pierdes 2 de vida)
    a = f - 10
    if 0 <= a <= 6:
        k = a / 6.0
        dx_ = 7.5 - k * 1.5
        yy = y + 3 + k * 13
        blood_drop(c, g, x + dx_ + 0.5, yy, 0.9, stretch=1 + k * 0.5)
        if a >= 1:
            trail = c.line(x + 7.5, y + 3, x + dx_ + 0.5, yy - 2) & ~c.disc(x + dx_ + 0.5, yy, 1.5)
            c.put(trail, KW['crim3'], 0.8)
    splash(c, g, f, 17, x + 6, SH_FLOOR - 1, seed=1081, count=5)
    # the steam rises and turns into a chevron: +1 de Fuerza
    rng = np.random.default_rng(1083)
    for i in range(8):
        ph = rng.random()
        u = frac(t * 2 + ph)
        sx = x + rng.uniform(-4, 4) + math.sin((u + ph) * 2 * math.pi) * 1.5
        sy = y + 2 - u * 16
        aa = math.sin(u * math.pi) * (0.4 + 0.6 * boil)
        if aa < 0.2:
            continue
        m = c.pixel(sx, sy) | c.pixel(sx + 1, sy)
        c.put(m, KW['crim3'] if i % 2 else KP['smoke3'], aa)
        g.add(m, LW['crimson'], 0.25 * aa)
    if f == 12:
        fury_star(c, g, x, y - 6, 6)
    rising_chevrons(c, g, f, x, y - 8, 12, 7, KW['crim5'], LW['crimson'], count=1, gain=1.1)
    motes(c, g, t, 1085, 6, (8, 6, 80, 48), KW['crim4'], KW['spark1'], LW['crimson'], gain=0.35)
    vignette(c, 0.72)
    return c, g


# ---------------------------------------------------------------------------------------- Sangría
SG_CUP = (13.0, 46.0)
SG_SWORD = (70.0, 14.0)      # crossguard of the planted sword (point down into the altar)
SG_CHANNEL_Y = 49


def blood_altar(c, g, t):
    """A sacrificial altar in a cave: a long stone slab with a carved channel, rune pillars, cold light from above."""
    gradient_bg(c, K['vio0'], K['vio1'], K['teal0'], 0.6)
    cave = c.poly([(0, 0), (88, 0), (88, 30), (80, 22), (70, 26), (58, 18), (44, 22), (30, 16), (18, 24), (8, 20),
                   (0, 26)])
    c.put(cave, K['roof'])
    c.put(outline_mask(cave) & ~cave, K['vio3'], 0.6)
    shaft = np.abs((c.x - 44) - (c.y - 20) * 0.15) < 6 + (c.y - 20) * 0.2
    c.put(shaft & (c.y > 20) & (c.y < 50), K['teal2'], 0.3)
    for x in (24.0, 50.0):
        p = c.rect(int(x) - 2, 24, int(x) + 2, 46)
        c.put(outline_mask(p), K['ink'])
        c.put(p, KP['temple1'])
        c.put(c.rect(int(x) - 2, 24, int(x) - 2, 46), KP['temple2'])
        for i, ry in enumerate((29, 35, 41)):
            r = c.pixel(x, ry) | c.pixel(x, ry + 1)
            lit = 0.5 + 0.5 * math.sin((t * 2 + i * 0.3 + x * 0.1) * 2 * math.pi)
            c.put(r, KW['crim4'] if lit > 0.5 else KW['crim2'])
            g.add(r, LW['crimson'], 0.4 * lit)
    slab = c.rect(4, 47, 84, 53)
    c.put(outline_mask(slab), K['ink'])
    c.put(slab, KP['temple2'])
    c.put(c.rect(4, 47, 84, 47), KP['temple3'])
    c.put(c.rect(4, 53, 84, 53), KP['temple1'])
    ch = c.rect(9, SG_CHANNEL_Y, 72, SG_CHANNEL_Y)
    c.put(ch, K['ink'])
    c.put(c.rect(0, 54, AW - 1, AH - 1), KP['temple0'])


def sangria_frame(f):
    t = f / FRAMES
    c, g = Canvas(AW, AH), Canvas(AW, AH)
    blood_altar(c, g, t)
    gx, gy = SG_SWORD
    # four drops of her blood fall into the cup at the head of the channel
    cx, cy = SG_CUP
    bowl = c.disc(cx, cy + 1, 3.6) & (c.y >= cy)
    c.put(outline_mask(bowl), K['ink'])
    c.put(bowl, KP['temple3'])
    for j, born in enumerate((0, 1, 2, 3)):
        drip(c, g, f, born, cx + (j % 2) - 0.5, -3, cy, fall=2, s=1.0, swell=1, seed=1091 + j)
    level = min(1.0, max(0.0, (f - 2) / 3.0)) if f < 20 else max(0.0, 1 - (f - 19) / 3.0)
    if level > 0:
        c.put(bowl & (c.y < cy + 1.5) & (c.y > cy - 0.5), KW['crim3'], level)
    # the blood runs along the channel to the sword's point
    run = 0.0 if f < 5 else min(1.0, (f - 4) / 5.0)
    if f >= 20:
        run = 0.0
    if run > 0:
        x1 = 10 + (gx - 10) * run
        stream = c.rect(10, SG_CHANNEL_Y, int(x1), SG_CHANNEL_Y)
        c.put(stream, KW['crim3'])
        c.put(stream & ((np.floor(c.x - f * 2) % 5) == 0), KW['crim5'])
        g.add(stream, LW['crimson'], 0.5)
        if run < 1:
            c.put(c.pixel(x1, SG_CHANNEL_Y) | c.pixel(x1, SG_CHANNEL_Y - 1), KW['crim5'])
    # the sword, point down into the altar: its fuller fills crimson from the point up
    fill = 0.0 if f < 9 else min(1.0, (f - 8) / 5.0)
    if f >= 20:
        fill = max(0.0, 1 - (f - 19) / 3.0)
    lab = sword(c, g, gx, gy, math.pi / 2, t * 2, rune_gain=1.0 + 1.4 * fill, fury=1.0 if fill >= 0.99 else 0.0,
                blade=34, cut=34 - 0.0)
    lab[(lab > 0) & (c.y > 52.5)] = 0
    if 0 < fill < 1:
        top = 48 - fill * 30
        fl = (lab == 5) & (c.y >= top)
        c.put(fl, KW['crim4'])
        g.add(fl, LW['crimson'], 0.9)
        fury_star(c, g, gx, top, 2, gain=0.8)
    if f == 14:
        fury_star(c, g, gx, gy - 1, 7, gain=1.5)
    if 14 <= f <= 19:
        blood_aura(c, g, lab > 0, {14: 1.0, 15: 0.8, 16: 0.6, 17: 0.45, 18: 0.3, 19: 0.15}[f], t, width=1)
    # +3 de Fuerza: three big chevrons
    for i, born in enumerate((14, 15, 16)):
        rising_chevrons(c, g, f, gx + (i - 1) * 9, gy + 4, born, 6, KW['crim5'], LW['crimson'], count=1, gain=1.1)
    sparks(c, g, f, 14, gx, gy, 1095, 9, (0.6, 1.6), 6, CRIM_SPARKS, LW['crimson'], gravity=-0.03, gain=0.8)
    motes(c, g, t, 1097, 6, (6, 22, 82, 48), KW['crim4'], K['violet3'], LW['crimson'], gain=0.35)
    vignette(c, 0.72)
    return c, g


# ---------------------------------------------------------------------------------------- Sed de Batalla
SB_GUARD = (22.0, 34.0)
SB_SIGIL = (69.0, 33.0)
SB_HEART = (12.0, 15.0)


def graveyard(c, g, t):
    """A graveyard at night: leaning tombstones, a dead tree, low fog drifting, a pale moon."""
    gradient_bg(c, K['night0'], K['teal1'], K['night1'], 0.6)
    c.put(c.disc(46, 10, 5), K['moon1'])
    c.put(c.disc(46, 10, 5) & ~c.disc(44.5, 9, 4.5), K['moon0'])
    g.add(c.disc(46, 10, 5), L['blue'], 0.2)
    twinkling_stars(c, g, t, 1101, 12, (2, 2, 86, 24), avoid=(46, 10, 8))
    tree = c.line(80, 47, 79, 24) | c.line(79, 30, 73, 22) | c.line(79, 27, 85, 20) | c.line(74, 23, 71, 23)
    c.put(tree | shift(tree, 1, 0), K['roof'])
    for x, h, lean in ((8, 10, -1), (30, 8, 1), (52, 9, 0), (60, 7, -1), (86, 9, 1)):
        stn = c.rect(x - 3, 47 - h, x + 3, 47) | (c.disc(x, 47 - h, 3) & (c.y < 47 - h))
        c.put(outline_mask(stn), K['ink'])
        c.put(stn, KW['stone1'])
        c.put(stn & (c.x < x - 1), KW['stone2'])
        c.put(c.rect(x - 1, 49 - h, x + 1, 49 - h) | c.rect(x, 48 - h, x, 51 - h), KW['stone0'])
    ground = c.rect(0, 47, AW - 1, AH - 1)
    c.put(ground, KP2['grass0'])
    c.put(c.rect(0, 47, AW - 1, 47), KP2['grass2'])
    for i, y in enumerate((45, 50, 55)):
        off = (t * (1 + i % 2) * AW) % AW
        band = (np.abs(c.y - y) <= 1.2) & (((c.x + off + i * 13) % 31) < 19)
        c.put(band, K['teal3'], 0.3)


def sed_batalla_frame(f):
    t = f / FRAMES
    c, g = Canvas(AW, AH), Canvas(AW, AH)
    graveyard(c, g, t)
    hit = 8
    age = f - hit
    sx, sy = SB_SIGIL
    foe_sigil(c, g, sx, sy, t, hit=age if age >= 0 else -99, r=7.0)
    dx = {6: -2, 7: 8, 8: 12, 9: 12, 10: 12, 11: 12, 12: 12, 13: 12, 14: 11, 15: 9, 16: 6, 17: 4, 18: 2, 19: 1}.get(f, 0)
    if f == 5:
        dx = -1
    bob = math.sin(t * 2 * math.pi * 2) * 0.7 if (f <= 4 or f >= 20) else 0.0
    gx, gy = SB_GUARD[0] + dx, SB_GUARD[1] + bob
    if f == 7:
        speed_lines(c, g, gx + 4, gy, 1, 0, 1.0, 1103, count=6, spread=4, length=(6, 12), back=4)
    draining = 9 <= f <= 15
    lab = sword(c, g, gx, gy, 0.0, t * 2, rune_gain=1.0 + (1.2 if draining else 0), fury=1.0 if draining else 0.0)
    # its life flows back along the blade, past the hilt, into her heart
    hx, hy = SB_HEART
    hy += math.sin(t * 2 * math.pi * 2) * 0.6
    if draining:
        rng = np.random.default_rng(1105)
        for i in range(14):
            ph = rng.random()
            u = frac((f - 9) / 7.0 * 2 + ph)
            if u < 0.55:                                               # along the blade, right to left
                k = u / 0.55
                px = sx - 2 + (gx - sx + 2) * k
                py = gy + rng.uniform(-1.5, 1.5)
            else:                                                      # up from the hilt to the heart
                k = (u - 0.55) / 0.45
                px = gx + (hx - gx) * k
                py = gy + (hy - gy) * k - math.sin(k * math.pi) * 4
            m = c.pixel(px, py)
            near = u > 0.85
            c.put(m, KP['heal3'] if near else KW['crim5'])
            g.add(m, LP['heal'] if near else LW['crimson'], 0.8)
    fill = 0.35
    if f >= 11:
        fill = 0.35 + 0.5 * ease_out(min(1.0, (f - 10) / 5.0))
    if f >= 20:
        fill = 0.85 - 0.5 * ease_in_out((f - 19) / 4.0)
    green = {11: 0.5, 12: 0.7, 13: 0.8, 14: 0.8, 15: 0.6, 16: 0.4, 17: 0.2}.get(f, 0.0)
    life_heart(c, g, hx, hy, 0.85, fill=fill, green=green * 0.7, beat=1.0 if f in (13, 16) else 0.0)
    heal_crosses(c, g, f, 12, hx, hy - 3, count=3, life=7, spread=5, seed=1107)
    if f == 16:
        heal_star(c, g, hx, hy, 5)
    if f == hit:
        c.put(c.disc(sx - 4, sy, 6), KW['crim4'], 0.6)
        c.put(c.disc(sx - 4, sy, 3), K['white'])
        g.add(c.disc(sx - 4, sy, 7), LW['crimson'], 1.3)
    if 0 <= age <= 3:
        fury_star(c, g, sx - 4, sy, {0: 11, 1: 7, 2: 4, 3: 2}[age], gain=1.4)
    sparks(c, g, f, hit, sx - 4, sy, 1109, 9, (1.0, 2.2), 6, CRIM_SPARKS, LW['crimson'], gravity=0.15)
    motes(c, g, t, 1111, 6, (4, 8, 84, 46), KP['heal3'], KW['crim4'], LP['heal'], gain=0.35)
    vignette(c, 0.72)
    if f == hit:
        shake(c, g, 1, 0)
    return c, g


ILLUSTRATIONS = {
    'fortaleza_interior': fortaleza_interior_frame,
    'grito_batalla': grito_batalla_frame,
    'grito_guerra': grito_guerra_frame,
    'hachazo_temerario': hachazo_temerario_frame,
    'puno_hierro': puno_hierro_frame,
    'respiro': respiro_frame,
    'rugido': rugido_frame,
    'sangre_hirviente': sangre_hirviente_frame,
    'sangria': sangria_frame,
    'sed_batalla': sed_batalla_frame,
}


def render(card_id):
    fn = ILLUSTRATIONS[card_id]
    base, glow = [], []
    for f in range(FRAMES):
        b, gl = fn(f)
        base.append(b)
        glow.append(gl)
    return base, glow
