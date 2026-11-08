"""Animated pixel-art illustrations for the warrior's rare loot cards (88x64, 24 frames at 12 fps, seamless).

Same method as warrior_pool_art.py / _2.py (shared vocabulary: her blood, Furia, Aguante, Fuerza, healing, block) and
docs/art-style.md. No figure: her gear on its own (sword, shield, greataxe, horn, gauntlet, helm) plus her cuirass on
a stand, her war hammer and an iron pact tablet, each drawn as one silhouette.

  conjuro_combate     Conjuro de Combate (10 + 6 de escudo): in a circle of standing stones the blade draws a rune
                      ring and shoots a bolt into an enemy sigil; a ward rises round her shield.
  coraza_viva         Coraza Viva (escudo = la mitad de la vida que falta): the empty part of her heart flows into her
                      cuirass, steel scales grow over it, a big ward.
  golpe_pesado        Golpe Pesado (16): in a quarry a slow, heavy blow splits a sigil-marked block; enemy magic pulls
                      the halves back together.
  golpe_total         Golpe Total (10 a todos): at a crossroads the sword spins a full circle through three sigils.
  golpe_escudo        Golpe de Escudo (daño = tu escudo): on a misty bridge her ward folds into the shield, which rams an
                      enemy sigil and bursts it in blue.
  martillo_sismico    Martillo Sísmico (18 a todos; Aguante: +2 Fuerza): her hammer strikes a canyon floor, cracks run
                      to three sigils and rocks burst up under them; the shield's gold pulse, two chevrons.
  pacto_hierro        Pacto de Hierro (pierdes 4 de vida; +1 de maná): her blood on a chained iron tablet; the runes
                      burn, the chains fall and a mana crystal rises.
  regeneracion        Regeneración (poder: al empezar tu turno te curas 3): in overgrown ruins vines bloom round her
                      heart on a green rune circle; the hourglass turns and it heals.
  represalia          Represalia (el doble de la vida perdida este turno): an enemy bolt draws her blood, the blood
                      doubles on the blade and comes back as a crimson cut.
  sacrificio          Sacrificio (16; pierdes 6 de vida; Furia: roba 1): under a red window her heart pours its blood on
                      the sword, which flies into a sigil; a card rises from the blood.
  segunda_piel        Segunda Piel (poder: cada vez que pierdes vida, 3 de escudo): her heart in a mesh of steel rings;
                      when a drop leaves it the mesh flares into a ward.
  torbellino_acero    Torbellino de Acero (7 a todos; Furia: cura 3 por golpe): on night dunes her blood on the sword,
                      which spins into a disc of steel through three sigils; a green cross for each.
  torre               Torre (16 de escudo): stone blocks stack up behind her shield into a tower, a tall ward.
  transfusion         Transfusión (pierdes tu escudo y te curas esa cantidad): in an alchemist's room the ward drains as
                      blue light along a glass coil into her heart and turns green.
"""
import math

import numpy as np

from pixelkit import Canvas, hexc, dilate, erode, shift
from rogue_art import (AW, AH, FRAMES, K, L, ease_out, ease_in_out, frac, gradient_bg, vignette, star4, motes,
                       outline_mask, crescent)
from mage_art import twinkling_stars, sparks, ellipse_ring, flame
from warrior_art import (KW, LW, shake, falling_sparks, dust_puffs, rising_chevrons, sword_labels, blade_point,
                         WARM_SPARKS, CYAN_SPARKS)
from rogue_pool_art import KB, LB, GOLD_SPARKS, mana_drop, speed_lines, converge, diamond_ring, sigil
from rogue_pool_art_2 import paint_card
from rogue_pool_art_3 import plus_marks, rising_card, ward_rings
from rogue_pool_art_4 import hourglass, comet
from rogue_pool_art_5 import power_ring
from rogue_pool_art_6 import ground_sigil
from warrior_pool_art import (KP, LP, BLOOD_SPARKS, HEAL_SPARKS, blood_drop, drip, splash, life_heart, heart_mask,
                              heal_crosses, blood_aura, gold_pulse, faint_ward, foe_sigil, sword, shield, shield_gem,
                              contact_shadow, floor_slabs)
from warrior_pool_art_2 import KP2, CRIM_SPARKS, fury_star, gold_star, heal_star

KP3 = dict(KP2)
KP3.update({
    'sand0': hexc('#120f1e'), 'sand1': hexc('#1e1a30'), 'sand2': hexc('#2c2644'), 'sand3': hexc('#40385e'),
    'rock0': hexc('#0e0c16'), 'rock1': hexc('#1a1626'), 'rock2': hexc('#28223a'), 'rock3': hexc('#3a3252'),
    'leaf0': hexc('#0c2214'), 'leaf1': hexc('#1a4426'), 'leaf2': hexc('#2e7a3a'), 'leaf3': hexc('#5cc060'),
    'bloom': hexc('#ffd0e6'), 'glassr': hexc('#7a1a2a'),
})
BLUE_SPARKS = (KB['b3'], KB['b2'], KB['b1'])


def blue_star(c, g, x, y, arm, gain=1.3):
    star4(c, g, x, y, arm, core=K['white'], col=KB['b3'], edge=KB['b2'], light=LB, gain=gain)


def impact(c, g, x, y, age, r=7, kind='cyan', gain=1.4):
    """The flash and star of a hit (age 0 = the hit frame); kind picks the colour."""
    pal = {'cyan': (K['cyan2'], K['cyan4'], L['cyan'], K['cyan3'], K['cyan2']),
           'crim': (KW['crim4'], KW['crim5'], LW['crimson'], KW['crim5'], KW['crim4']),
           'warm': (KW['spark1'], KW['spark2'], L['warm'], KW['spark2'], KW['spark1']),
           'blue': (KB['b2'], KB['b3'], LB, KB['b3'], KB['b2']),
           'gold': (KW['gold2'], KW['gold3'], LW['gold'], KW['gold3'], KW['gold2'])}[kind]
    if age == 0:
        c.put(c.disc(x, y, r), pal[0], 0.6)
        c.put(c.disc(x, y, r * 0.6), pal[1])
        c.put(c.disc(x, y, r * 0.3), K['white'])
        g.add(c.disc(x, y, r + 1), pal[2], gain)
    if 0 <= age <= 3:
        star4(c, g, x, y, {0: int(r * 1.7), 1: int(r * 1.1), 2: int(r * 0.7), 3: 2}[age], core=K['white'],
              col=pal[3], edge=pal[4], light=pal[2], gain=gain)


# ---------------------------------------------------------------------------------------- her kit, more
def cuirass(c, g, x, y, scales=0.0, flash=0.0, glow=0.0, t=0.0):
    """Her cuirass on a stand, from the front: steel breastplate with gold trim and a cyan gem, pauldrons, a crimson
    tabard below; scales 0..1 grows steel scales over it from the gem outwards (living armour)."""
    post = c.rect(int(x) - 1, int(y) + 12, int(x) + 1, 52)
    foot = c.rect(int(x) - 7, 51, int(x) + 7, 52)
    st = post | foot
    c.put(outline_mask(st), K['ink'])
    c.put(st, KP['oak1'])
    c.put(st & ~shift(st, 0, 1), KP['oak2'])
    dx, dy = c.x - x, c.y - y
    plate = (np.abs(dx) <= 9.5 - np.clip(dy - 2, 0, None) * 0.4) & (dy >= -9) & (dy <= 9)
    plate &= ~((dy < -6) & (np.abs(dx) < 3.6 - (dy + 9) * 0.3))
    paul = (np.hypot((dx + 10) / 5.0, (dy + 6) / 3.4) <= 1) | (np.hypot((dx - 10) / 5.0, (dy + 6) / 3.4) <= 1)
    tab = (dy > 8) & (dy <= 15) & (np.abs(dx) <= 6.5 - (dy - 8) * 0.2)
    tab &= ~((dy > 12) & (np.abs(dx) < (dy - 12) * 0.9))
    m = plate | paul | tab
    c.put(outline_mask(m), K['ink'])
    c.put(tab, KW['crim3'])
    c.put(tab & (dx < -2), KW['crim4'])
    c.put(tab & (dx > 3), KW['crim2'])
    sp = plate | paul
    c.put(sp, K['steel1'])
    c.put(sp & (dx + dy * 0.7 < -1), K['steel2'])
    c.put(sp & (dx + dy * 0.7 < -6), K['steel3'])
    c.put(sp & (dx > 5) & (dy > -3), K['steel0'])
    c.put(plate & (np.abs(dx) < 0.8) & (dy > -5) & (dy < 7), K['steel3'])
    trim = (sp & ~erode(sp)) | (plate & (np.abs(dy - 8.5) < 0.7))
    c.put(trim, KW['gold1'])
    c.put(trim & (dx + dy < 0), KW['gold2'])
    gem = np.abs(dx) / 1.6 + np.abs(dy + 1) / 1.8 <= 1
    c.put(gem, K['cyan2'])
    c.put(gem & (dx + dy + 1 < 0), K['cyan4'])
    g.add(gem, L['cyan'], 0.7 + glow)
    if scales > 0:
        r = np.hypot(dx, (dy + 1) * 1.2)
        ix = np.floor(c.x).astype(int)
        iy = np.floor(c.y).astype(int)
        row = iy // 3
        lx = (ix + (row % 2) * 2) % 4
        ly = iy % 3
        edge = ((ly < 2) & ((lx == 0) | (lx == 3))) | ((ly == 2) & ((lx == 1) | (lx == 2)))   # rows of "U" scales
        sc = sp & (r <= scales * 16) & ~gem
        c.put(sc, KW['plate2'])
        c.put(sc & (ly == 0) & (lx > 0) & (lx < 3), KW['plate3'])
        c.put(sc & edge, KW['plate0'])
        front = sp & (np.abs(r - scales * 16) <= 0.6)
        c.put(front, K['cyan3'])
        g.add(front, L['cyan'], 0.7)
        g.add(sc, LW['steel'], 0.15)
    if flash > 0:
        c.put(sp & ~erode(sp), K['white'], flash)
        g.add(sp, LW['steel'], 0.5 * flash)
    return m


def warhammer(c, g, hx, hy, ang, s=1.0, fury=0.0, glow=0.0):
    """Her war hammer held at (hx, hy) on the haft, the head along `ang`: a steel block with gold bands and a rune."""
    ux, uy = math.cos(ang), math.sin(ang)
    vx, vy = -uy, ux
    dx, dy = c.x - hx, c.y - hy
    u = (dx * ux + dy * uy) / s
    v = (dx * vx + dy * vy) / s
    haft = (u >= -9) & (u <= 26) & (np.abs(v) <= 1.3)
    wrap = haft & (u >= -6) & (u <= 3) & ((np.floor(u * 0.8) % 2) == 0)
    pom = np.hypot(u + 10, v) <= 1.8
    head = (u >= 22) & (u <= 31) & (np.abs(v) <= 7.0)
    faces = head & (np.abs(v) >= 5.6)
    band = head & (np.abs(v) < 5.6) & ((np.abs(u - 23.5) < 0.8) | (np.abs(u - 29.5) < 0.8))
    m = haft | pom | head
    c.put(outline_mask(m), K['ink'])
    c.put(haft, KP['oak2'])
    c.put(haft & (v < -0.3), KP['oak3'])
    c.put(wrap, KW['crim3'])
    c.put(pom, KW['gold2'])
    c.put(head, K['steel1'])
    c.put(head & (u < 26), K['steel2'])
    c.put(head & (u < 23.5) & (v < 0), K['steel3'])
    c.put(faces, K['steel2'])
    c.put(faces & (v < 0), K['steel4'])
    c.put(band, KW['gold2'])
    rune = head & (np.abs(u - 26.5) / 1.6 + np.abs(v) / 2.6 <= 1)
    c.put(rune, KW['crim5'] if fury > 0.5 else K['cyan2'])
    g.add(rune, LW['crimson'] if fury > 0.5 else L['cyan'], 0.8 + glow)
    return hx + ux * 26.5 * s, hy + uy * 26.5 * s


def iron_tablet(c, g, x, y, lit=0.0, chains=1.0, t=0.0):
    """A chained iron tablet on a stone lectern: an arched slab with three rows of runes; lit 0..1 burns them crimson,
    chains 0..1 how far the chains still hold it (they fall away)."""
    lect = c.poly([(x - 6, 52), (x - 3, 40), (x + 3, 40), (x + 6, 52)])
    c.put(outline_mask(lect), K['ink'])
    c.put(lect, KP['temple2'])
    c.put(lect & (c.x < x - 1), KP['temple3'])
    dx, dy = c.x - x, c.y - y
    slab = ((np.abs(dx) <= 8) & (dy >= -6) & (dy <= 10)) | ((np.hypot(dx, dy + 6) <= 8) & (dy < -6))
    c.put(outline_mask(slab), K['ink'])
    c.put(slab, KW['iron1'])
    c.put(slab & (dx < -4), KW['iron2'])
    c.put(slab & ~erode(slab), KW['iron3'])
    c.put(slab & (dx > 5), KW['iron0'])
    rng = np.random.default_rng(1201)
    runes = np.zeros((AH, AW), bool)
    for row in range(3):
        for col in range(4):
            rx, ry = x - 5 + col * 3.3, y - 6 + row * 5
            k = rng.integers(0, 4)
            if k == 0:
                runes |= c.line(rx, ry, rx, ry + 2) | c.pixel(rx + 1, ry)
            elif k == 1:
                runes |= c.line(rx - 1, ry, rx + 1, ry + 2)
            elif k == 2:
                runes |= c.line(rx, ry, rx, ry + 2) | c.pixel(rx + 1, ry + 1)
            else:
                runes |= c.line(rx - 1, ry + 2, rx + 1, ry) | c.pixel(rx - 1, ry)
    runes &= slab
    c.put(runes, KW['iron0'])
    if lit > 0:
        c.put(runes, KW['crim5'] if lit > 0.6 else KW['crim3'], min(1.0, lit * 1.5))
        g.add(runes, LW['crimson'], 0.9 * lit)
    for side in (-1, 1):                                               # the chains down to rings in the lectern
        if chains <= 0:
            continue
        x0, y0 = x + side * 8, y - 2
        x1, y1 = x + side * 13, 50
        n = 8
        for i in range(n):
            u = i / (n - 1)
            if u > chains:
                break
            px = x0 + (x1 - x0) * u
            py = y0 + (y1 - y0) * u + math.sin(u * math.pi) * 2
            link = c.disc(px, py, 1.3) & ~c.disc(px, py, 0.5)
            c.put(outline_mask(link), K['ink'])
            c.put(link, KW['iron3'])
    return slab


# ---------------------------------------------------------------------------------------- Conjuro de Combate
CC_GUARD = (25.0, 36.0)
CC_ANG = math.radians(-50)
CC_SIGIL = (71.0, 26.0)
CC_SHIELD = (17.0, 46.0)


def standing_stones(c, g, t):
    """A circle of standing stones on a moor under a violet sky with a faint nebula."""
    gradient_bg(c, K['vio1'], K['vio2'], K['vio0'], 0.6)
    c.put(c.disc(56, 14, 18), K['vio3'], 0.2)
    twinkling_stars(c, g, t, 1211, 18, (2, 2, 86, 32), light=L['violet'])
    moor = c.poly([(0, 46), (20, 43), (44, 45), (66, 42), (88, 45), (88, 64), (0, 64)])
    c.put(moor, K['vio0'])
    c.put(moor & ~shift(moor, 0, 1), K['vio3'], 0.7)
    for x, h, w in ((5, 14, 4), (37, 9, 3), (50, 10, 3), (84, 15, 4)):
        st = c.rect(x - w, 46 - h, x + w, 47) | (c.disc(x, 46 - h, w) & (c.y < 46 - h))
        c.put(outline_mask(st), K['ink'])
        c.put(st, KP3['rock2'])
        c.put(st & (c.x < x - 1), KP3['rock3'])
        r = c.pixel(x, 46 - h + 4)
        lit = 0.5 + 0.5 * math.sin((t * 2 + x * 0.05) * 2 * math.pi)
        c.put(r, K['cyan2'] if lit > 0.5 else K['cyan1'])
        g.add(r, L['cyan'], 0.4 * lit)
    c.put(c.rect(0, 52, AW - 1, AH - 1), K['vio0'])
    c.put(c.rect(0, 52, AW - 1, 52), K['vio3'], 0.5)


def conjuro_combate_frame(f):
    t = f / FRAMES
    c, g = Canvas(AW, AH), Canvas(AW, AH)
    standing_stones(c, g, t)
    hit = 11
    age = f - hit
    sx, sy = CC_SIGIL
    foe_sigil(c, g, sx, sy, t, hit=age if age >= 0 else -99, r=7.0)
    shx, shy = CC_SHIELD
    slab = shield(c, g, shx, shy, W=8.0, H=20.0, gem_gain=1.0 + (1.4 if 12 <= f <= 14 else 0),
                  flash={12: 0.4}.get(f, 0.0))
    gx, gy = CC_GUARD
    gy += math.sin(t * 2 * math.pi * 2) * 0.6
    tx, ty = blade_point(gx, gy, CC_ANG)
    # the tip draws a rune ring in the air (2-7), it flares (8) and fires (9-10)
    rx, ry = tx + 4, ty - 2
    draw = {2: 0.15, 3: 0.35, 4: 0.55, 5: 0.75, 6: 0.9, 7: 1.0, 8: 1.0, 9: 1.0, 10: 0.7, 11: 0.4, 12: 0.15}.get(f, 0.0)
    if draw > 0:
        ang = np.arctan2(c.y - ry, c.x - rx)
        prog = ((ang + math.pi / 2) % (2 * math.pi)) / (2 * math.pi)
        ring = (np.abs(np.hypot(c.x - rx, c.y - ry) - 6.5) <= 0.6) & (prog <= min(1.0, draw * 1.1))
        inner = (np.abs(np.hypot(c.x - rx, c.y - ry) - 4.0) <= 0.5) & (((ang / (2 * math.pi) * 8 + t * 2) % 1) < 0.5)
        fade = 1.0 if f <= 9 else draw
        c.put(ring, K['cyan3'], fade)
        c.put(inner & (draw >= 0.9), K['cyan2'], fade)
        g.add(ring, L['cyan'], 0.9 * fade)
        g.add(inner & (draw >= 0.9), L['cyan'], 0.5 * fade)
        for i in range(6):
            th = -math.pi / 2 + i / 6 * 2 * math.pi
            if i / 6 <= draw:
                m = c.pixel(rx + math.cos(th) * 8.5, ry + math.sin(th) * 8.5)
                c.put(m, K['cyan4'], fade)
                g.add(m, L['cyan'], 0.7 * fade)
    sword(c, g, gx, gy, CC_ANG, t * 2 + (1.5 if 3 <= f <= 10 else 0), rune_gain=1.0 + (1.2 if 3 <= f <= 10 else 0),
          glint=(-4 + (f - 16) * 9.0 if 16 <= f <= 20 else None))
    if 2 <= f <= 7:
        star4(c, g, tx, ty, 2, gain=0.9)
    if f == 8:
        star4(c, g, rx, ry, 8, gain=1.4)
    if f in (9, 10):                                                   # the bolt
        k = (f - 8) / 3.0
        bx, by = rx + (sx - 2 - rx) * k * 1.5, ry + (sy - ry) * k * 1.5
        comet(c, g, bx, by, rx, ry, (K['cyan1'], K['cyan2'], K['cyan4']), L['cyan'], r=2.2)
        speed_lines(c, g, bx, by, (sx - rx) / 50, (sy - ry) / 50, 1.0, 1213, count=5, spread=3, length=(4, 8), back=6)
    impact(c, g, sx, sy, age, r=7)
    sparks(c, g, f, hit, sx, sy, 1215, 12, (1.0, 2.6), 7, CYAN_SPARKS, L['cyan'], gravity=0.12)
    if 0 <= f - 12 <= 7:
        ward_rings(c, g, shx, shy + 1, f - 12, size=12, squash=1.1, life=8)
    motes(c, g, t, 1217, 7, (4, 6, 84, 46), K['cyan3'], K['violet3'], L['cyan'], gain=0.45)
    vignette(c, 0.72)
    if f == hit:
        shake(c, g, 1, 0)
    return c, g


# ---------------------------------------------------------------------------------------- Coraza Viva
CV_CUIRASS = (56.0, 27.0)
CV_HEART = (20.0, 24.0)


def order_hall(c, g, t):
    """The hall of her order: stone pillars, hanging crimson banners with a gold sword, a cold window light."""
    gradient_bg(c, K['teal0'], K['teal1'], K['night0'], 0.6)
    back = c.rect(0, 0, AW - 1, 51)
    c.put(back, KW['stone1'])
    lit = np.clip(1 - np.hypot(c.x - 56, c.y - 10) / 38, 0, 1)
    c.put(back, KW['stone2'], np.clip(lit * 1.5, 0, 1))
    for x in (4, 36, 78):
        p = c.rect(x, 0, x + 6, 51)
        c.put(p, KW['stone0'])
        c.put(c.rect(x, 0, x, 51), KW['stone2'])
        c.put(c.rect(x + 6, 0, x + 6, 51), K['ink'])
    for x in (44, 70):
        bn = c.rect(x, 4, x + 7, 22)
        bn &= ~((c.y > 19) & (np.abs(c.x - x - 3.5) < (c.y - 19) * 1.2))
        c.put(outline_mask(bn), K['ink'])
        c.put(bn, KW['crim2'])
        c.put(bn & (c.x < x + 2), KW['crim3'])
        emb = bn & (((np.abs(c.x - x - 3.5) < 0.6) & (c.y > 7) & (c.y < 17)) | ((np.abs(c.y - 10) < 0.6) & (np.abs(c.x - x - 3.5) < 2.5)))
        c.put(emb, KW['gold2'])
        c.put(c.rect(x - 1, 3, x + 8, 3), KW['gold1'])
    floor_slabs(c, 52)


def coraza_viva_frame(f):
    t = f / FRAMES
    c, g = Canvas(AW, AH), Canvas(AW, AH)
    order_hall(c, g, t)
    x, y = CV_CUIRASS
    grow = 0.0 if f < 7 else min(1.0, (f - 6) / 6.0)
    if f >= 20:
        grow = max(0.0, 1 - (f - 19) / 4.0)
    lock = 12
    m = cuirass(c, g, x, y, scales=grow, flash={12: 0.6, 13: 0.25}.get(f, 0.0), glow=0.8 if 6 <= f <= 12 else 0.0,
                t=t)
    hx, hy = CV_HEART
    hy += math.sin(t * 2 * math.pi * 2) * 0.6
    life_heart(c, g, hx, hy, 1.15, fill=0.3, crack=0.6)
    # what she has lost (the dark part of her heart) flows into the armour as dark-red motes
    if 3 <= f <= 11:
        rng = np.random.default_rng(1221)
        for i in range(12):
            ph = rng.random()
            u = frac((f - 3) / 9.0 * 1.5 + ph)
            px = hx + 3 + (x - hx - 3) * u
            py = hy - 3 + (y - 1 - hy + 3) * u - math.sin(u * math.pi) * 8
            mm = c.pixel(px, py)
            c.put(mm, KW['crim4'] if i % 2 else KP['glass_r1'])
            g.add(mm, LW['crimson'], 0.6)
    if f == lock:
        blue_star(c, g, x, y - 1, 8)
    if 0 <= f - lock <= 8:
        ward_rings(c, g, x, y + 2, f - lock, size=22, squash=1.0, life=9)
    sparks(c, g, f, lock, x, y, 1223, 10, (0.6, 1.6), 6, BLUE_SPARKS, LB, gravity=-0.03, gain=0.8)
    motes(c, g, t, 1225, 6, (6, 6, 82, 48), KW['plate3'], KW['crim4'], LW['steel'], gain=0.35)
    vignette(c, 0.72)
    return c, g


# ---------------------------------------------------------------------------------------- Golpe Pesado
GP_PIVOT = (22.0, 47.0)
GP_IDLE = math.radians(-62)
GP_RAISE = math.radians(-112)
GP_HIT = math.radians(4)
GP_BLOCK = (66.0, 52.0)            # bottom centre of the block


def quarry(c, g, t):
    """A quarry at night: terraced rock walls, a crane of timber, blocks lying about."""
    gradient_bg(c, K['night0'], K['night2'], K['night1'], 0.6)
    twinkling_stars(c, g, t, 1231, 12, (2, 2, 86, 16))
    for i, (y, x0) in enumerate(((18, 0), (28, 6), (38, 0))):
        terr = c.poly([(x0, y), (88, y - 3), (88, y + 10), (0, y + 12)])
        c.put(terr, KP3['rock1'] if i % 2 else KP3['rock2'])
        c.put(terr & ~shift(terr, 0, 1), KP3['rock3'])
        for x in range(4 + i * 5, AW, 13):
            c.put(c.line(x, y + 1, x + 1, y + 7) & terr, KP3['rock0'])
    crane = c.line(80, 48, 80, 14) | c.line(80, 14, 68, 18) | c.line(80, 22, 72, 17) | c.line(68, 18, 68, 24)
    c.put(crane | shift(crane, 1, 0), K['roof'])
    for x, w in ((6, 6), (12, 4)):
        b = c.rect(x, 46, x + w, 51)
        c.put(outline_mask(b), K['ink'])
        c.put(b, KP3['rock2'])
        c.put(c.rect(x, 46, x + w, 46), KP3['rock3'])
    c.put(c.rect(0, 52, AW - 1, AH - 1), KP3['rock0'])
    c.put(c.rect(0, 52, AW - 1, 52), KP3['rock3'])
    for i in range(10):
        x = (i * 31) % AW
        c.put(c.pixel(x, 55 + (i * 3) % 7), KP3['rock2'])


def stone_block(c, g, x, base, t, keep=None, dx=0.0, rot=0.0, seal=0.0):
    """A big quarried block with an enemy sigil cut into it; seal 0..1 = violet light along the split."""
    cx, cy = x + dx, base
    co, si = math.cos(rot), math.sin(rot)
    lx = (c.x - cx) * co + (c.y - cy) * si
    ly = -(c.x - cx) * si + (c.y - cy) * co
    m = (np.abs(lx) <= 11) & (ly <= 0) & (ly >= -20)
    if keep is not None:
        m &= keep
    if not m.any():
        return m
    c.put(outline_mask(m), K['ink'])
    c.put(m, KP3['rock2'])
    c.put(m & (lx < -6), KP3['rock3'])
    c.put(m & (lx > 7), KP3['rock1'])
    c.put(m & (ly < -19), KP3['rock3'])
    c.put(m & (np.abs(ly + 10) < 0.5) & (np.abs(lx) > 7), KP3['rock1'])
    rr = np.hypot(lx, ly + 11)
    ring = m & (np.abs(rr - 5) < 0.7) & (((np.arctan2(ly + 11, lx) / (2 * math.pi) * 8 + t * 2) % 1) < 0.65)
    eye = m & (rr < 1.4)
    c.put(ring, KW['crim3'])
    c.put(eye, KW['crim4'])
    g.add(ring | eye, LW['crimson'], 0.5)
    return m


def gp_angle(f):
    keys = {5: -70, 6: -82, 7: -94, 8: -104, 9: -110, 10: -112, 11: -60, 12: 4, 13: 6, 14: 5, 15: 4}
    if f <= 4:
        return GP_IDLE
    if f in keys:
        return math.radians(keys[f])
    return GP_HIT + (GP_IDLE - GP_HIT) * ease_in_out((f - 15) / 7.0) if f <= 22 else GP_IDLE


def golpe_pesado_frame(f):
    t = f / FRAMES
    c, g = Canvas(AW, AH), Canvas(AW, AH)
    quarry(c, g, t)
    hit = 12
    age = f - hit
    bx, base = GP_BLOCK
    if f < hit or age >= 11:
        stone_block(c, g, bx, base, t)
    else:
        if age <= 4:
            spread = ease_out(age / 4.0) * 3.5
        elif age <= 7:
            spread = 3.5
        else:
            spread = 3.5 * (1 - ease_in_out((age - 7) / 3.0))
        left = c.x < bx + 0.5
        stone_block(c, g, bx, base, t, keep=left, dx=-spread, rot=-0.06 * spread)
        stone_block(c, g, bx, base, t, keep=~left, dx=spread, rot=0.06 * spread)
        if age >= 6:                                                   # enemy magic draws them together
            k = 1 - abs(age - 9) / 4.0
            seam = c.rect(int(bx) - 1, int(base) - 21, int(bx) + 1, int(base))
            c.put(seam, K['violet3'], max(0.0, k))
            g.add(dilate(seam), L['violet'], 0.7 * max(0.0, k))
            for i in range(5):
                yy = base - 4 - i * 4
                m = c.pixel(bx - spread - 3 + (age - 6) * 0.8, yy) | c.pixel(bx + spread + 3 - (age - 6) * 0.8, yy)
                c.put(m, K['violet4'], max(0.0, k))
                g.add(m, L['violet'], 0.6 * max(0.0, k))
    px, py = GP_PIVOT
    py += math.sin(t * 2 * math.pi * 2) * 0.6 if (f <= 4 or f >= 21) else 0.0
    ang = gp_angle(f)
    R = (5 + 34) * 1.2
    cpal = (K['cyan1'], K['cyan2'], K['steel3'], K['white'])
    if f == 12:
        crescent(c, g, px, py, R + 0.5, -ang, math.radians(116), -1, 13.0, cpal, L['cyan'], gain=1.6)
    elif 13 <= f <= 16:
        q = (f - 13) / 4.0
        crescent(c, g, px, py, R + 0.5, -GP_HIT, math.radians(116) * (1 - 0.6 * q), -1, 11 * (1 - 0.6 * q), cpal,
                 L['cyan'], a=0.85 - 0.6 * q, dash=f >= 15, gain=1.3 * (1 - q))
    if 8 <= f <= 10:                                                   # the weight gathers in the blade
        gx_, gy_ = px + math.cos(ang) * 6, py + math.sin(ang) * 6
        converge(c, g, f, *blade_point(gx_, gy_, ang, s=1.2), 8, 3, 1233, 10, 12, (K['cyan3'], K['steel3']), L['cyan'])
    gx, gy = px + math.cos(ang) * 6, py + math.sin(ang) * 6
    sword(c, g, gx, gy, ang, t * 2 + (1.5 if 8 <= f <= 12 else 0), s=1.2,
          rune_gain=1.0 + (1.3 if 8 <= f <= 12 else 0), glint=(-4 + (f - 17) * 9.0 if 17 <= f <= 21 else None))
    ix, iy = bx, base - 20
    impact(c, g, ix, iy, age, r=8)
    if 0 <= age <= 8:
        rng = np.random.default_rng(1235)
        for i in range(12):
            a0 = rng.uniform(0.25, math.pi - 0.25)
            spd = rng.uniform(1.2, 2.6)
            x = ix + math.cos(a0) * spd * age
            y = iy - math.sin(a0) * spd * age + 0.32 * age * age
            if y < 52 and age > 0:
                m = c.rect(int(x), int(y), int(x) + (i % 2), int(y) + ((i + age) % 2))
                c.put(outline_mask(m), K['ink'], 0.8)
                c.put(m, KP3['rock3'] if i % 3 else K['dust1'])
    falling_sparks(c, g, f, hit, ix, iy, 1237, 10, (1.2, 2.8), 9, WARM_SPARKS, L['warm'], 52, (0.3, math.pi - 0.3))
    dust_puffs(c, bx, base - 1, f, hit, 6, 2.6, rise=0.5, life=8, seed=1239, big=1.2)
    motes(c, g, t, 1241, 6, (4, 6, 84, 46), K['cyan3'], K['steel3'], L['cyan'], gain=0.4)
    vignette(c, 0.72)
    if f == hit:
        shake(c, g, 0, 2)
    elif f == hit + 1:
        shake(c, g, -1, 1)
    elif f == hit + 2:
        shake(c, g, 1, 0)
    return c, g


# ---------------------------------------------------------------------------------------- Golpe Total
GT_C = (44.0, 33.0)
GT_SIGILS = ((14.0, 18.0), (75.0, 20.0), (64.0, 52.0))


def crossroads(c, g, t):
    """A crossroads at night: three roads meeting, a broken signpost, a lantern on a post, dry grass."""
    gradient_bg(c, K['night0'], K['night2'], K['teal1'], 0.6)
    twinkling_stars(c, g, t, 1251, 16, (2, 2, 86, 26), avoid=(44, 33, 22))
    hills = c.poly([(0, 34), (22, 31), (44, 33), (66, 30), (88, 33), (88, 64), (0, 64)])
    c.put(hills, KP2['grass0'])
    c.put(hills & ~shift(hills, 0, 1), KP2['grass2'])
    road = (np.abs(c.x - 44 - (c.y - 33) * 0.0) < 3 + (c.y - 33) * 0.35) & (c.y > 33)
    road |= (np.abs((c.y - 40) - (c.x - 44) * 0.25) < 2.5) & (c.y > 33)
    c.put(road, KP['oak1'])
    c.put(road & ((np.floor(c.x * 0.7 + c.y * 1.3) % 5) == 0), KP['oak2'])
    post = c.rect(20, 34, 21, 50)
    c.put(post, KP['oak2'])
    c.put(c.poly([(13, 37), (21, 36), (21, 39), (14, 40)]), KP['oak3'])
    c.put(c.poly([(21, 42), (28, 43), (27, 46), (21, 45)]), KP['oak2'])
    for i in range(12):
        x = (i * 37) % AW
        y = 38 + (i * 11) % 22
        c.put(c.line(x, y, x + 1, y - 2), KP2['grass2'])


def golpe_total_frame(f):
    t = f / FRAMES
    c, g = Canvas(AW, AH), Canvas(AW, AH)
    crossroads(c, g, t)
    cx, cy = GT_C
    spin0, spin1 = 9, 13                                               # the full turn
    R = 36.0
    hits = []
    for (sx, sy) in GT_SIGILS:
        th = math.atan2(sy - cy, sx - cx)
        hits.append(th)
    def blade_ang(f):
        if f <= 6:
            return math.radians(-60)
        if f <= 8:
            return math.radians(-60 - 35 * ease_in_out((f - 6) / 2.0))
        if f <= spin1:
            k = (f - 8) / (spin1 - 8)
            return math.radians(-95 + 360 * ease_out(k) + 0)
        if f <= 15:
            return math.radians(-95 + 360 + 8 * (f - spin1))
        k = (f - 15) / 8.0
        return math.radians(281 + (300 - 281) * ease_in_out(k))
    ang = blade_ang(f)
    hit_frames = []
    for (sx, sy) in GT_SIGILS:
        target = (math.degrees(math.atan2(sy - cy, sx - cx)) + 95) % 360
        hf = spin1
        for ff in range(8, spin1 + 1):
            if math.degrees(blade_ang(ff)) + 95 >= target:
                hf = ff
                break
        hit_frames.append(hf)
    for i, (sx, sy) in enumerate(GT_SIGILS):
        a = f - hit_frames[i]
        foe_sigil(c, g, sx, sy, t + i * 0.25, hit=a if a >= 0 else -99, r=6.0)
    if spin0 <= f <= 15:                                               # the ring of the spin
        prev = blade_ang(f - 1)
        length = min(2 * math.pi, abs(ang - prev) + 0.6)
        k = 1.0 if f <= spin1 else 1 - (f - spin1) / 3.0
        crescent(c, g, cx, cy, R, -ang, length * (1.4 if f <= spin1 else 1.0), 1, 8.0 * k,
                 (K['cyan1'], K['cyan2'], K['steel3'], K['white']), L['cyan'], a=k, gain=1.4 * k, dash=f > spin1)
    if f == spin1 + 1:
        ring = np.abs(np.hypot(c.x - cx, c.y - cy) - R) <= 1.0
        c.put(ring, K['cyan2'], 0.7)
        g.add(ring, L['cyan'], 0.6)
    gx, gy = cx + math.cos(ang) * 3, cy + math.sin(ang) * 3
    bob = math.sin(t * 2 * math.pi * 2) * 0.6 if (f <= 6 or f >= 21) else 0.0
    sword(c, g, gx, gy + bob, ang, t * 2 + (1.5 if 7 <= f <= 14 else 0), rune_gain=1.0 + (1.2 if 7 <= f <= 14 else 0))
    for i, (sx, sy) in enumerate(GT_SIGILS):
        a = f - hit_frames[i]
        impact(c, g, sx, sy, a, r=5)
        sparks(c, g, f, hit_frames[i], sx, sy, 1253 + i, 8, (0.8, 2.0), 6, CYAN_SPARKS, L['cyan'], gravity=0.12)
    motes(c, g, t, 1259, 6, (4, 6, 84, 50), K['cyan3'], K['steel3'], L['cyan'], gain=0.4)
    vignette(c, 0.72)
    if f == spin1:
        shake(c, g, 1, 0)
    return c, g


# ---------------------------------------------------------------------------------------- Golpe de Escudo
GE_SHIELD = (24.0, 32.0)
GE_SIGIL = (72.0, 30.0)


def misty_bridge(c, g, t):
    """A narrow stone bridge over a chasm in the mist: parapets, a far gatehouse, fog drifting below."""
    gradient_bg(c, K['teal1'], K['teal2'], K['teal0'], 0.6)
    gh = c.rect(78, 14, 88, 46) | c.rect(76, 12, 88, 14)
    for x in (76, 80, 84):
        gh |= c.rect(x, 9, x + 2, 12)
    c.put(gh, K['teal0'])
    c.put(c.rect(80, 30, 85, 46), K['ink'])
    for i, y in enumerate((22, 30, 38)):
        off = (t * (1 + i % 2) * AW * (1 if i % 2 else -1)) % AW
        band = (np.abs(c.y - y) <= 2) & (((c.x + off + i * 17) % 37) < 24)
        c.put(band, K['teal3'], 0.35)
    deck = c.rect(0, 46, AW - 1, 49)
    c.put(deck, KW['stone2'])
    c.put(c.rect(0, 46, AW - 1, 46), KW['stone3'])
    for x in range(0, AW, 8):
        c.put(c.rect(x, 47, x, 49), KW['stone0'])
    arch = c.rect(0, 50, AW - 1, 54) & ~((np.hypot((c.x - 44) / 30.0, (c.y - 60) / 9.0)) <= 1)
    c.put(arch, KW['stone1'])
    c.put(c.rect(0, 55, AW - 1, AH - 1) & ~arch, K['teal0'])
    for i, y in enumerate((57, 61)):
        off = (t * AW * (1 + i)) % AW
        band = (np.abs(c.y - y) <= 1.5) & (((c.x + off) % 29) < 18)
        c.put(band, K['teal3'], 0.4)
    par = c.rect(0, 42, AW - 1, 45)
    for x in range(2, AW, 9):
        par &= ~c.rect(x, 42, x + 3, 43)
    c.put(par, KW['stone1'])
    c.put(par & ~shift(par, 0, 1), KW['stone3'])


def golpe_escudo_frame(f):
    t = f / FRAMES
    c, g = Canvas(AW, AH), Canvas(AW, AH)
    misty_bridge(c, g, t)
    hit = 11
    age = f - hit
    sx, sy = GE_SIGIL
    foe_sigil(c, g, sx, sy, t, hit=age if age >= 0 else -99, r=7.0)
    x, y = GE_SHIELD
    dx = {8: -2, 9: -3, 10: 14, 11: 30, 12: 30, 13: 28, 14: 24, 15: 18, 16: 12, 17: 7, 18: 3, 19: 1}.get(f, 0)
    bob = math.sin(t * 2 * math.pi * 2) * 0.6 if (f <= 7 or f >= 20) else 0.0
    shx, shy = x + dx, y + bob
    charged = 5 <= f <= 11
    if f == 10:
        speed_lines(c, g, shx - 8, shy, 1, 0, 1.0, 1261, count=7, spread=10, length=(6, 12), back=4)
    slab = shield(c, g, shx, shy, W=11.0, H=28.0, sx={11: 0.9}.get(f, 1.0),
                  gem_gain=1.0 + (1.8 if charged else 0), flash={11: 0.5}.get(f, 0.0))
    m = slab > 0
    # the ward it holds (its block): big, folds into the shield's face (5-9) and is spent on the hit
    size = {5: 13.0, 6: 10.0, 7: 7.0, 8: 4.0, 9: 1.5}.get(f, 15.0)
    if f <= 4 or f >= 20 or 5 <= f <= 9:
        a = 1.0 if f <= 9 else {20: 0.3, 21: 0.6, 22: 0.85, 23: 1.0}.get(f, 1.0)
        for i, (r, w) in enumerate(((5 + size, 0.6), (3 + size * 0.65, 0.5))):
            ring = diamond_ring(c, shx, shy + 2, r, w, squash=1.15)
            c.put(ring, KB['b2'] if i == 0 else KB['b1'], a)
            g.add(ring, LB, (0.6 if i == 0 else 0.3) * a)
    if charged:
        c.put(m & (slab == 3), KB['b2'], 0.35 + 0.1 * (f - 5))
        g.add(m, LB, 0.15 + 0.08 * (f - 5))
    ix, iy = sx - 8, sy
    impact(c, g, ix + 3, iy, age, r=8, kind='blue')
    if 0 <= age <= 5:                                                   # the ward bursts outward in blue
        ring = diamond_ring(c, ix + 3, iy, 6 + age * 5, 0.7, squash=1.0)
        c.put(ring, KB['b3'] if age < 2 else KB['b2'], 1 - age / 6)
        g.add(ring, LB, 0.9 * (1 - age / 6))
    sparks(c, g, f, hit, ix + 3, iy, 1263, 14, (1.2, 2.8), 7, BLUE_SPARKS, LB, gravity=0.1)
    motes(c, g, t, 1265, 6, (4, 6, 84, 44), KB['b2'], K['cyan3'], LB, gain=0.4)
    vignette(c, 0.72)
    if f == hit:
        shake(c, g, 2, 0)
    elif f == hit + 1:
        shake(c, g, -1, 0)
    return c, g


# ---------------------------------------------------------------------------------------- Martillo Sísmico
MS_HANDS = (20.0, 42.0)
MS_STRIKE = (36.0, 52.0)
MS_SIGILS = ((52.0, 55.0), (66.0, 57.0), (80.0, 55.0))
MS_SHIELD = (9.0, 44.0)


def canyon(c, g, t):
    """A canyon at night: steep walls on both sides, a strip of sky with stars, the floor of cracked rock."""
    gradient_bg(c, K['night0'], K['vio1'], K['night0'], 0.6)
    twinkling_stars(c, g, t, 1271, 14, (20, 2, 70, 22), light=L['violet'])
    left = c.poly([(0, 0), (18, 0), (24, 14), (20, 28), (28, 40), (24, 52), (0, 52)])
    right = c.poly([(88, 0), (70, 0), (64, 12), (70, 26), (60, 38), (66, 52), (88, 52)])
    for w in (left, right):
        c.put(w, KP3['rock1'])
        c.put(w & ~shift(w, 0, 1), KP3['rock2'])
        c.put(outline_mask(w) & ~w, KP3['rock2'], 0.6)
    for y in range(6, 50, 7):
        c.put(left & (np.abs(c.y - y) < 0.5), KP3['rock0'])
        c.put(right & (np.abs(c.y - y - 3) < 0.5), KP3['rock0'])
    floor = c.rect(0, 50, AW - 1, AH - 1)
    c.put(floor, KP3['rock1'])
    c.put(c.rect(0, 50, AW - 1, 50), KP3['rock3'])
    for i in range(14):
        x = (i * 29) % AW
        c.put(c.line(x, 53 + (i * 3) % 9, x + 3, 53 + (i * 3) % 9), KP3['rock0'])


def ms_angle(f):
    keys = {5: -70, 6: -85, 7: -100, 8: -112, 9: -118, 10: -120, 11: -60, 12: 22, 13: 24, 14: 23, 15: 22}
    if f <= 4:
        return math.radians(-62)
    if f in keys:
        return math.radians(keys[f])
    return math.radians(22 + (-62 - 22) * ease_in_out((f - 15) / 7.0)) if f <= 22 else math.radians(-62)


def martillo_sismico_frame(f):
    t = f / FRAMES
    c, g = Canvas(AW, AH), Canvas(AW, AH)
    canyon(c, g, t)
    hit = 12
    age = f - hit
    sx_, sy_ = MS_SHIELD
    slab = shield(c, g, sx_, sy_, W=6.5, H=16.0, gem_gain=1.0 + (1.5 if 14 <= f <= 16 else 0))
    faint_ward(c, g, slab > 0, t, a=0.6)
    hx, hy = MS_HANDS
    hy += math.sin(t * 2 * math.pi * 2) * 0.6 if (f <= 4 or f >= 21) else 0.0
    ang = ms_angle(f)
    if f == 12:
        crescent(c, g, hx, hy, 33, -ang, math.radians(130), -1, 11.0,
                 (K['cyan1'], K['cyan2'], K['steel3'], K['white']), L['cyan'], gain=1.5)
    fury = 1.0 if 17 <= f <= 21 else 0.0
    warhammer(c, g, hx, hy, ang, s=1.15, fury=fury, glow=0.8 if 8 <= f <= 12 else 0.0)
    ix, iy = MS_STRIKE
    # cracks run along the floor from the strike to each sigil; rocks burst up under them
    for i, (sx, sy) in enumerate(MS_SIGILS):
        reach = hit + 1 + i
        a = f - reach
        prog = min(1.0, max(0.0, (f - hit) / (1.0 + i)))
        if 0 <= f - hit <= 10:
            k = 1 - max(0, f - hit - 6) / 5
            pts = [(ix, iy)]
            rng = np.random.default_rng(1281 + i)
            for s_ in range(1, 5):
                u = s_ / 4
                pts.append((ix + (sx - ix) * u + rng.uniform(-1, 1), iy + (sy - iy) * u + rng.uniform(-1, 1)))
            n = max(1, int(round(prog * 4)))
            for (a0, b0), (a1, b1) in zip(pts[:n], pts[1:n + 1]):
                m = c.line(a0, b0, a1, b1) & (c.y >= 50)
                c.put(m, K['ink'], k)
                c.put(m & ~shift(m, 0, 1), K['cyan2'], k * 0.9)
                g.add(m, L['cyan'], 0.6 * k)
        foe_sigil(c, g, sx, sy - 17, t + i * 0.3, hit=(a - 1) if a >= 1 else -99, r=5.0)
        if 0 <= a <= 6:                                                 # a spike of rock bursts up into it
            h = {0: 7, 1: 13, 2: 12, 3: 10, 4: 7, 5: 4, 6: 1}[a]
            spike = c.poly([(sx - 3, sy), (sx - 0.5, sy - h), (sx + 0.5, sy - h - 1), (sx + 3, sy)])
            c.put(outline_mask(spike), K['ink'])
            c.put(spike, KP3['rock3'])
            c.put(spike & (c.x < sx), K['dust1'])
            impact(c, g, sx, sy - 17, a - 1, r=5)
            dust_puffs(c, sx, sy - 1, f, reach, 2, 1.4, rise=0.5, life=5, seed=1285 + i, big=0.8)
    impact(c, g, ix, iy - 1, age, r=8)
    if 0 <= age <= 5:
        ring = ellipse_ring(c, ix, iy, 4 + age * 5, 1 + age * 1.0, 0.6) & (c.y > 48)
        c.put(ring, K['cyan3'], 1 - age / 6)
        g.add(ring, L['cyan'], 0.8 * (1 - age / 6))
    falling_sparks(c, g, f, hit, ix, iy - 1, 1287, 10, (1.2, 2.6), 8, WARM_SPARKS, L['warm'], 53, (0.3, math.pi - 0.3))
    dust_puffs(c, ix, iy - 1, f, hit, 5, 2.4, rise=0.5, life=7, seed=1289, big=1.1)
    # Aguante: the shield's gold pulse to the hammer, +2 de Fuerza
    gx0, gy0 = shield_gem(sx_, sy_, 16.0)
    hhx, hhy = hx + math.cos(ang) * 4, hy + math.sin(ang) * 4
    gold_pulse(c, g, gx0, gy0, hhx, hhy, {14: 0.0, 15: 0.5, 16: 1.0}.get(f, -1))
    if f == 17:
        fury_star(c, g, hhx, hhy, 6)
    for side, born in ((-1, 17), (1, 18)):
        rising_chevrons(c, g, f, hhx + side * 6, hhy - 8, born, 5, KW['crim5'], LW['crimson'], count=2, gain=0.9)
    motes(c, g, t, 1291, 6, (24, 6, 64, 46), K['cyan3'], K['violet3'], L['cyan'], gain=0.4)
    vignette(c, 0.72)
    if f == hit:
        shake(c, g, 0, 2)
    elif f == hit + 1:
        shake(c, g, 1, -1)
    elif f == hit + 2:
        shake(c, g, -1, 1)
    return c, g


# ---------------------------------------------------------------------------------------- Pacto de Hierro
PI_TABLET = (44.0, 24.0)


def iron_shrine(c, g, t):
    """A shrine of black iron: chained walls, two iron braziers with cold blue fire, a grate floor."""
    gradient_bg(c, K['night0'], K['night1'], K['night0'], 0.6)
    back = c.rect(0, 0, AW - 1, 51)
    c.put(back, KW['iron0'])
    for x in range(0, AW, 11):
        c.put(c.rect(x, 0, x, 51), K['ink'])
        for y in range(4, 50, 9):
            c.put(c.pixel(x + 2, y) | c.pixel(x + 8, y), KW['iron1'])
    for x in (8, 80):
        for y in range(2, 50, 3):
            link = c.disc(x, y, 1.2) & ~c.disc(x, y, 0.4)
            c.put(link, KW['iron2'])
    for i, x in enumerate((20.0, 68.0)):
        bowl = c.rect(int(x) - 3, 40, int(x) + 3, 42) | c.rect(int(x) - 1, 42, int(x) + 1, 51)
        c.put(outline_mask(bowl), K['ink'])
        c.put(bowl, KW['iron2'])
        fl = np.zeros((AH, AW), bool)
        for k in range(3):
            h = 5 + 1.5 * math.sin(t * 4 * math.pi + i + k)
            fl |= c.poly([(x - 2.5 + k * 1.5, 40), (x - 1.5 + k * 1.5 + math.sin(t * 6 * math.pi + k) * 0.6, 40 - h),
                          (x - 0.5 + k * 1.5, 40)])
        c.put(fl, K['cyan1'])
        c.put(fl & (c.y > 37), K['cyan2'])
        g.add(fl, L['cyan'], 0.7)
        g.add(c.disc(x, 37, 8) & ~fl, L['cyan'], 0.05)
    floor = c.rect(0, 52, AW - 1, AH - 1)
    c.put(floor, KW['iron1'])
    c.put(c.rect(0, 52, AW - 1, 52), KW['iron3'])
    for x in range(2, AW, 4):
        c.put(c.rect(x, 53, x, AH - 1), K['ink'])


def pacto_hierro_frame(f):
    t = f / FRAMES
    c, g = Canvas(AW, AH), Canvas(AW, AH)
    iron_shrine(c, g, t)
    x, y = PI_TABLET
    lit = 0.0
    if f >= 6:
        lit = min(1.0, (f - 5) / 4.0)
    if f >= 19:
        lit = max(0.0, 1 - (f - 18) / 4.0)
    chains = 1.0 if f < 10 else max(0.0, 1 - (f - 9) / 3.0)
    if f >= 20:
        chains = min(1.0, (f - 19) / 4.0)
    jolt = 1 if f in (10, 11) else 0
    iron_tablet(c, g, x + jolt, y, lit=lit, chains=chains, t=t)
    for side in (-1, 1):                                               # the broken chains fall
        a = f - 10
        if 0 <= a <= 6:
            for i in range(4):
                px = x + side * (9 + i * 1.3) + side * a * 0.6
                py = y + 2 + i * 4 + a * a * 0.6
                link = c.disc(px, py, 1.3) & ~c.disc(px, py, 0.5)
                c.put(outline_mask(link), K['ink'], 1 - a / 7)
                c.put(link, KW['iron3'], 1 - a / 7)
    land = drip(c, g, f, 2, x + 0.5, -3, y - 12, fall=3, s=1.4, swell=1, seed=1301)
    if land == 0:
        fury_star(c, g, x, y - 12, 5)
    if f == 10:
        fury_star(c, g, x, y + 2, 9, gain=1.5)
    # the pact pays: a mana crystal rises out of the tablet
    a = f - 11
    if 0 <= a <= 9:
        k = ease_out(min(1.0, (a + 1) / 5.0))
        my = y - 2 - k * 16
        mana_drop(c, g, x, my + math.sin(a * 0.8) * 0.5, size=1.4, gain=1.2)
        if a == 4:
            star4(c, g, x, my, 5, core=K['white'], col=KP['mana3'], edge=KP['mana2'], light=LP['mana'], gain=1.2)
    sparks(c, g, f, 10, x, y, 1303, 12, (0.8, 2.0), 7, CRIM_SPARKS, LW['crimson'], gravity=0.06)
    motes(c, g, t, 1305, 6, (6, 6, 82, 48), KW['crim4'], KP['mana2'], LW['crimson'], gain=0.4)
    vignette(c, 0.72)
    if f == 10:
        shake(c, g, 1, 0)
    return c, g


# ---------------------------------------------------------------------------------------- Regeneración
RG_HEART = (46.0, 28.0)
RG_GLASS = (16.0, 40.0)


def overgrown_ruins(c, g, t):
    """Ruins swallowed by the forest: a broken arch wrapped in ivy, ferns, moonlight through leaves."""
    gradient_bg(c, K['teal0'], K['teal2'], K['night0'], 0.6)
    twinkling_stars(c, g, t, 1311, 10, (2, 2, 86, 18), avoid=(46, 26, 18))
    arch = (c.rect(26, 10, 32, 51) | c.rect(60, 10, 66, 51) | (np.abs(np.hypot(c.x - 46, c.y - 14) - 17) <= 3)
            & (c.y < 14))
    arch &= ~c.rect(55, 0, 60, 6)                                      # broken top
    c.put(outline_mask(arch) & ~arch, K['ink'])
    c.put(arch, KW['stone1'])
    c.put(arch & (c.x < 29), KW['stone2'])
    ivy = arch & ((np.floor(c.x * 0.9 + np.sin(c.y * 0.5) * 2 + c.y * 0.3) % 7) == 0) & (np.sin(c.y * 0.4 + c.x) > 0)
    c.put(ivy, KP3['leaf1'])
    c.put(ivy & ((np.floor(c.y) % 3) == 0), KP3['leaf2'])
    for x, h in ((6, 9), (14, 6), (76, 8), (84, 10)):
        for k in range(5):
            th = math.pi * (0.15 + k * 0.175)
            fr = c.line(x, 52, x + math.cos(th) * h, 52 - math.sin(th) * h)
            c.put(fr, KP3['leaf1'] if k % 2 else KP3['leaf2'])
    c.put(c.rect(0, 52, AW - 1, AH - 1), KP2['grass0'])
    c.put(c.rect(0, 52, AW - 1, 52), KP2['grass2'])


def vine(c, g, cx, cy, grow, t, seed, bloom):
    """Two vines spiralling round (cx, cy) up to `grow` 0..1, with leaves and, when bloom > 0, small pink flowers."""
    m = np.zeros((AH, AW), bool)
    leaves = []
    for side in (-1, 1):
        n = 26
        for i in range(n):
            u = i / (n - 1)
            if u > grow:
                break
            th = side * (u * 2.6 * math.pi) + (0 if side > 0 else math.pi)
            r = 12 - u * 2
            px = cx + math.cos(th) * r
            py = cy + 12 - u * 22 + math.sin(th) * 2.5
            m |= c.pixel(px, py)
            if i % 4 == 2:
                leaves.append((px, py, th))
    c.put(m, KP3['leaf1'])
    for i, (px, py, th) in enumerate(leaves):
        lf = c.disc(px + math.cos(th) * 1.5, py - 1, 1.3)
        c.put(lf, KP3['leaf2'] if i % 2 else KP3['leaf3'])
        g.add(lf, LP['heal'], 0.12)
        if bloom > 0 and i % 2 == 0:
            fl = c.pixel(px + math.cos(th) * 1.5, py - 2)
            c.put(fl, KP3['bloom'], bloom)
            g.add(fl, np.array([1.0, 0.6, 0.8, 1]), 0.6 * bloom)
    return m


def regeneracion_frame(f):
    t = f / FRAMES
    c, g = Canvas(AW, AH), Canvas(AW, AH)
    overgrown_ruins(c, g, t)
    x, y = RG_HEART
    power_ring(c, g, x, 51.5, 16, 2.6, t, 0.5, col=KP['heal2'], light=LP['heal'])
    gx, gy = RG_GLASS
    if f < 3:
        hourglass(c, g, gx, gy, 0.0, 0.04, stream=False)
    elif f < 8:
        hourglass(c, g, gx, gy, math.pi * ease_in_out((f - 2) / 5.0), 0.04, stream=False)
    else:
        hourglass(c, g, gx, gy, 0.0, 0.96 - 0.92 * (f - 8) / 15.0, stream=True)
    if f == 7:
        star4(c, g, gx, gy - 9, 4, core=K['white'], col=KW['gold3'], edge=KW['gold2'], light=LW['gold'])
    heal = 9
    grow = 0.55 + 0.45 * ease_out(min(1.0, max(0.0, (f - 7) / 4.0))) if f < 20 else 0.55 + 0.45 * (1 - (f - 19) / 4.0)
    bloom = {9: 0.5, 10: 1.0, 11: 1.0, 12: 1.0, 13: 0.9, 14: 0.7, 15: 0.5, 16: 0.3}.get(f, 0.0)
    hy = y + math.sin(t * 2 * math.pi * 2) * 0.6
    fill = 0.5 if f < heal else min(0.8, 0.5 + 0.3 * ease_out((f - heal + 1) / 3.0))
    if f >= 20:
        fill = 0.8 - 0.3 * (f - 19) / 4.0
    green = {9: 1.0, 10: 0.8, 11: 0.6, 12: 0.4, 13: 0.2}.get(f, 0.0)
    life_heart(c, g, x, hy, 1.15, fill=fill, green=green * 0.7, beat=1.0 if f in (10, 14) else 0.0)
    vine(c, g, x, hy, grow, t, 1313, bloom)
    if f == heal:
        heal_star(c, g, x, hy, 7)
    heal_crosses(c, g, f, heal, x, hy - 4, count=4, life=8, spread=8, seed=1315)
    motes(c, g, t, 1317, 8, (24, 8, 68, 50), KP['heal3'], KP3['bloom'], LP['heal'], gain=0.35)
    vignette(c, 0.72)
    return c, g


# ---------------------------------------------------------------------------------------- Represalia
RE_HEART = (16.0, 16.0)
RE_PIVOT = (20.0, 50.0)
RE_SIGIL = (70.0, 26.0)
RE_IDLE = math.radians(-50)


def ruined_court(c, g, t):
    """A ruined courtyard at night: broken colonnade, torn crimson banners, rubble."""
    gradient_bg(c, K['night0'], K['night2'], K['night1'], 0.6)
    twinkling_stars(c, g, t, 1321, 14, (2, 2, 86, 24), avoid=(16, 16, 10))
    for i, (x, h) in enumerate(((30, 30), (42, 22), (54, 34), (82, 26))):
        col = c.rect(x - 2, 52 - h, x + 2, 52)
        c.put(col, KW['stone1'])
        c.put(c.rect(x - 2, 52 - h, x - 2, 52), KW['stone2'])
        c.put(c.rect(x - 3, 52 - h, x + 3, 53 - h), KW['stone2'])
    for x in (36, 76):
        bn = c.rect(x, 18, x + 4, 30) & ~((c.y > 26) & ((np.floor(c.x) % 2) == 0))
        c.put(bn, KW['crim1'])
        c.put(bn & (c.x < x + 1.5), KW['crim2'])
    floor_slabs(c, 52)
    for x, w in ((12, 3), (62, 4)):
        c.put(c.rect(x, 50, x + w, 52), KW['stone2'])


def represalia_frame(f):
    t = f / FRAMES
    c, g = Canvas(AW, AH), Canvas(AW, AH)
    ruined_court(c, g, t)
    shot, struck, hit = 3, 5, 14
    sx, sy = RE_SIGIL
    foe_sigil(c, g, sx, sy, t, hit=(f - hit) if f >= hit else -99, r=7.0)
    hx, hy = RE_HEART
    hy += math.sin(t * 2 * math.pi * 2) * 0.6
    fill = 0.8 if f < struck else 0.55
    if f >= 20:
        fill = 0.55 + 0.25 * (f - 19) / 4.0
    life_heart(c, g, hx, hy, 1.0, fill=fill, crack=1.0 if struck <= f < 20 else 0.0,
               flash={5: 0.6, 6: 0.2}.get(f, 0.0))
    # the enemy's bolt hits her heart
    a = f - shot
    if 0 <= a <= 1:
        k = (a + 1) / 2.0
        bx, by = sx + (hx - sx) * k, sy + (hy - sy) * k
        comet(c, g, bx, by, sx, sy, (KW['crim1'], KW['crim3'], KW['crim5']), LW['crimson'], r=1.8)
    if f == struck:
        fury_star(c, g, hx, hy, 7)
    # her blood falls from the heart onto the blade, and doubles there
    px, py = RE_PIVOT
    def ang_at(f):
        if f <= 10:
            return RE_IDLE
        if f <= 12:
            return RE_IDLE + math.radians(-60) * ease_in_out((f - 10) / 2.0)
        if f == 13:
            return math.radians(-60)
        if f <= 16:
            return math.radians(20)
        return math.radians(20) + (RE_IDLE - math.radians(20)) * ease_in_out((f - 16) / 7.0)
    ang = ang_at(f)
    gx, gy = px + math.cos(ang) * 5, py + math.sin(ang) * 5
    for j, born in enumerate((5, 6)):
        aa = f - born
        if 0 <= aa <= 3:
            k = aa / 3.0
            yy = hy + 6 + (gy - 22 - hy - 6) * k * k
            blood_drop(c, g, hx + 1 + j * 2 + k * 6, yy, 1.0, stretch=1 + k * 0.6)
    fury = 1.0 if 9 <= f <= 17 else 0.0
    if f == 13:
        crescent(c, g, px, py, 39.5, -ang, math.radians(80), -1, 10.0,
                 (KW['crim1'], KW['crim3'], KW['crim5'], K['white']), LW['crimson'], gain=1.5)
    elif 14 <= f <= 17:
        q = (f - 14) / 4.0
        crescent(c, g, px, py, 39.5, math.radians(-20), math.radians(110) * (1 - 0.6 * q), -1, 10 * (1 - 0.7 * q),
                 (KW['crim1'], KW['crim3'], KW['crim5'], K['white']), LW['crimson'], a=0.9 - 0.6 * q, dash=f >= 16,
                 gain=1.4 * (1 - q))
    lab = sword(c, g, gx, gy, ang, t * 2 + (1.5 if 9 <= f <= 13 else 0), rune_gain=1.0 + 1.3 * fury, fury=fury,
                glint=(-4 + (f - 19) * 9.0 if 19 <= f <= 23 else None))
    if 8 <= f <= 12:                                                    # two drops on the blade = twice the blood
        for j in range(2):
            u = 14 + j * 10
            bx_, by_ = gx + math.cos(ang) * u, gy + math.sin(ang) * u - 3
            blood_drop(c, g, bx_, by_, 1.3 if f >= 9 else 1.0, gain=1.5)
        if f == 9:
            fury_star(c, g, gx + math.cos(ang) * 19, gy + math.sin(ang) * 19 - 4, 5)
        blood_aura(c, g, lab > 0, {8: 0.4, 9: 0.8, 10: 1.0, 11: 1.0, 12: 0.8}[f], t, width=1)
    impact(c, g, sx, sy, f - hit, r=8, kind='crim')
    for j in range(2):
        plus_marks(c, g, sx - 6 + j * 8, sy - 6, f - hit - 1 - j, count=2)
    sparks(c, g, f, hit, sx, sy, 1323, 12, (1.0, 2.6), 7, CRIM_SPARKS, LW['crimson'], gravity=0.15)
    motes(c, g, t, 1325, 6, (4, 6, 84, 46), KW['crim4'], K['violet3'], LW['crimson'], gain=0.4)
    vignette(c, 0.72)
    if f == hit:
        shake(c, g, 1, 0)
    elif f == struck:
        shake(c, g, -1, 0)
    return c, g


# ---------------------------------------------------------------------------------------- Sacrificio
SC_HEART = (44.0, 15.0)
SC_SWORD = (44.0, 40.0)         # guard of the sword lying point-right
SC_SIGIL = (72.0, 40.0)


def red_chapel(c, g, t):
    """A chapel with a tall red stained-glass window, candles on an altar step, dark pews."""
    gradient_bg(c, K['vio0'], K['vio1'], K['night0'], 0.6)
    back = c.rect(0, 0, AW - 1, 51)
    c.put(back, KP['temple0'])
    win = c.rect(36, 4, 52, 34) | (c.disc(44, 5, 8) & (c.y < 5))
    c.put(dilate(win), K['ink'])
    c.put(win, KW['crim1'])
    lead = win & ((np.abs(c.x - 44) < 0.6) | (np.abs(c.y - 14) < 0.6) | (np.abs(c.y - 24) < 0.6) |
                  (np.abs((c.x - 44) + (c.y - 19)) < 0.6))
    c.put(win & (c.x < 41), KW['crim2'])
    c.put(win & (c.y < 9) & (c.x < 44), KW['crim3'])
    c.put(lead, K['ink'])
    g.add(win & ~lead, LW['crimson'], 0.05)
    beam = (np.abs((c.x - 44) - (c.y - 34) * 0.1) < 7 + (c.y - 34) * 0.3) & (c.y > 34) & (c.y < 52)
    c.put(beam, KW['crim1'], 0.2)
    for x in (8, 18, 70, 80):
        pew = c.rect(x - 4, 44, x + 4, 51)
        c.put(pew, KP['oak0'])
        c.put(c.rect(x - 4, 44, x + 4, 44), KP['oak2'])
    floor_slabs(c, 52, tone=(KP['temple0'], KP['temple1'], KP['temple2'], KP['temple3']))


def sacrificio_frame(f):
    t = f / FRAMES
    c, g = Canvas(AW, AH), Canvas(AW, AH)
    red_chapel(c, g, t)
    hit = 12
    sx, sy = SC_SIGIL
    foe_sigil(c, g, sx, sy, t, hit=(f - hit) if f >= hit else -99, r=6.5)
    hx, hy = SC_HEART
    hy += math.sin(t * 2 * math.pi * 2) * 0.6
    fill = 0.85 if f < 4 else max(0.4, 0.85 - 0.45 * (f - 3) / 5.0)
    if f >= 20:
        fill = 0.4 + 0.45 * (f - 19) / 4.0
    life_heart(c, g, hx, hy, 1.1, fill=fill, crack=1.0 if 4 <= f < 20 else 0.0, flash={4: 0.4}.get(f, 0.0))
    # its blood pours down onto the sword (lots of it: 6 de vida)
    if 4 <= f <= 9:
        stream = c.rect(int(hx), int(hy) + 6, int(hx), int(SC_SWORD[1]) - 3)
        c.put(stream, KW['crim3'])
        c.put(stream & ((np.floor(c.y - f * 3) % 4) == 0), KW['crim5'])
        g.add(stream, LW['crimson'], 0.6)
    gx, gy = SC_SWORD
    gx0 = gx - 17
    fly = {10: 6, 11: 16, 12: 22, 13: 21, 14: 19, 15: 15, 16: 10, 17: 6, 18: 3, 19: 1}.get(f, 0)
    fury = 1.0 if 6 <= f <= 17 else 0.0
    if f in (10, 11):
        speed_lines(c, g, gx0 + fly + 4, gy, 1, 0, 1.0, 1331, count=6, spread=4, length=(6, 12), back=4)
        comet(c, g, gx0 + fly + 34, gy, gx0 + fly + 10, gy, (KW['crim1'], KW['crim3'], KW['crim5']),
              LW['crimson'], r=2.0, gain=0.8)
    lab = sword(c, g, gx0 + fly, gy, 0.0, t * 2, rune_gain=1.0 + 1.4 * fury, fury=fury)
    blood_aura(c, g, lab > 0, {7: 0.5, 8: 0.9, 9: 1.0, 10: 1.0, 11: 1.0, 12: 0.7, 13: 0.4}.get(f, 0.0), t, width=2)
    splash(c, g, f, 5, hx, gy - 3, seed=1333, count=8)
    impact(c, g, sx - 3, sy, f - hit, r=8, kind='crim')
    sparks(c, g, f, hit, sx - 3, sy, 1335, 12, (1.0, 2.6), 7, CRIM_SPARKS, LW['crimson'], gravity=0.15)
    # Furia: a card rises out of the spilled blood
    rising_card(c, g, f, 14, 30, 50, 22, life=8, seed=3)
    motes(c, g, t, 1337, 6, (4, 6, 84, 46), KW['crim4'], K['violet3'], LW['crimson'], gain=0.4)
    vignette(c, 0.72)
    if f == hit:
        shake(c, g, 1, 0)
    return c, g


# ---------------------------------------------------------------------------------------- Segunda Piel
SP_HEART = (44.0, 26.0)


def blue_sanctum(c, g, t):
    """A cold vault: arches of dark stone, two braziers of blue fire, a mosaic floor."""
    gradient_bg(c, K['teal0'], K['teal1'], K['night0'], 0.6)
    back = c.rect(0, 0, AW - 1, 51)
    c.put(back, KP['temple0'])
    for x0 in (2, 30, 58):
        arch = (c.rect(x0, 10, x0 + 27, 51) | (c.disc(x0 + 13.5, 10, 13.5) & (c.y < 10)))
        inner = (c.rect(x0 + 3, 12, x0 + 24, 51) | (c.disc(x0 + 13.5, 12, 10.5) & (c.y < 12)))
        c.put(arch & ~inner, KP['temple1'])
        c.put(inner, K['teal0'])
    for i, x in enumerate((14.0, 74.0)):
        bowl = c.rect(int(x) - 3, 41, int(x) + 3, 43) | c.rect(int(x) - 1, 43, int(x) + 1, 51)
        c.put(outline_mask(bowl), K['ink'])
        c.put(bowl, KW['iron2'])
        fl = np.zeros((AH, AW), bool)
        for k in range(3):
            h = 4 + 1.5 * math.sin(t * 4 * math.pi + i * 2 + k)
            fl |= c.poly([(x - 2.5 + k * 1.5, 41), (x - 1.5 + k * 1.5, 41 - h), (x - 0.5 + k * 1.5, 41)])
        c.put(fl, KB['b1'])
        c.put(fl & (c.y > 38.5), KB['b2'])
        g.add(fl, LB, 0.7)
    floor = c.rect(0, 52, AW - 1, AH - 1)
    c.put(floor, KP['temple1'])
    c.put(floor & (((np.floor(c.x / 4) + np.floor(c.y / 2)) % 2) == 0), KP['temple2'])
    c.put(c.rect(0, 52, AW - 1, 52), KP['temple3'])


def mail_mesh(c, g, x, y, s, glow):
    """A mesh of small steel rings over the heart (its second skin); glow 0..1 lights it blue."""
    hm = dilate(heart_mask(c, x, y, s))
    ix, iy = np.floor(c.x), np.floor(c.y)
    rings = hm & ((((ix + iy) % 4) == 0) | (((ix - iy) % 4) == 0))
    edge = hm & ~erode(hm)
    c.put(rings, K['steel2'])
    c.put(edge, K['steel1'])
    if glow > 0:
        c.put(rings | edge, KB['b3'], glow)
        g.add(rings | edge, LB, 0.8 * glow)


def segunda_piel_frame(f):
    t = f / FRAMES
    c, g = Canvas(AW, AH), Canvas(AW, AH)
    blue_sanctum(c, g, t)
    x, y = SP_HEART
    power_ring(c, g, x, 51.5, 17, 2.6, t, 0.5, col=KB['b2'], light=LB)
    hy = y + math.sin(t * 2 * math.pi * 2) * 0.6
    loss = 6
    fill = 0.8 if f < loss else 0.65
    if f >= 20:
        fill = 0.65 + 0.15 * (f - 19) / 4.0
    life_heart(c, g, x, hy, 1.5, fill=fill, flash={6: 0.3}.get(f, 0.0))
    glow = {7: 0.5, 8: 1.0, 9: 1.0, 10: 0.8, 11: 0.6, 12: 0.4, 13: 0.2}.get(f, 0.0)
    mail_mesh(c, g, x, hy, 1.5, glow)
    # a drop of her blood leaves the heart (she loses HP) ...
    a = f - loss
    if 0 <= a <= 5:
        yy = hy + 9 + a * a * 0.9 + a
        blood_drop(c, g, x + 0.5, yy, 1.1, stretch=1 + a * 0.15)
    splash(c, g, f, loss + 6, x, 51, seed=1341, count=5)
    if f == loss:
        fury_star(c, g, x, hy + 7, 4)
    # ... and the mesh answers with a ward: +3 de escudo
    if f == 8:
        blue_star(c, g, x, hy - 9, 6)
    if 0 <= f - 8 <= 8:
        ward_rings(c, g, x, hy + 1, f - 8, size=18, squash=1.0, life=9)
    sparks(c, g, f, 8, x, hy, 1343, 10, (0.6, 1.6), 6, BLUE_SPARKS, LB, gravity=-0.03, gain=0.8)
    motes(c, g, t, 1345, 7, (20, 6, 68, 48), KB['b2'], KW['crim4'], LB, gain=0.35)
    vignette(c, 0.72)
    return c, g


# ---------------------------------------------------------------------------------------- Torbellino de Acero
TA_SIGILS = ((30.0, 40.0), (50.0, 30.0), (70.0, 40.0))
TA_HOME = (16.0, 34.0)


def night_dunes(c, g, t):
    """Desert dunes at night: cold violet sand, a big moon low on the horizon, sand blowing off the crests."""
    gradient_bg(c, K['night0'], K['vio2'], K['vio1'], 0.6)
    c.put(c.disc(62, 11, 10), K['vio3'], 0.35)
    moon = c.disc(62, 11, 6)
    c.put(moon, K['moon1'])
    c.put(moon & (c.x + c.y > 75), K['moon0'])
    g.add(moon, L['violet'], 0.2)
    twinkling_stars(c, g, t, 1351, 16, (2, 2, 86, 24), avoid=(62, 11, 10), light=L['violet'])
    for i, (y0, amp, ph) in enumerate(((40, 3, 0.0), (46, 4, 1.4), (53, 3, 2.7))):
        dune = c.y >= y0 + np.sin(c.x / 13.0 + ph) * amp
        c.put(dune, KP3['sand1'] if i % 2 else KP3['sand2'])
        crest = dune & ~shift(dune, 0, 1)
        c.put(crest, KP3['sand3'])
        off = (t * AW * (1 + i)) % AW
        blow = crest & (((c.x + off) % 23) < 3)
        c.put(shift(blow, -1, -1), KP3['sand3'], 0.6)


def torbellino_acero_frame(f):
    t = f / FRAMES
    c, g = Canvas(AW, AH), Canvas(AW, AH)
    night_dunes(c, g, t)
    hits = (9, 11, 13)
    for i, (sx, sy) in enumerate(TA_SIGILS):
        foe_sigil(c, g, sx, sy - 6, t + i * 0.2, hit=(f - hits[i]) if f >= hits[i] else -99, r=5.5)
    hx, hy = TA_HOME
    # path: from home, spinning, across the three sigils and back
    if f <= 6:
        px, py, spin = hx, hy + math.sin(t * 2 * math.pi * 2) * 0.6, 0.0
    elif f <= 15:
        k = (f - 7) / 8.0
        px = hx + (78 - hx) * k
        py = hy + math.sin(k * math.pi * 3) * 4 - 2
        spin = 1.0
    else:
        k = ease_in_out((f - 15) / 8.0)
        px = 78 + (hx - 78) * k
        py = hy - 6 * math.sin(k * math.pi)
        spin = max(0.0, 1 - (f - 15) / 4.0)
    fury = 1.0 if 4 <= f <= 15 else 0.0
    if spin > 0.2:                                                      # a flat disc of steel spinning
        R = 13
        for j, ph in enumerate((0.0, 0.33, 0.66)):
            head = (f * 2.2 + ph * 2 * math.pi) % (2 * math.pi)
            crescent(c, g, px, py, R, head, 1.6, 1, 3.5, (K['cyan1'], K['cyan2'], K['steel3'], K['white']),
                     L['cyan'], a=spin, gain=1.0 * spin)
        disc = ellipse_ring(c, px, py, R, R * 0.35, 0.8)
        c.put(disc, K['steel3'], 0.6 * spin)
        g.add(disc, L['cyan'], 0.4 * spin)
        ang = (f * 2.2) % (2 * math.pi)
        gx, gy = px - math.cos(ang) * 17, py - math.sin(ang) * 17 * 0.35
        lab = sword(c, g, gx, gy, math.atan2(math.sin(ang) * 0.35, math.cos(ang)), t * 2, fury=fury,
                    rune_gain=1.0 + fury, blade=28)
        if f in (8, 12):
            speed_lines(c, g, px - 10, py, 1, 0, 1.0, 1353 + f, count=6, spread=6, length=(6, 12), back=4)
    else:
        lab = sword(c, g, px - 17 + 17, py, math.radians(-90), t * 2, fury=fury, rune_gain=1.0 + fury,
                    glint=(-4 + (f - 19) * 9.0 if 19 <= f <= 23 else None), blade=28)
    if 0 <= f <= 6:
        land = drip(c, g, f, 0, hx + 0.5, -3, hy + 6, fall=3, s=1.3, swell=1, seed=1355)
        if land == 0:
            fury_star(c, g, hx, hy + 6, 5)
        blood_aura(c, g, lab > 0, {4: 0.5, 5: 1.0, 6: 1.0}.get(f, 0.0), t, width=1)
    for i, (sx, sy) in enumerate(TA_SIGILS):
        a = f - hits[i]
        impact(c, g, sx, sy - 6, a, r=5)
        sparks(c, g, f, hits[i], sx, sy - 6, 1357 + i, 7, (0.8, 2.0), 6, CYAN_SPARKS, L['cyan'], gravity=0.12)
        heal_crosses(c, g, f, hits[i] + 1, sx, sy - 12, count=2, life=7, spread=2, seed=1361 + i)   # Furia: cura 3
    motes(c, g, t, 1365, 6, (4, 6, 84, 46), K['cyan3'], KP['heal3'], L['cyan'], gain=0.4)
    vignette(c, 0.72)
    return c, g


# ---------------------------------------------------------------------------------------- Torre
TO_SHIELD = (44.0, 38.0)


def rocky_plain(c, g, t):
    """A rocky plain before dawn: a band of pale light at the horizon, far crags, a cold wind in the grass."""
    gradient_bg(c, K['night1'], K['teal2'], K['teal3'], 0.62)
    c.put(np.abs(c.y - 40) < 2, K['moon0'], 0.22)
    twinkling_stars(c, g, t, 1371, 12, (2, 2, 86, 20))
    crags = c.poly([(0, 42), (8, 34), (14, 38), (22, 31), (30, 40), (60, 40), (66, 33), (74, 37), (80, 30),
                    (88, 36), (88, 64), (0, 64)])
    c.put(crags, KP3['rock1'])
    c.put(crags & ~shift(crags, 0, 1), KP3['rock3'])
    c.put(c.rect(0, 52, AW - 1, AH - 1), KP3['rock0'])
    c.put(c.rect(0, 52, AW - 1, 52), KP3['rock2'])
    for i in range(14):
        x = (i * 31 + int(t * 24)) % AW
        c.put(c.line(x, 55 + (i * 5) % 8, x + 1, 53 + (i * 5) % 8), KP2['grass2'])


def tower_block(c, x, y, w, h, alpha=1.0):
    blk = c.rect(int(x - w / 2), int(y - h), int(x + w / 2) - 1, int(y) - 1)
    c.put(outline_mask(blk), K['ink'], alpha)
    c.put(blk, KW['stone2'], alpha)
    c.put(blk & (c.x < x - w / 2 + 2), KW['stone3'], alpha)
    c.put(blk & (c.y < y - h + 1), KW['stone3'], alpha)
    c.put(blk & (c.x > x + w / 2 - 2), KW['stone1'], alpha)
    return blk


def torre_frame(f):
    t = f / FRAMES
    c, g = Canvas(AW, AH), Canvas(AW, AH)
    rocky_plain(c, g, t)
    x, y = TO_SHIELD
    # stone courses fly in and stack into a tower behind the shield (2-10), the crown last
    courses = [(52, 26, 6), (46, 24, 6), (40, 24, 6), (34, 22, 6), (28, 22, 6), (22, 22, 6), (16, 24, 4)]
    land_at = [2, 3, 4, 5, 6, 7, 9]
    fade = 1.0 if f < 19 else max(0.0, 1 - (f - 18) / 4.0)
    for i, ((by, w, h), la) in enumerate(zip(courses, land_at)):
        a = f - la
        if a < -2:
            continue
        drop = 0 if a >= 0 else (-a) * 7
        if fade <= 0:
            continue
        blk = tower_block(c, x, by - drop, w, h, alpha=fade)
        for j in range(int(w // 6)):
            c.put(c.rect(int(x - w / 2) + 3 + j * 6, int(by - drop - h), int(x - w / 2) + 3 + j * 6, int(by - drop) - 1)
                  & blk, KW['stone1'], fade)
        if i == len(courses) - 1:
            for mx in range(int(x - w / 2), int(x + w / 2), 6):
                mer = c.rect(mx, int(by - drop - h - 3), mx + 3, int(by - drop - h) - 1)
                c.put(outline_mask(mer), K['ink'], fade)
                c.put(mer, KW['stone2'], fade)
        if a == 0:
            dust_puffs(c, x, by, f, la, 2, 2.0, rise=0.3, life=4, seed=1371 + i, big=0.7)
    if 9 <= f <= 12:                                                    # the window in it lights
        win = c.rect(int(x) - 1, 26, int(x) + 1, 30)
        c.put(win, K['window'])
        g.add(win, L['warm'], 0.5)
    elif 13 <= f <= 18:
        win = c.rect(int(x) - 1, 26, int(x) + 1, 30)
        c.put(win, K['window2'])
        g.add(win, L['warm'], 0.3)
    contact_shadow(c, x, 53, 10)
    slab = shield(c, g, x, y, W=10.0, H=26.0, gem_gain=1.0 + (1.5 if 10 <= f <= 12 else 0),
                  flash={10: 0.4}.get(f, 0.0))
    if 0 <= f - 10 <= 9:
        ward_rings(c, g, x, 32, f - 10, size=22, squash=1.45, life=10)
    if f == 10:
        blue_star(c, g, x, 8, 7)
    sparks(c, g, f, 10, x, 12, 1379, 10, (0.6, 1.6), 6, BLUE_SPARKS, LB, gravity=0.05, gain=0.8)
    motes(c, g, t, 1381, 6, (16, 6, 72, 48), KB['b2'], K['steel3'], LB, gain=0.35)
    vignette(c, 0.72)
    if f in (2, 9):
        shake(c, g, 0, 1)
    return c, g


# ---------------------------------------------------------------------------------------- Transfusión
TF_SHIELD = (20.0, 38.0)
TF_HEART = (66.0, 22.0)


def alchemy_room(c, g, t):
    """An alchemist's room: shelves of bottles, a brass coil, a window with moonlight, a workbench."""
    gradient_bg(c, K['night0'], K['teal1'], K['night0'], 0.6)
    back = c.rect(0, 0, AW - 1, 51)
    c.put(back, KP['oak0'])
    for y in (12, 24):
        c.put(c.rect(0, y, AW - 1, y), KP['oak2'])
        c.put(c.rect(0, y + 1, AW - 1, y + 1), K['ink'])
        for i, x in enumerate(range(3, AW, 7)):
            h = 4 + (i * 3) % 4
            b = c.rect(x, y - h, x + 2, y - 1)
            col = (KP3['leaf1'], KW['crim1'], K['violet1'], KB['b0'])[i % 4]
            c.put(b, col)
            c.put(c.rect(x + 1, y - h - 1, x + 1, y - h - 1), KP['oak3'])
            if i % 3 == 0:
                g.add(b, L['violet'], 0.08)
    bench = c.rect(0, 50, AW - 1, 52)
    c.put(bench, KP['oak2'])
    c.put(c.rect(0, 50, AW - 1, 50), KP['oak3'])
    c.put(c.rect(0, 53, AW - 1, AH - 1), KP['oak0'])


def coil_path(u):
    """A glass coil from the shield to the heart: (x, y) at u 0..1."""
    x = TF_SHIELD[0] + 10 + (TF_HEART[0] - 9 - TF_SHIELD[0] - 10) * u
    y = 40 - u * 14 + math.sin(u * math.pi * 3) * 4
    return x, y


def transfusion_frame(f):
    t = f / FRAMES
    c, g = Canvas(AW, AH), Canvas(AW, AH)
    alchemy_room(c, g, t)
    # the glass coil
    for i in range(80):
        u = i / 79
        px, py = coil_path(u)
        m = c.pixel(px, py) | c.pixel(px, py - 1) | c.pixel(px, py + 1)
        c.put(m & ~c.pixel(px, py), KP3['glass2'] if i % 2 else KP3['glass1'])
        c.put(c.pixel(px, py), KP3['glass0'])
    sx, sy = TF_SHIELD
    slab = shield(c, g, sx, sy, W=9.0, H=24.0, gem_gain=1.0 + (1.2 if 4 <= f <= 10 else 0))
    # the ward it holds drains (4-10) along the coil (5-14)
    size = 14.0 if f < 4 else max(0.0, 14.0 * (1 - (f - 3) / 7.0))
    if f >= 19:
        size = 14.0 * (f - 18) / 5.0
    if size > 0.5:
        for i, (r, w) in enumerate(((5 + size, 0.6), (3 + size * 0.65, 0.5))):
            ring = diamond_ring(c, sx, sy + 1, r, w, squash=1.15)
            c.put(ring, KB['b2'] if i == 0 else KB['b1'])
            g.add(ring, LB, 0.6 if i == 0 else 0.3)
    if 5 <= f <= 15:
        for j in range(8):
            u = ((f - 5) / 9.0) - j * 0.07
            if not 0 <= u <= 1:
                continue
            px, py = coil_path(u)
            col = KB['b3'] if u < 0.7 else KP['heal3']
            light = LB if u < 0.7 else LP['heal']
            m = c.pixel(px, py)
            c.put(m, col)
            g.add(dilate(m), light, 0.7)
    hx, hy = TF_HEART
    hy += math.sin(t * 2 * math.pi * 2) * 0.6
    heal = 12
    fill = 0.3 if f < heal else min(0.85, 0.3 + 0.55 * ease_out((f - heal + 1) / 4.0))
    if f >= 20:
        fill = 0.85 - 0.55 * (f - 19) / 4.0
    green = {12: 1.0, 13: 0.9, 14: 0.7, 15: 0.5, 16: 0.3, 17: 0.15}.get(f, 0.0)
    life_heart(c, g, hx, hy, 1.3, fill=fill, green=green * 0.7, beat=1.0 if f in (13, 16) else 0.0)
    if f == heal:
        heal_star(c, g, hx, hy, 7)
    heal_crosses(c, g, f, heal, hx, hy - 4, count=4, life=8, spread=8, seed=1391)
    motes(c, g, t, 1393, 6, (6, 6, 82, 48), KB['b2'], KP['heal3'], LB, gain=0.35)
    vignette(c, 0.72)
    return c, g


ILLUSTRATIONS = {
    'conjuro_combate': conjuro_combate_frame,
    'coraza_viva': coraza_viva_frame,
    'golpe_pesado': golpe_pesado_frame,
    'golpe_total': golpe_total_frame,
    'golpe_escudo': golpe_escudo_frame,
    'martillo_sismico': martillo_sismico_frame,
    'pacto_hierro': pacto_hierro_frame,
    'regeneracion': regeneracion_frame,
    'represalia': represalia_frame,
    'sacrificio': sacrificio_frame,
    'segunda_piel': segunda_piel_frame,
    'torbellino_acero': torbellino_acero_frame,
    'torre': torre_frame,
    'transfusion': transfusion_frame,
}


def render(card_id):
    fn = ILLUSTRATIONS[card_id]
    base, glow = [], []
    for f in range(FRAMES):
        b, gl = fn(f)
        base.append(b)
        glow.append(gl)
    return base, glow
