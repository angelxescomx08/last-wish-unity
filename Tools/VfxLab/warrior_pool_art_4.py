"""Animated pixel-art illustrations for the warrior's epic and legendary loot cards (88x64, 24 frames at 12 fps).

Same method as warrior_pool_art.py .. _3.py (shared vocabulary: her blood, Furia, Aguante, Fuerza, healing, block) and
docs/art-style.md. No figure: her gear on its own. The rarest cards get the biggest pictures: a colossal blade of
light, a whole castle, a great wall, an eclipse.

  baluarte               Baluarte (22 de escudo; Aguante: +1 Fuerza): in a snowy pass six shields of light lock round
                         hers into a wall; the gold pulse turns the planted sword's runes crimson.
  furia_desatada         Furia Desatada (4 golpes de 4; Furia: +2): among burning ruins her blood on the sword, which
                         cuts an enemy sigil four times and then twice more in crimson.
  gran_golpe             Gran Golpe (26): on a mountain the raised sword calls a colossal blade of light that falls on
                         an enemy sigil; shock ring, cracks, dust.
  gran_muralla           Gran Muralla (28 de escudo): a great stone wall with towers rises in a wave across the plain
                         behind her shield; a ward as wide as the picture.
  ira_carmesi            Ira Carmesí (40; pierdes 8 de vida; cura la mitad): under a black sun her heart pours its blood
                         on the sword, which burns crimson and cleaves a sigil; half the blood comes back green.
  piel_acero             Piel de Acero (poder: al final de tu turno, 6 de escudo): her cuirass on a steel rune circle in
                         a smithy; the hourglass runs out and liquid steel coats it, a ward.
  bastion                Bastión (30 de escudo): a whole castle rises behind her shield, flags unfurl, a huge ward.
  berserker              Berserker (poder: cada vez que pierdes vida, +1 Fuerza): her war-painted helm on a crimson rune
                         circle; each drop of her blood makes the visor flare and a chevron rise.
  doblar_escudo          Doblar Escudo (duplica tu escudo): over a frozen lake the shield's reflection rises out of the
                         ice and joins it; the ward doubles.
  ejecucion              Ejecución (30; si lo mata, te curas 10): a downward cut empties an enemy's life bar, the skull
                         lights, the sigil shatters and its life comes back to her heart.
  escudo_impenetrable    Escudo Impenetrable (22 de escudo; Aguante: +2 Fuerza): during a siege an arrow, a fireball and
                         an axe break on the dome round her shield; two crimson chevrons.
  golpe_vampirico        Golpe Vampírico (24; te curas el daño hecho): in a ruined cathedral the cut on a sigil sends a
                         swarm of blood bats back into her heart, which fills.
  muralla_infranqueable  Muralla Infranqueable (30 de escudo; dura un turno más): in a blizzard a huge ward round her
                         shield is sealed in gold; the hourglass turns and the ward stays.
  voluntad_hierro        Voluntad de Hierro (poder: tus efectos no te quitan más de 1 de vida): her heart in an iron cage
                         on a rune circle; big drops of her blood break on the bars, one tiny drop gets through.
"""
import math

import numpy as np

from pixelkit import Canvas, hexc, dilate, erode, shift
from rogue_art import (AW, AH, FRAMES, K, L, ease_out, ease_in_out, frac, gradient_bg, vignette, star4, motes,
                       outline_mask, crescent)
from mage_art import twinkling_stars, sparks, ellipse_ring, flame, hex_cells
from warrior_art import (KW, LW, shake, falling_sparks, dust_puffs, rising_chevrons, sword_labels, paint_sword,
                         heater_labels, blade_point, WARM_SPARKS, CYAN_SPARKS)
from rogue_pool_art import KB, LB, GOLD_SPARKS, mana_drop, speed_lines, converge, diamond_ring
from rogue_pool_art_2 import paint_card, arrow
from rogue_pool_art_3 import plus_marks, rising_card, ward_rings, slash_line
from rogue_pool_art_4 import hourglass, comet
from rogue_pool_art_5 import power_ring
from rogue_pool_art_6 import hp_bar, ground_sigil
from warrior_pool_art import (KP, LP, BLOOD_SPARKS, HEAL_SPARKS, blood_drop, drip, splash, life_heart, heart_mask,
                              heal_crosses, blood_aura, gold_pulse, faint_ward, foe_sigil, sword, shield, shield_gem,
                              contact_shadow, floor_slabs)
from warrior_pool_art_2 import KP2, CRIM_SPARKS, fury_star, gold_star, heal_star, helm, greataxe
from warrior_pool_art_3 import KP3, BLUE_SPARKS, blue_star, impact, cuirass

KP4 = dict(KP3)
KP4.update({
    'snow0': hexc('#2a3450'), 'snow1': hexc('#5a6a8e'), 'snow2': hexc('#a9b8d8'), 'snow3': hexc('#e8eeff'),
    'ice0': hexc('#0c1a2c'), 'ice1': hexc('#16304a'), 'ice2': hexc('#2a5a7c'), 'ice3': hexc('#6aa6c8'),
    'fire0': hexc('#3a0e08'), 'fire1': hexc('#8a2a10'), 'fire2': hexc('#e0661e'), 'fire3': hexc('#ffb04a'),
})
FIRE_SPARKS = (KP4['fire3'], KP4['fire2'], KP4['fire1'])


def ward_n(c, g, x, y, size, a=1.0, squash=1.0, col=None):
    """A held ward of a given size (two diamond rings), drawn at strength a."""
    for i, (r, w) in enumerate(((5 + size, 0.6), (3 + size * 0.65, 0.5))):
        ring = diamond_ring(c, x, y, r, w, squash=squash)
        c.put(ring, (col or KB['b2']) if i == 0 else KB['b1'], a)
        c.put(ring & (((c.x + c.y) % 7) < 2), KB['b3'], a)
        g.add(ring, LB, (0.6 if i == 0 else 0.3) * a)


def snowfall(c, g, t, count=36, wind=0.4, seed=1401, a=0.9):
    rng = np.random.default_rng(seed)
    for i in range(count):
        x0, ph = rng.uniform(0, AW + 20), rng.random()
        sp = 1 if i % 3 else 2
        u = frac(t * sp + ph)
        y = u * (AH + 4) - 2
        x = (x0 - u * AH * wind + math.sin((u * 3 + ph) * 2 * math.pi) * 1.5) % (AW + 4) - 2
        m = c.pixel(x, y)
        c.put(m, KP4['snow3'] if i % 4 == 0 else KP4['snow2'], a)


# ---------------------------------------------------------------------------------------- Baluarte
BA_SHIELD = (44.0, 34.0)
BA_SLOTS = [(150, 24), (180, 25), (210, 23), (30, 24), (0, 25), (-30, 23)]


def snowy_pass(c, g, t):
    """A mountain pass in the snow at night: white peaks, a frozen path, pines bowed with snow."""
    gradient_bg(c, K['night0'], K['teal1'], K['teal2'], 0.6)
    twinkling_stars(c, g, t, 1411, 12, (2, 2, 86, 16))
    peaks = c.poly([(0, 30), (12, 16), (22, 26), (34, 12), (48, 28), (60, 14), (72, 24), (82, 12), (88, 20),
                    (88, 64), (0, 64)])
    c.put(peaks, KP4['snow0'])
    caps = peaks & (c.y < 22)
    c.put(caps, KP4['snow1'])
    c.put(caps & ~shift(caps, 0, 1), KP4['snow2'])
    near = c.poly([(0, 44), (16, 40), (32, 43), (56, 41), (72, 44), (88, 40), (88, 64), (0, 64)])
    c.put(near, KP4['snow1'])
    c.put(near & ~shift(near, 0, 1), KP4['snow3'])
    c.put(near & (c.y > 50), KP4['snow0'], 0.5)
    for x in (6, 80, 86):
        tree = c.poly([(x - 5, 46), (x, 30), (x + 5, 46)])
        c.put(tree, K['roof'])
        c.put(tree & (np.abs(c.y - 36) < 0.6) | tree & (np.abs(c.y - 41) < 0.6), KP4['snow2'])


def spectral_heater(c, g, cx, cy, a=1.0, flash=0.0, s=0.5):
    """One of the shields of light that lock round hers: her heater's shape in steel-blue light."""
    lab = heater_labels(c, cx, cy, W=11.5 * s, H=30.0 * s)
    m = lab > 0
    if not m.any():
        return m
    c.put(outline_mask(m), K['ink'], a)
    c.put(m, KW['plate1'], a)
    c.put(m & (c.x + c.y < cx + cy - 1), KW['plate2'], a)
    c.put(m & ~erode(m), KW['plate3'], a)
    c.put(lab == 4, KW['plate3'], a)
    g.add(m & ~erode(m), LW['steel'], 0.5 * a)
    if flash > 0:
        c.put(m & ~erode(m), K['white'], flash)
        g.add(m, LW['steel'], 0.6 * flash)
    return m


def baluarte_frame(f):
    t = f / FRAMES
    c, g = Canvas(AW, AH), Canvas(AW, AH)
    snowy_pass(c, g, t)
    x, y = BA_SHIELD
    lock = 10
    fury = 1.0 if 14 <= f <= 19 else 0.0
    sword(c, g, x, y - 16, math.pi / 2, t * 2, rune_gain=1.0 + 1.3 * fury, fury=fury, blade=30)
    contact_shadow(c, x, 52, 9)
    slab = shield(c, g, x, y, W=10.0, H=26.0, gem_gain=1.0 + (1.5 if 11 <= f <= 13 else 0),
                  flash={lock: 0.35}.get(f, 0.0))
    faint_ward(c, g, slab > 0, t, a=0.6)
    for i, (deg, r) in enumerate(BA_SLOTS):
        th = math.radians(deg)
        tx, ty = x + math.cos(th) * r, y + 2 - math.sin(th) * r * 0.6
        arrive = 3 + i % 3 + (i // 3) * 0.5
        a = f - arrive
        if f >= 22:
            continue
        if a < 0:
            k = 0.0
            if a < -1:
                continue
        k = ease_out(min(1.0, max(0.0, (a + 1) / 3.0)))
        ox, oy = x + math.cos(th) * 60, y - 30
        px, py = ox + (tx - ox) * k, oy + (ty - oy) * k
        fade = 1.0 if f < 18 else max(0.0, 1 - (f - 17) / 4.0)
        spectral_heater(c, g, px, py, a=fade, flash=0.6 if a == 2 else (0.5 if f == lock else 0.0))
        if a == 2:
            star4(c, g, px, py - 6, 3, core=K['white'], col=KW['plate3'], edge=KW['plate2'], light=LW['steel'])
        if 18 <= f <= 21:
            sparks(c, g, f, 18, px, py, 1413 + i, 4, (0.3, 0.8), 4, (KW['plate3'], KW['plate2'], KW['plate1']),
                   LW['steel'], gravity=-0.05, gain=0.6)
    if 0 <= f - lock <= 9:
        ward_rings(c, g, x, y + 2, f - lock, size=30, squash=0.85, life=10)
    if f == lock:
        blue_star(c, g, x, y - 16, 8)
    gx0, gy0 = shield_gem(x, y, 26.0)
    gold_pulse(c, g, gx0, gy0, x, y - 16, {12: 0.0, 13: 1.0}.get(f, -1))
    if f == 14:
        fury_star(c, g, x, y - 17, 7)
    rising_chevrons(c, g, f, x, y - 22, 14, 6, KW['crim5'], LW['crimson'], count=1, gain=1.0)
    snowfall(c, g, t)
    vignette(c, 0.72)
    if f == lock:
        shake(c, g, 0, 1)
    return c, g


# ---------------------------------------------------------------------------------------- Furia Desatada
FD_HOME = (20.0, 40.0)
FD_SIGIL = (64.0, 30.0)
FD_CUTS = [(-40, 8), (35, 9), (-80, 10), (10, 11), (-55, 13), (60, 14)]     # (blade angle, frame); last two: Furia


def burning_ruins(c, g, t):
    """Ruins of a village burning at night: black walls with lit windows, smoke columns, embers."""
    gradient_bg(c, K['night0'], KP['blood1'], KP4['fire0'], 0.62)
    for i, x in enumerate((12, 46, 78)):
        col = (np.abs(c.x - x - np.sin(c.y / 6.0 + t * 2 * math.pi + i) * 2) < 3 + (40 - c.y) * 0.08) & (c.y < 40)
        c.put(col, K['roof2'], 0.5)
    for x0, w, h in ((0, 16, 16), (20, 12, 12), (52, 14, 18), (72, 16, 13)):
        house = c.rect(x0, 48 - h, x0 + w, 48) | c.poly([(x0 - 1, 48 - h), (x0 + w / 2, 48 - h - 6), (x0 + w + 1, 48 - h)])
        house &= ~c.rect(x0 + w // 3, 48 - h - 6, x0 + w // 3 + 2, 48 - h - 2)        # broken roof
        c.put(house, K['roof'])
        for k in range(2):
            wx = x0 + 3 + k * (w - 6)
            win = c.rect(wx, 48 - h + 4, wx + 1, 48 - h + 6)
            lit = 0.6 + 0.4 * math.sin(t * 6 * math.pi + x0 + k)
            c.put(win, KP4['fire3'] if lit > 0.7 else KP4['fire2'])
            g.add(win, L['warm'], 0.5 * lit)
    for i, x in enumerate((8.0, 60.0, 82.0)):
        flame(c, g, x, 48 - (16 if i == 0 else 13 if i == 2 else 18) + 1, 5, 2.4, t, i * 0.4, gain=0.8, side=False)
    c.put(c.rect(0, 49, AW - 1, AH - 1), KP['blood0'])
    c.put(c.rect(0, 49, AW - 1, 49), KP4['fire1'])
    rng = np.random.default_rng(1421)
    for i in range(12):
        ph = rng.random()
        u = frac(t * (1 + i % 2) + ph)
        ex = rng.uniform(0, AW) + math.sin((u + ph) * 2 * math.pi) * 2
        ey = 50 - u * 48
        a = math.sin(u * math.pi)
        if a > 0.2:
            m = c.pixel(ex, ey)
            c.put(m, KP4['fire3'] if i % 3 else KP4['fire2'], a)
            g.add(m, L['warm'], 0.5 * a)


def furia_desatada_frame(f):
    t = f / FRAMES
    c, g = Canvas(AW, AH), Canvas(AW, AH)
    burning_ruins(c, g, t)
    sx, sy = FD_SIGIL
    last = max((fr for _, fr in FD_CUTS if fr <= f), default=None)
    foe_sigil(c, g, sx, sy, t, hit=(f - last) if last is not None and f - last <= 8 else -99, r=7.5)
    hx, hy = FD_HOME
    hy += math.sin(t * 2 * math.pi * 2) * 0.6 if (f <= 5 or f >= 20) else 0.0
    cut = next(((ang, fr) for ang, fr in FD_CUTS if fr == f), None)
    fury_blade = 1.0 if 3 <= f <= 17 else 0.0
    if 6 <= f <= 15:
        if cut is None:                                                 # between cuts: a ghost of the last pose
            prev = [(a_, fr) for a_, fr in FD_CUTS if fr < f]
            ang = math.radians(prev[-1][0] if prev else -60)
        else:
            ang = math.radians(cut[0])
        ux, uy = math.cos(ang), math.sin(ang)
        gx, gy = sx - ux * 18, sy - uy * 18
        if f in (6, 7):
            k = (f - 5) / 3.0
            gx, gy = hx + (gx - hx) * k, hy + (gy - hy) * k
            ang = math.radians(-60) + (ang - math.radians(-60)) * k
        lab = sword(c, g, gx, gy, ang, t * 2 + 1.5, rune_gain=2.0, fury=fury_blade)
        blood_aura(c, g, lab > 0, 0.8, t, width=1)
    else:
        if f <= 5:
            ang, gx, gy = math.radians(-60), hx, hy
        else:
            k = ease_in_out((f - 15) / 6.0) if f <= 21 else 1.0
            ux, uy = math.cos(math.radians(60)), math.sin(math.radians(60))
            gx, gy = (sx - ux * 18) + (hx - sx + ux * 18) * k, (sy - uy * 18) + (hy - sy + uy * 18) * k
            ang = math.radians(60) + (math.radians(-60) - math.radians(60)) * k
        lab = sword(c, g, gx, gy, ang, t * 2, rune_gain=1.0 + fury_blade, fury=fury_blade,
                    glint=(-4 + (f - 21) * 12.0 if 21 <= f <= 23 else None))
        if 3 <= f <= 5:
            blood_aura(c, g, lab > 0, {3: 0.4, 4: 0.8, 5: 1.0}[f], t, width=1)
    land = drip(c, g, f, 0, hx + 0.5, -3, hy - 1, fall=2, s=1.4, swell=1, seed=1423)
    if land == 0:
        fury_star(c, g, hx, hy - 1, 5)
    for i, (deg, fr) in enumerate(FD_CUTS):
        a = f - fr
        if not 0 <= a <= 3:
            continue
        crim = i >= 4
        pal = (None, KW['crim3'], KW['crim5'], K['white']) if crim else (None, K['cyan2'], K['cyan4'], K['white'])
        sl = slash_line(c, g, sx, sy, math.radians(deg + 90), 13 if crim else 11, 2.0 if crim else 1.5, pal,
                        LW['crimson'] if crim else L['cyan'], a=1 - a / 4)
        if a == 0 and crim:
            impact(c, g, sx, sy, 0, r=6, kind='crim')
        elif a == 0:
            star4(c, g, sx, sy, 5, gain=1.2)
        if crim and a <= 2:
            plus_marks(c, g, sx + (i - 4.5) * 8, sy - 9, a, count=2)
        sparks(c, g, f, fr, sx, sy, 1425 + i, 6, (0.8, 2.0), 5, CRIM_SPARKS if crim else CYAN_SPARKS,
               LW['crimson'] if crim else L['cyan'], gravity=0.15)
    vignette(c, 0.72)
    if f in (13, 14):
        shake(c, g, 1 if f == 13 else -1, 0)
    return c, g


# ---------------------------------------------------------------------------------------- Gran Golpe
GG_REAL = (17.0, 26.0)
GG_TARGET = (60.0, 53.0)


def mountain_top(c, g, t):
    """A mountain top above the clouds at night: a sea of cloud, far peaks, a cold moon."""
    gradient_bg(c, K['night0'], K['night2'], K['teal2'], 0.6)
    c.put(c.disc(34, 11, 5), K['moon1'])
    c.put(c.disc(34, 11, 5) & ~c.disc(32.5, 10, 4.5), K['moon0'])
    g.add(c.disc(34, 11, 5), L['blue'], 0.2)
    twinkling_stars(c, g, t, 1431, 16, (2, 2, 86, 30), avoid=(34, 11, 8))
    for i, (y, sp) in enumerate(((36, 1), (40, 2))):
        off = (t * sp * AW) % AW
        cloud = c.y >= y + np.sin((c.x + off) / 8.0) * 1.5 + np.sin((c.x - off) / 5.0) * 0.8
        c.put(cloud, K['teal2'] if i == 0 else K['teal3'])
        c.put(cloud & ~shift(cloud, 0, 1), K['moon0'], 0.5)
    rock = c.poly([(0, 48), (14, 44), (30, 46), (50, 44), (70, 46), (88, 44), (88, 64), (0, 64)])
    c.put(rock, KP3['rock1'])
    c.put(rock & ~shift(rock, 0, 1), KP3['rock3'])
    c.put(rock & (c.y > 52), KP3['rock0'])


def gran_golpe_frame(f):
    t = f / FRAMES
    c, g = Canvas(AW, AH), Canvas(AW, AH)
    mountain_top(c, g, t)
    hit = 11
    age = f - hit
    tx, ty = GG_TARGET
    ground_sigil(c, g, tx, ty - 1, t, age if age >= 0 else -99)
    # cracks of light from the impact
    if 0 <= age <= 9:
        k = 1 - max(0, age - 4) / 6
        rng = np.random.default_rng(1433)
        for a0 in (-2.9, -2.4, -0.7, -0.2, 0.3, 2.8):
            x0, y0 = tx, ty
            pts = [(x0, y0)]
            ln = (6 + 10 * min(1.0, (age + 1) / 2.0)) * rng.uniform(0.7, 1.2)
            for s_ in range(3):
                x0 += math.cos(a0) * ln / 3 + rng.uniform(-1, 1)
                y0 += abs(math.sin(a0)) * ln / 8 + rng.uniform(-0.4, 0.6)
                pts.append((x0, y0))
            for (a, b), (cc, d) in zip(pts, pts[1:]):
                m = c.line(a, b, cc, d) & (c.y >= 47)
                c.put(m, K['ink'], k)
                c.put(m & ~shift(m, 1, 0), K['cyan2'], k)
                g.add(m, L['cyan'], 0.7 * k)
    # the real sword, raised, calls the colossal blade
    rx, ry = GG_REAL
    ry += math.sin(t * 2 * math.pi * 2) * 0.6
    call = 3 <= f <= 11
    sword(c, g, rx, ry + 14, -math.pi / 2, t * 2 + (1.5 if call else 0), rune_gain=1.0 + (1.5 if call else 0),
          blade=30, glint=(-4 + (f - 16) * 8.0 if 16 <= f <= 20 else None))
    if call:
        star4(c, g, rx, ry - 17, 3 if f % 2 else 4, gain=1.1)
        m = c.line(rx, ry - 17, 46, 2) if 4 <= f <= 9 else np.zeros((AH, AW), bool)
        c.put(m, K['cyan2'], 0.5)
        g.add(m, L['cyan'], 0.4)
    # the colossal blade of light: forms in the sky (4-9), falls (10), strikes (11), fades from the bottom (12-17)
    if 4 <= f <= 17:
        if f <= 9:
            gy_ = -38 + (f - 4) * 1.0
            alpha = (f - 3) / 6.0
        elif f == 10:
            gy_ = -16
            alpha = 1.0
        else:
            gy_ = -4
            alpha = 1.0 if f <= 12 else max(0.0, 1 - (f - 12) / 5.0)
        lab, u, v = sword_labels(c, tx, gy_, math.pi / 2, s=1.6, blade=34.0)
        if f >= 13:
            lab[(c.y > ty - (f - 12) * 10)] = 0
        m = lab > 0
        edge = outline_mask(m)
        solid = 1.0 if alpha > 0.5 else alpha * 2
        c.put(edge, K['cyan3'], solid)
        c.put(m, K['cyan0'], 0.7 * alpha)
        c.put(m & ((lab == 1) | (lab == 5)), K['cyan2'], solid)
        c.put(m & ((lab == 6) | (lab == 11)), K['cyan3'], solid)
        c.put(m & (lab == 10), K['white'], solid)
        g.add(edge, L['cyan'], 0.8 * alpha)
        g.add(m, L['cyan'], 0.25 * alpha)
        if f == 10:
            speed_lines(c, g, tx, 20, 0, 1, 1.0, 1435, count=10, spread=14, length=(8, 16), back=10)
    impact(c, g, tx, ty - 2, age, r=11, gain=1.6)
    if 0 <= age <= 6:
        ring = ellipse_ring(c, tx, ty, 5 + age * 7, 1.5 + age * 1.4, 0.7) & (c.y > 44)
        c.put(ring, K['cyan3'], 1 - age / 7)
        g.add(ring, L['cyan'], 0.9 * (1 - age / 7))
    if 0 <= age <= 9:
        rng = np.random.default_rng(1437)
        for i in range(16):
            a0 = rng.uniform(0.2, math.pi - 0.2)
            spd = rng.uniform(1.4, 3.2)
            x = tx + math.cos(a0) * spd * age
            y = ty - 2 - math.sin(a0) * spd * age + 0.3 * age * age
            if y < ty + 2 and age > 0:
                m = c.rect(int(x), int(y), int(x) + (i % 2), int(y) + ((i + age) % 2))
                c.put(outline_mask(m), K['ink'], 0.8)
                c.put(m, KP3['rock3'] if i % 3 else K['dust1'])
    falling_sparks(c, g, f, hit, tx, ty - 2, 1439, 16, (1.6, 3.4), 10, CYAN_SPARKS, L['cyan'], ty + 1,
                   (0.3, math.pi - 0.3))
    dust_puffs(c, tx, ty - 1, f, hit + 1, 8, 3.0, rise=0.6, life=8, seed=1441, big=1.4)
    vignette(c, 0.72)
    if f == hit:
        shake(c, g, 0, 2)
    elif f == hit + 1:
        shake(c, g, 2, -1)
    elif f == hit + 2:
        shake(c, g, -1, 1)
    return c, g


# ---------------------------------------------------------------------------------------- Gran Muralla
GM_SHIELD = (44.0, 42.0)
GM_BASE = 52


def great_plain(c, g, t):
    """A wide plain at night: mountains far off, a big moon, grass."""
    gradient_bg(c, K['night0'], K['night2'], K['teal1'], 0.6)
    c.put(c.disc(20, 13, 6), K['moon1'])
    c.put(c.disc(20, 13, 6) & ~c.disc(22, 12, 5.5), K['moon2'])
    g.add(c.disc(20, 13, 6), L['blue'], 0.2)
    twinkling_stars(c, g, t, 1451, 18, (2, 2, 86, 26), avoid=(20, 13, 9))
    mts = c.poly([(0, 34), (10, 26), (20, 32), (32, 22), (46, 31), (58, 24), (70, 30), (80, 23), (88, 28),
                  (88, 64), (0, 64)])
    c.put(mts, K['teal0'])
    c.put(mts & ~shift(mts, 0, 1), K['teal3'], 0.6)
    c.put(c.rect(0, 46, AW - 1, AH - 1), KP2['grass0'])
    c.put(c.rect(0, 46, AW - 1, 46), KP2['grass1'])
    for i in range(12):
        x = (i * 31) % AW
        c.put(c.line(x, 56 + (i * 5) % 7, x + 1, 54 + (i * 5) % 7), KP2['grass2'])


def great_wall(c, g, rise_x, sink_x, lit):
    """The great wall across the plain: each column stands once the wave (rise_x) has passed it and before the sink
    wave reaches it; towers every 22 px."""
    top = np.full(AW, 99.0)
    for xi in range(AW):
        k = np.clip((rise_x - xi) / 8.0, 0, 1) * np.clip((xi - sink_x) / 8.0, 0, 1)
        if k <= 0:
            continue
        tower = (xi % 22) in range(8, 15)
        h = (24 if tower else 16) * ease_out(k)
        top[xi] = GM_BASE - h
    m = c.y >= top[np.clip(np.floor(c.x).astype(int), 0, AW - 1)]
    m &= c.y <= GM_BASE
    xi = np.floor(c.x).astype(int)
    merl = m & (c.y < top[np.clip(xi, 0, AW - 1)] + 2) & ((xi % 4) < 2)
    m &= ~merl | ~(c.y < top[np.clip(xi, 0, AW - 1)] + 2)
    m &= ~(((xi % 4) >= 2) & (c.y < top[np.clip(xi, 0, AW - 1)] + 2))
    if not m.any():
        return m
    c.put(outline_mask(m), K['ink'])
    c.put(m, K['steel0'])
    c.put(m & ~shift(m, 0, 1), K['steel2'])
    c.put(m & ~shift(m, 0, 2) & shift(m, 0, 1), K['steel1'])
    for yy in range(GM_BASE - 22, GM_BASE, 4):
        c.put(m & (np.abs(c.y - yy) < 0.5), KW['stone2'])
    tw = m & (((xi % 22) == 8) | ((xi % 22) == 14))
    c.put(tw, KW['stone2'])
    win = m & ((xi % 22) == 11) & (np.abs(c.y - (GM_BASE - 17)) < 1.5)
    c.put(win, K['window'] if lit else K['window2'])
    g.add(win, L['warm'], 0.4 if lit else 0.15)
    return m


def gran_muralla_frame(f):
    t = f / FRAMES
    c, g = Canvas(AW, AH), Canvas(AW, AH)
    great_plain(c, g, t)
    rise_x = -10 + (f - 2) * 12 if f >= 2 else -99
    sink_x = -99 if f < 18 else -10 + (f - 18) * 20
    if f >= 2:
        great_wall(c, g, min(rise_x, 140), sink_x, lit=10 <= f <= 18)
        if 2 <= f <= 10:
            for x in range(max(0, int(rise_x) - 8), min(AW, int(rise_x) + 1), 6):
                dust_puffs(c, x, GM_BASE - 1, 3, 0, 1, 1.2, rise=0.3, life=4, seed=1453 + x, big=0.7)
    x, y = GM_SHIELD
    contact_shadow(c, x, 56, 9)
    lock = 11
    shield(c, g, x, y, W=8.5, H=22.0, gem_gain=1.0 + (1.5 if 11 <= f <= 13 else 0), flash={lock: 0.4}.get(f, 0.0))
    if 0 <= f - lock <= 8:
        a = f - lock
        k = ease_out(min(1.0, a / 2.0))
        fade = 1.0 if a <= 3 else max(0.0, 1 - (a - 3) / 6.0)
        for i, (r, w) in enumerate(((10 + 34 * k, 0.6), (6 + 24 * k, 0.5))):
            ring = diamond_ring(c, x, 38, r, w, squash=0.38)
            c.put(ring, KB['b2'] if i == 0 else KB['b1'], fade)
            c.put(ring & (((c.x + c.y + a * 2) % 7) < 2), KB['b3'], fade)
            g.add(ring, LB, (0.7 if i == 0 else 0.35) * fade)
    if f == lock:
        blue_star(c, g, x, y - 12, 8)
    sparks(c, g, f, lock, x, 30, 1455, 12, (0.8, 2.0), 7, BLUE_SPARKS, LB, gravity=0.04, gain=0.8)
    motes(c, g, t, 1457, 7, (4, 6, 84, 46), KB['b2'], K['steel3'], LB, gain=0.35)
    vignette(c, 0.72)
    return c, g


# ---------------------------------------------------------------------------------------- Ira Carmesí
IC_HEART = (14.0, 14.0)
IC_PIVOT = (22.0, 50.0)
IC_SIGIL = (70.0, 32.0)


def eclipse(c, g, t):
    """A black sun with a crimson corona over a dark plain."""
    gradient_bg(c, KP['blood1'], KP['blood2'], K['night0'], 0.62)
    sx, sy = 57.0, 15.0
    ang = np.arctan2(c.y - sy, c.x - sx)
    rr = np.hypot(c.x - sx, c.y - sy)
    rays = (np.abs(np.sin(ang * 9 + t * 2 * math.pi)) > 0.6) & (rr < 14) & (rr > 9)
    c.put(rays, KW['crim2'], 0.6)
    c.put((rr < 10.5) & (rr > 8.5), KW['crim4'])
    c.put((rr <= 9.5) & (rr > 8.6), KW['crim5'])
    c.put(rr <= 8.5, K['ink'])
    g.add((rr < 10.5) & (rr > 8.5), LW['crimson'], 0.6)
    g.add(rays, LW['crimson'], 0.15)
    land = c.poly([(0, 48), (22, 45), (44, 47), (66, 44), (88, 47), (88, 64), (0, 64)])
    c.put(land, K['ink'])
    c.put(land & ~shift(land, 0, 1), KW['crim2'])
    for x in (40, 58, 80):
        c.put(c.line(x, 47, x + 1, 39), K['roof2'])


def blood_flames(c, g, lab, ang, t, k):
    """Crimson flames licking up off the blade (her blood burning on it)."""
    if k <= 0:
        return
    m = lab > 0
    rng = np.random.default_rng(1461)
    up = np.zeros((AH, AW), bool)
    for d in range(1, 5):
        sh = shift(m, 0, -d)
        wav = np.sin(c.x * 0.9 + t * 24 + d) > (d - 2.5) / 2.5
        up |= sh & wav & ~m
    c.put(up, KW['crim3'], k)
    c.put(up & shift(m, 0, -1), KW['crim5'], k)
    g.add(up, LW['crimson'], 0.8 * k)


def ira_carmesi_frame(f):
    t = f / FRAMES
    c, g = Canvas(AW, AH), Canvas(AW, AH)
    eclipse(c, g, t)
    hit = 12
    sx, sy = IC_SIGIL
    foe_sigil(c, g, sx, sy, t, hit=(f - hit) if f >= hit else -99, r=8.0)
    hx, hy = IC_HEART
    hy += math.sin(t * 2 * math.pi * 2) * 0.6
    fill = 0.9 if f < 2 else max(0.3, 0.9 - 0.6 * (f - 1) / 5.0)
    if f >= 14:
        fill = min(0.6, 0.3 + 0.3 * ease_out((f - 13) / 4.0))
    if f >= 20:
        fill = 0.6 + 0.3 * (f - 19) / 4.0
    green = {14: 0.5, 15: 0.8, 16: 0.8, 17: 0.6, 18: 0.4, 19: 0.2}.get(f, 0.0)
    life_heart(c, g, hx, hy, 1.0, fill=fill, crack=1.0 if 2 <= f < 20 else 0.0, green=green * 0.7)
    px, py = IC_PIVOT
    def ang_at(f):
        if f <= 8:
            return math.radians(-55)
        if f <= 10:
            return math.radians(-55 - 60 * ease_in_out((f - 8) / 2.0))
        if f == 11:
            return math.radians(-40)
        if f <= 15:
            return math.radians(18)
        return math.radians(18 + (-55 - 18) * ease_in_out((f - 15) / 8.0))
    ang = ang_at(f)
    gx, gy = px + math.cos(ang) * 5, py + math.sin(ang) * 5
    # her blood pours from the heart onto the blade (8 de vida)
    if 2 <= f <= 7:
        tx_, ty_ = gx + math.cos(ang) * 20, gy + math.sin(ang) * 20
        for i in range(10):
            u = frac(i / 10 + f * 0.17)
            xx = hx + 3 + (tx_ - hx - 3) * u
            yy = hy + 5 + (ty_ - hy - 5) * u + math.sin(u * math.pi) * 3
            m = c.pixel(xx, yy) | c.pixel(xx + 1, yy)
            c.put(m, KW['crim4'] if i % 2 else KW['crim3'])
            g.add(m, LW['crimson'], 0.6)
    burn = 0.0 if f < 4 else (1.0 if f <= 13 else max(0.0, 1 - (f - 13) / 4.0))
    R = 39.5
    cpal = (KW['crim1'], KW['crim3'], KW['crim5'], K['white'])
    if f == 11:
        crescent(c, g, px, py, R, -ang, math.radians(80), -1, 14.0, cpal, LW['crimson'], gain=1.8)
    elif 12 <= f <= 16:
        q = (f - 12) / 5.0
        crescent(c, g, px, py, R, math.radians(-18), math.radians(130) * (1 - 0.6 * q), -1, 14 * (1 - 0.7 * q), cpal,
                 LW['crimson'], a=1 - 0.7 * q, dash=f >= 15, gain=1.7 * (1 - q))
    lab = sword(c, g, gx, gy, ang, t * 2 + 1.5 * burn, rune_gain=1.0 + 1.5 * burn, fury=1.0 if burn > 0 else 0.0)
    blood_flames(c, g, lab, ang, t, burn)
    blood_aura(c, g, lab > 0, burn * 0.8, t, width=2)
    impact(c, g, sx, sy, f - hit, r=11, kind='crim', gain=1.7)
    if 0 <= f - hit <= 5:
        a = f - hit
        ring = (np.abs(np.hypot(c.x - sx, c.y - sy) - (6 + a * 5)) <= 0.7)
        c.put(ring, KW['crim4'], 1 - a / 6)
        g.add(ring, LW['crimson'], 0.8 * (1 - a / 6))
    sparks(c, g, f, hit, sx, sy, 1463, 16, (1.2, 3.0), 8, CRIM_SPARKS, LW['crimson'], gravity=0.15)
    # half the blood comes back to her as healing
    if 13 <= f <= 17:
        rng = np.random.default_rng(1465)
        for i in range(10):
            ph = rng.random()
            u = frac((f - 13) / 5.0 + ph)
            xx = sx + (hx - sx) * u
            yy = sy + (hy - sy) * u - math.sin(u * math.pi) * 10
            m = c.pixel(xx, yy)
            near = u > 0.6
            c.put(m, KP['heal3'] if near else KW['crim5'])
            g.add(m, LP['heal'] if near else LW['crimson'], 0.7)
    heal_crosses(c, g, f, 15, hx, hy - 3, count=3, life=6, spread=4, seed=1467)
    vignette(c, 0.72)
    if f == hit:
        shake(c, g, 2, 0)
    elif f == hit + 1:
        shake(c, g, -1, 1)
    return c, g


# ---------------------------------------------------------------------------------------- Piel de Acero
PA_CUIRASS = (50.0, 26.0)
PA_GLASS = (16.0, 36.0)


def smithy(c, g, t):
    """A smithy at night: a stone hearth glowing, tools on the wall, a quench barrel."""
    gradient_bg(c, K['night0'], K['night1'], K['night0'], 0.6)
    back = c.rect(0, 0, AW - 1, 51)
    c.put(back, KP['brick1'])
    for row, y in enumerate(range(3, 51, 5)):
        c.put(c.rect(0, y, AW - 1, y), KP['brick0'])
        for x in range(0 if row % 2 else 6, AW, 12):
            c.put(c.rect(x, y + 1, x, y + 4), KP['brick0'])
    warm = np.clip(1 - np.hypot(c.x - 80, c.y - 44) / 26, 0, 1)
    c.put(back & (warm > 0.4), KP['brick2'], np.clip(warm * 1.3, 0, 1))
    hearth = c.rect(74, 38, 88, 51)
    c.put(hearth, K['ink'])
    c.put(c.rect(75, 46, 88, 51), KP2['ember0'])
    c.put(c.rect(75, 46, 88, 51) & ((np.floor(c.x * 1.3 + c.y) % 3) == 0), KP2['ember1'])
    g.add(c.rect(75, 46, 88, 51), L['warm'], 0.35)
    for i, x in enumerate((28, 33, 38)):
        tool = c.line(x, 6, x, 18) | c.rect(x - 1, 4 + i, x + 1, 6 + i)
        c.put(tool, KW['iron2'])
    barrel = c.rect(64, 42, 72, 51)
    c.put(outline_mask(barrel), K['ink'])
    c.put(barrel, KP['oak1'])
    c.put(c.rect(64, 44, 72, 44) | c.rect(64, 49, 72, 49), KW['iron2'])
    floor_slabs(c, 52)


def piel_acero_frame(f):
    t = f / FRAMES
    c, g = Canvas(AW, AH), Canvas(AW, AH)
    smithy(c, g, t)
    x, y = PA_CUIRASS
    power_ring(c, g, x, 52, 16, 2.6, t, 0.5, col=KW['plate2'], light=LW['steel'])
    gx, gy = PA_GLASS
    if f <= 8:                                                          # the turn runs out
        hourglass(c, g, gx, gy, 0.0, max(0.0, 0.35 - 0.35 * f / 8.0), stream=f < 8)
    elif f <= 12:
        hourglass(c, g, gx, gy, 0.0, 0.0, stream=False)
    elif f <= 17:                                                       # turned for the next one
        hourglass(c, g, gx, gy, math.pi * ease_in_out((f - 12) / 5.0), 0.0, stream=False)
    else:
        hourglass(c, g, gx, gy, 0.0, 1.0 - 0.65 * (f - 17) / 6.0, stream=True)
    if f == 8:
        star4(c, g, gx, gy, 5, core=K['white'], col=KW['gold3'], edge=KW['gold2'], light=LW['gold'], gain=1.2)
    coat = 0.0 if f < 9 else min(1.0, (f - 8) / 4.0)
    if f >= 19:
        coat = max(0.0, 1 - (f - 18) / 4.0)
    m = cuirass(c, g, x, y, flash={13: 0.5}.get(f, 0.0))
    if coat > 0:                                                        # liquid steel runs down over it
        front = y - 10 + coat * 28
        sheet = m & (c.y <= front) & (c.y < y + 9)
        c.put(sheet, K['steel2'])
        c.put(sheet & (c.x + c.y < x + y - 2), K['steel3'])
        c.put(sheet & (c.x + c.y < x + y - 9), K['steel4'])
        c.put(sheet & (c.x > x + 5), K['steel1'])
        drips = m & (np.abs(c.y - front) < 1.0) & (c.y < y + 9)
        c.put(drips, K['white'])
        g.add(sheet, LW['steel'], 0.3)
        g.add(drips, L['white'], 0.6)
    if f == 13:
        blue_star(c, g, x, y - 9, 7)
    if 0 <= f - 13 <= 8:
        ward_rings(c, g, x, y + 2, f - 13, size=17, squash=1.05, life=9)
    sparks(c, g, f, 13, x, y, 1471, 10, (0.6, 1.6), 6, BLUE_SPARKS, LB, gravity=-0.03, gain=0.8)
    motes(c, g, t, 1473, 6, (20, 6, 72, 48), KW['plate3'], KW['spark1'], LW['steel'], gain=0.35)
    vignette(c, 0.72)
    return c, g


# ---------------------------------------------------------------------------------------- Bastión
BS_SHIELD = (44.0, 40.0)


def castle(c, g, rise, flags, t, lit, glow):
    """A castle rising behind the shield: a keep, two towers with crimson roofs and flags, a gatehouse."""
    parts = [((44, 46, 20, 26), 0.0), ((24, 50, 10, 30), -0.15), ((64, 50, 10, 30), -0.15)]
    allm = np.zeros((AH, AW), bool)
    for (cx, base, w, h), delay in parts:
        k = ease_out(np.clip(rise + delay, 0, 1)) if rise + delay > 0 else 0.0
        if k <= 0:
            continue
        top = base - h * k
        body = c.rect(int(cx - w / 2), int(top), int(cx + w / 2) - 1, int(base))
        crown = np.zeros((AH, AW), bool)
        for mx in range(int(cx - w / 2), int(cx + w / 2), 4):
            crown |= c.rect(mx, int(top) - 2, mx + 1, int(top) - 1)
        m = (body | crown) & (c.y <= base)
        allm |= m
        c.put(outline_mask(m), K['ink'])
        c.put(m, KW['stone2'])
        c.put(m & (c.x < cx - w / 2 + 2), KW['stone3'])
        c.put(m & (c.x > cx + w / 2 - 2), KW['stone1'])
        for yy in range(int(top) + 3, int(base), 4):
            c.put(m & (np.abs(c.y - yy) < 0.5), KW['stone1'])
        if w <= 10 and k > 0.6:                                         # tower roof and flag
            roof = c.poly([(cx - w / 2 - 1, top - 1), (cx, top - 9), (cx + w / 2, top - 1)])
            c.put(outline_mask(roof), K['ink'])
            c.put(roof, KW['crim3'])
            c.put(roof & (c.x < cx), KW['crim4'])
            pole = c.line(cx, top - 9, cx, top - 14)
            c.put(pole, KW['iron3'])
            fl = np.zeros((AH, AW), bool)
            for i in range(int(6 * flags)):
                yy = top - 14 + math.sin(i * 0.9 - t * 4 * math.pi) * 0.7
                fl |= c.rect(int(cx) + 1 + i, int(yy), int(cx) + 1 + i, int(yy) + 2 - (i > 3))
            c.put(fl, KW['crim4'])
        win = c.rect(int(cx) - 1, int(top) + 5, int(cx), int(top) + 7)
        if k > 0.7:
            c.put(win, K['window'] if lit else K['window2'])
            g.add(win, L['warm'], 0.4 if lit else 0.15)
    if glow > 0:
        c.put(outline_mask(allm), KB['b2'], glow)
        g.add(outline_mask(allm), LB, 0.7 * glow)
    return allm


def bastion_frame(f):
    t = f / FRAMES
    c, g = Canvas(AW, AH), Canvas(AW, AH)
    great_plain(c, g, t)
    lock = 11
    rise = 0.0 if f < 2 else min(1.3, (f - 1) / 7.0)
    if f >= 19:
        rise = max(0.0, 1.3 - (f - 18) / 3.0)
    flags = 0.0 if f < 8 else min(1.0, (f - 7) / 3.0)
    if f >= 19:
        flags = 0.0
    castle(c, g, rise, flags, t, lit=lock <= f <= 18, glow={11: 1.0, 12: 0.7, 13: 0.45, 14: 0.25}.get(f, 0.0))
    if 2 <= f <= 8:
        dust_puffs(c, 44, 51, f, 2, 6, 3.0, rise=0.4, life=7, seed=1481, big=1.2)
    x, y = BS_SHIELD
    contact_shadow(c, x, 56, 10)
    shield(c, g, x, y, W=10.0, H=26.0, gem_gain=1.0 + (1.6 if 11 <= f <= 13 else 0), flash={lock: 0.45}.get(f, 0.0))
    if 0 <= f - lock <= 9:
        ward_rings(c, g, x, 30, f - lock, size=34, squash=0.75, life=10)
    if f == lock:
        blue_star(c, g, x, 8, 9, gain=1.5)
    sparks(c, g, f, lock, x, 22, 1483, 14, (0.8, 2.2), 8, BLUE_SPARKS, LB, gravity=0.04, gain=0.9)
    motes(c, g, t, 1485, 8, (4, 6, 84, 46), KB['b2'], KW['gold3'], LB, gain=0.35)
    vignette(c, 0.72)
    if f in (2, 9):
        shake(c, g, 0, 1)
    return c, g


# ---------------------------------------------------------------------------------------- Berserker
BK_HELM = (44.0, 25.0)
BK_DROPS = (3, 9, 15)


def weapon_field(c, g, t):
    """A field after battle under a red sky: swords and spears stuck in the ground, a broken banner."""
    gradient_bg(c, K['night0'], KP['blood1'], K['night0'], 0.6)
    c.put(np.abs(c.y - 41) < 2.5, KW['crim1'], 0.6)
    twinkling_stars(c, g, t, 1491, 10, (2, 2, 86, 22), light=LW['crimson'])
    hills = c.poly([(0, 44), (20, 41), (40, 43), (60, 40), (88, 43), (88, 64), (0, 64)])
    c.put(hills, K['ink'])
    c.put(hills & ~shift(hills, 0, 1), KW['crim2'])
    for x, lean, h in ((6, 1, 10), (13, -1, 8), (22, 2, 12), (66, -2, 11), (74, 1, 9), (83, -1, 12)):
        bl = c.line(x, 50, x + lean, 50 - h)
        c.put(bl, K['steel0'])
        c.put(c.line(x - 2 + lean * 0.3, 50 - h * 0.25, x + 2 + lean * 0.3, 50 - h * 0.25), K['roof2'])
    c.put(c.rect(0, 50, AW - 1, AH - 1), K['night0'])
    c.put(c.rect(0, 50, AW - 1, 50), KW['crim1'])


def berserker_frame(f):
    t = f / FRAMES
    c, g = Canvas(AW, AH), Canvas(AW, AH)
    weapon_field(c, g, t)
    x, y = BK_HELM
    count = sum(1 for d in BK_DROPS if f >= d + 4)
    if f >= 21:
        count = 0
    power_ring(c, g, x, 53, 16, 2.4, t, 0.55 + 0.12 * count, col=KW['crim4'], light=LW['crimson'])
    post = c.rect(int(x) - 1, int(y) + 11, int(x) + 1, 50)
    bar = c.rect(int(x) - 10, int(y) + 14, int(x) + 10, int(y) + 15)
    st = post | bar
    c.put(outline_mask(st), K['ink'])
    c.put(st, KP['oak2'])
    c.put(st & ~shift(st, 0, 1), KP['oak3'])
    flare = max([max(0.0, 1 - (f - d - 4) / 3.0) for d in BK_DROPS if 0 <= f - d - 4 <= 3], default=0.0)
    glow = min(1.0, 0.35 + 0.2 * count + 0.6 * flare)
    hm = helm(c, g, x, y, glow=glow, fury=1.0 if count > 0 or flare > 0 else 0.0, plume=flare, t=t, s=1.3)
    paint = hm & (np.abs(c.y - (y + 3)) < 0.8) & (np.abs(np.abs(c.x - x) - 6) < 1.6)
    c.put(paint, KW['crim4'])
    if count > 0:
        blood_aura(c, g, hm, 0.3 + 0.2 * count, t, width=1)
    for i, d in enumerate(BK_DROPS):
        land = drip(c, g, f, d, x + (i - 1) * 4 + 0.5, -3, y - 11 + abs(i - 1) * 2, fall=3, s=1.2, swell=1,
                    seed=1493 + i)
        if land == 0:
            fury_star(c, g, x + (i - 1) * 4, y - 10, 4 + i)
        rising_chevrons(c, g, f, x + (i - 1) * 10, y - 16, d + 4, 6, KW['crim5'], LW['crimson'], count=1, gain=1.0)
    vignette(c, 0.72)
    return c, g


# ---------------------------------------------------------------------------------------- Doblar Escudo
DE_SHIELD = (44.0, 23.0)
DE_ICE = 42


def frozen_lake(c, g, t):
    """A frozen lake at night ringed with dark pines; the ice mirrors the sky."""
    gradient_bg(c, K['night0'], K['teal1'], K['teal2'], 0.6)
    twinkling_stars(c, g, t, 1501, 14, (2, 2, 86, 30), avoid=(44, 23, 16))
    shore = c.poly([(0, DE_ICE), (14, DE_ICE - 6), (26, DE_ICE - 3), (62, DE_ICE - 3), (76, DE_ICE - 7),
                    (88, DE_ICE - 2), (88, DE_ICE), (0, DE_ICE)])
    c.put(shore, K['roof'])
    for x in (4, 10, 18, 70, 78, 84):
        c.put(c.poly([(x - 4, DE_ICE - 2), (x, DE_ICE - 16 - (x % 5)), (x + 4, DE_ICE - 2)]), K['roof'])
    ice = c.rect(0, DE_ICE, AW - 1, AH - 1)
    c.put(ice, KP4['ice1'])
    c.put(ice & (c.y < DE_ICE + 2), KP4['ice2'])
    c.put(ice & ((np.floor(c.x * 0.4 - c.y * 1.1) % 11) == 0), KP4['ice2'])
    c.put(ice & (np.abs(c.y - DE_ICE) < 0.6), KP4['ice3'])


def doblar_escudo_frame(f):
    t = f / FRAMES
    c, g = Canvas(AW, AH), Canvas(AW, AH)
    frozen_lake(c, g, t)
    x, y = DE_SHIELD
    y += math.sin(t * 2 * math.pi * 2) * 0.6
    merge = 10
    # the reflection in the ice, then rising out of it
    if f < 5 or f >= 20:
        ry, ra = 2 * DE_ICE - y + 4, 0.45
    elif f < merge:
        k = ease_in_out((f - 4) / 6.0)
        ry, ra = (2 * DE_ICE - y + 4) + (y - (2 * DE_ICE - y + 4)) * k, 0.45 + 0.4 * k
    else:
        ry, ra = None, 0.0
    if f >= 16 and f < 20:                                              # it sinks back into the ice
        k = (f - 15) / 4.0
        ry, ra = y + (2 * DE_ICE - y + 4 - y) * k, 0.6 - 0.15 * k
    if ry is not None:
        lab = heater_labels(c, x, ry, W=10.5, H=26.0, sy=-1.0 if ry > DE_ICE else 1.0)
        m = (lab > 0)
        if ry > DE_ICE:
            m &= c.y > DE_ICE
        c.put(outline_mask(m) & (c.y > DE_ICE if ry > DE_ICE else True), KP4['ice3'], ra)
        c.put(m, KP4['ice2'], ra)
        c.put(m & (lab == 3), KW['crim2'], ra * 0.6)
        c.put(m & (lab == 4), KP4['ice3'], ra)
        g.add(outline_mask(m), L['cyan'], 0.3 * ra)
    contact_shadow(c, x, DE_ICE + 1, 8)
    shield(c, g, x, y, W=10.5, H=26.0, gem_gain=1.0 + (1.6 if 9 <= f <= 12 else 0), flash={merge: 0.6}.get(f, 0.0))
    # the ward: what she had, then twice as much
    if f < merge or f >= 20:
        ward_n(c, g, x, y + 2, 9.0, a=0.9)
    else:
        a = f - merge
        k = ease_out(min(1.0, a / 2.0))
        fade = 1.0 if a <= 6 else max(0.35, 1 - (a - 6) / 4.0)
        ward_n(c, g, x, y + 2, 9.0, a=fade)
        ward_n(c, g, x, y + 2, 9.0 + 12.0 * k, a=fade)
    if f == merge:
        blue_star(c, g, x, y - 12, 8)
    sparks(c, g, f, merge, x, y, 1503, 12, (0.8, 2.0), 7, BLUE_SPARKS, LB, gravity=0.0, gain=0.8)
    motes(c, g, t, 1505, 7, (10, 6, 78, 40), KB['b2'], KP4['ice3'], LB, gain=0.35)
    vignette(c, 0.72)
    return c, g


# ---------------------------------------------------------------------------------------- Ejecución
EJ_SIGIL = (60.0, 30.0)
EJ_HEART = (14.0, 14.0)


def gallows_hill(c, g, t, flash):
    """A hill under storm clouds: a gallows frame, an executioner's block, lightning far away."""
    gradient_bg(c, KP['storm1'], KP['storm2'], KP['storm0'], 0.6)
    for i, (y, sp) in enumerate(((7, 1), (14, 2))):
        off = (t * sp * AW) % AW
        band = (np.abs(c.y - y - np.sin((c.x + off) / 7.0) * 1.5) < 3) & (((c.x + off + i * 11) % 29) < 20)
        c.put(band, KP['storm3'], 0.6)
    if flash > 0:
        bolt = c.line(80, 0, 76, 8) | c.line(76, 8, 81, 14) | c.line(81, 14, 78, 22)
        c.put(bolt, K['white'], flash)
        g.add(dilate(bolt), L['blue'], flash)
    hill = c.poly([(0, 50), (24, 44), (60, 43), (88, 47), (88, 64), (0, 64)])
    c.put(hill, K['ink'])
    c.put(hill & ~shift(hill, 0, 1), KP['storm3'])
    frame = c.rect(80, 18, 81, 44) | c.rect(70, 18, 81, 19) | c.line(80, 24, 75, 19)
    c.put(frame, KP['oak1'])
    c.put(c.line(72, 20, 72, 26), KP['oak2'])
    block = c.rect(52, 42, 68, 47)
    c.put(outline_mask(block), K['ink'])
    c.put(block, KP['oak1'])
    c.put(c.rect(52, 42, 68, 42), KP['oak3'])
    c.put(c.rect(59, 41, 61, 42), K['ink'])


def ejecucion_frame(f):
    t = f / FRAMES
    c, g = Canvas(AW, AH), Canvas(AW, AH)
    gallows_hill(c, g, t, {10: 1.0, 11: 0.4}.get(f, 0.0))
    hit = 10
    age = f - hit
    sx, sy = EJ_SIGIL
    frac_ = 0.32 if f < hit else (0.0 if f < 19 else min(1.0, (f - 18) / 4.0))
    if f >= 19:
        frac_ = 0.32 * min(1.0, (f - 18) / 4.0)
    if f < hit or f >= 19:
        a = 1.0 if f < hit else min(1.0, (f - 18) / 4.0)
        foe_sigil(c, g, sx, sy, t, hit=-99, r=7.5, a=0.9 * a)
        hp_bar(c, g, sx, sy - 13, 20, frac_ if f < hit else frac_, notch=0.0)
    else:
        hp_bar(c, g, sx, sy - 13, 20, 0.0, notch=0.0, flash=0.6 if age == 0 else 0.0)
        if age <= 6:                                                    # the sigil shatters into crimson smoke
            k = age / 6.0
            rng = np.random.default_rng(1511)
            for i in range(14):
                a0 = rng.uniform(0, 2 * math.pi)
                r = 2 + age * rng.uniform(1.0, 2.2)
                px, py = sx + math.cos(a0) * r, sy + math.sin(a0) * r - age * 0.5
                m = c.rect(int(px), int(py), int(px) + (i % 2), int(py))
                c.put(m, KW['crim4'] if i % 2 else KW['crim2'], 1 - k)
                g.add(m, LW['crimson'], 0.6 * (1 - k))
    # the sword: raised high (6-9), the downward cut (10), resting in the block (11-15), back (16-23)
    def pose(f):
        if f <= 5:
            return 22.0, 42.0, math.radians(-62)
        if f <= 9:
            k = ease_in_out((f - 5) / 4.0)
            return 22 + 24 * k, 42 - 6 * k, math.radians(-62 - 40 * k)
        if f <= 15:
            return sx, sy - 22, math.radians(90)
        k = ease_in_out((f - 15) / 8.0)
        return sx + (22 - sx) * k, (sy - 22) + (42 - sy + 22) * k, math.radians(90 + (-62 - 90) * k)
    gx, gy, ang = pose(f)
    if f == hit:
        speed_lines(c, g, gx, gy + 10, 0, 1, 1.0, 1513, count=7, spread=6, length=(8, 14), back=4)
        slash_line(c, g, sx, sy + 2, math.pi / 2, 18, 2.4, (None, KW['crim3'], KW['crim5'], K['white']),
                   LW['crimson'])
    fury = 1.0 if 6 <= f <= 15 else 0.0
    sword(c, g, gx, gy, ang, t * 2 + 1.5 * fury, rune_gain=1.0 + 1.4 * fury, fury=fury,
          cut=(42 - gy) / 1.0 if 11 <= f <= 15 else None)
    if f == 9:
        fury_star(c, g, *blade_point(gx, gy, ang), 5)
    impact(c, g, sx, sy, age, r=9, kind='crim', gain=1.6)
    if age == 1:
        fury_star(c, g, sx - 10 + 20 * 0.0, sy - 18, 5)
    # it dies: 10 de vida come back to her heart
    hx, hy = EJ_HEART
    hy += math.sin(t * 2 * math.pi * 2) * 0.6
    fill = 0.45 if f < 13 else min(0.9, 0.45 + 0.45 * ease_out((f - 12) / 4.0))
    if f >= 20:
        fill = 0.9 - 0.45 * (f - 19) / 4.0
    green = {13: 0.6, 14: 0.9, 15: 0.9, 16: 0.7, 17: 0.4, 18: 0.2}.get(f, 0.0)
    life_heart(c, g, hx, hy, 1.0, fill=fill, green=green * 0.7)
    if 11 <= f <= 16:
        rng = np.random.default_rng(1515)
        for i in range(10):
            ph = rng.random()
            u = frac((f - 11) / 5.0 + ph)
            xx = sx + (hx - sx) * u
            yy = sy + (hy - sy) * u - math.sin(u * math.pi) * 8
            m = c.pixel(xx, yy)
            c.put(m, KP['heal3'])
            g.add(m, LP['heal'], 0.7)
    heal_crosses(c, g, f, 14, hx, hy - 3, count=3, life=6, spread=4, seed=1517)
    vignette(c, 0.72)
    if f == hit:
        shake(c, g, 0, 2)
    return c, g


# ---------------------------------------------------------------------------------------- Escudo Impenetrable
EI_SHIELD = (44.0, 34.0)


def siege_night(c, g, t):
    """A siege at night: a burning wall behind, catapult fire in the sky, smoke."""
    gradient_bg(c, K['night0'], KP4['fire0'], K['night1'], 0.6)
    wall = c.rect(0, 30, AW - 1, 50)
    for x in range(0, AW, 8):
        wall |= c.rect(x, 26, x + 4, 30)
    c.put(wall, K['roof'])
    for i, x in enumerate((10.0, 72.0)):
        flame(c, g, x, 30, 7, 3, t, i * 0.7, gain=0.9)
    for i, x in enumerate((30, 58)):
        w = c.rect(x, 36, x + 1, 39)
        lit = 0.6 + 0.4 * math.sin(t * 4 * math.pi + i)
        c.put(w, KP4['fire3'] if lit > 0.7 else KP4['fire2'])
        g.add(w, L['warm'], 0.4 * lit)
    floor_slabs(c, 51)


def dome(c, g, x, base, rx, ry, t, a=1.0, ripple=None):
    """A dome of hexagonal light standing on the ground over the shield; ripple = (angle, age) of a hit (y-up angle
    on the dome's rim)."""
    rr = np.hypot((c.x - x) / rx, (c.y - base) / ry)
    inside = (rr <= 1) & (c.y <= base)
    hx_, hy_, n = hex_cells(c, x, base, 3.4)
    net = inside & (n > 0.8)
    rim = inside & (rr > 1 - 1.3 / ry)
    c.put(net, KB['b1'], 0.5 * a)
    c.put(rim, KB['b2'], a)
    c.put(rim & (c.y < base - ry * 0.6), KB['b3'], a)
    g.add(rim, LB, 0.6 * a)
    g.add(net, LB, 0.15 * a)
    if ripple is not None:
        ang, age = ripple
        if 0 <= age <= 4:
            px, py = x + math.cos(ang) * rx, base - math.sin(ang) * ry
            d = np.hypot(c.x - px, c.y - py)
            wave = inside & (np.abs(d - age * 4) < 1.2)
            c.put(wave | (net & (d < age * 4 + 2)), KB['b3'], 1 - age / 5)
            g.add(wave, LB, 0.9 * (1 - age / 5))


def escudo_impenetrable_frame(f):
    t = f / FRAMES
    c, g = Canvas(AW, AH), Canvas(AW, AH)
    siege_night(c, g, t)
    x, y = EI_SHIELD
    base, rx, ry = 51.0, 27.0, 30.0
    contact_shadow(c, x, 51, 9)
    slab = shield(c, g, x, y + 2, W=10.0, H=26.0, gem_gain=1.0 + (1.5 if 14 <= f <= 16 else 0),
                  flash={16: 0.3}.get(f, 0.0))
    hits = [(math.radians(150), 5, 'arrow'), (math.radians(35), 8, 'fire'), (math.radians(95), 11, 'axe')]
    rip = None
    for ang, hf, kind in hits:
        if 0 <= f - hf <= 4:
            rip = (ang, f - hf)
    dome(c, g, x, base, rx, ry, t, a=1.0, ripple=rip)
    for ang, hf, kind in hits:
        a = f - hf
        px, py = x + math.cos(ang) * rx, base - math.sin(ang) * ry
        if kind == 'arrow':
            if -2 <= a <= -1:
                k = (a + 3) / 3
                arrow(c, g, px - 14 * (1 - k) - 1, py - 4 * (1 - k), 0.3)
            if 1 <= a <= 5:
                arrow(c, g, px - 3 - a * 1.5, py + a * a * 0.6, math.pi * 0.7 + a * 0.6, a=1 - a / 6)
            spark_cols = WARM_SPARKS
        elif kind == 'fire':
            if -2 <= a <= -1:
                k = (a + 3) / 3
                fx, fy = px + 14 * (1 - k), py - 8 * (1 - k)
                fb = c.disc(fx, fy, 2.4)
                c.put(fb, KP4['fire2'])
                c.put(c.disc(fx - 0.5, fy - 0.5, 1.3), KP4['fire3'])
                g.add(dilate(fb), L['warm'], 0.9)
                c.put(c.line(fx, fy, fx + 5, fy - 3), KP4['fire1'], 0.7)
            spark_cols = FIRE_SPARKS
        else:
            if -2 <= a <= -1:
                k = (a + 3) / 3
                greataxe(c, g, px + 3, py - 12 * (1 - k) - 15, math.pi / 2 + 0.6 * (1 - k), t, s=0.6)
            if 1 <= a <= 5:
                greataxe(c, g, px + 8 + a * 2.5, py - 14 + a * a * 1.0, math.pi / 2 + 0.6 + a * 0.7, t, s=0.6)
            spark_cols = WARM_SPARKS
        if a == 0:
            impact(c, g, px, py, 0, r=5, kind='warm')
        if 0 <= a <= 2:
            star4(c, g, px, py, {0: 7, 1: 4, 2: 2}[a], core=K['white'], col=KB['b3'], edge=KB['b2'], light=LB)
        sparks(c, g, f, hf, px, py, 1521 + hf, 9, (0.8, 2.0), 6, spark_cols, L['warm'], gravity=0.15)
    # Aguante: the gold pulse climbs from the shield's gem to the top of the dome, +2 de Fuerza
    gx0, gy0 = shield_gem(x, y + 2, 26.0)
    top = base - ry
    gold_pulse(c, g, gx0, gy0, x, top + 1, {14: 0.0, 15: 0.6, 16: 1.0}.get(f, -1))
    if f == 16:
        fury_star(c, g, x, top + 1, 7)
    for side, born in ((-1, 16), (1, 17)):
        rising_chevrons(c, g, f, x + side * 7, top - 3, born, 6, KW['crim5'], LW['crimson'], count=1, gain=1.0)
    vignette(c, 0.72)
    return c, g


# ---------------------------------------------------------------------------------------- Golpe Vampírico
GV_HEART = (14.0, 15.0)
GV_PIVOT = (22.0, 50.0)
GV_SIGIL = (68.0, 32.0)


def ruined_cathedral(c, g, t):
    """A ruined cathedral at night: broken pointed arches, a crimson rose window, a huge red moon behind."""
    gradient_bg(c, K['night0'], K['vio1'], K['night0'], 0.6)
    c.put(c.disc(44, 18, 15), KW['crim1'], 0.35)
    moon = c.disc(44, 18, 10)
    c.put(moon, KW['crim2'])
    c.put(moon & (c.x + c.y < 58), KW['crim3'])
    g.add(moon, LW['crimson'], 0.12)
    for x0 in (4, 64):
        arch = c.rect(x0, 14, x0 + 3, 50) | c.rect(x0 + 17, 14, x0 + 20, 50)
        tip = (np.abs(c.x - x0 - 10) < (c.y - 2) * 0.55) & (c.y < 18) & (c.y > 2)
        tip &= ~((np.abs(c.x - x0 - 10) < (c.y - 6) * 0.55 - 1.5) & (c.y > 6))
        a = arch | tip
        if x0 == 64:
            a &= ~c.rect(x0 + 8, 0, x0 + 20, 12)                          # broken
        c.put(a, K['roof2'])
        c.put(outline_mask(a) & ~a, K['vio3'], 0.5)
    floor_slabs(c, 51, tone=(KP['temple0'], KP['temple1'], KP['temple2'], KP['temple3']))


def bat(c, g, x, y, phase, col, light, a=1.0):
    """A small blood bat: a body pixel and two wings that flap (phase)."""
    up = math.sin(phase) > 0
    m = c.pixel(x, y)
    if up:
        m |= c.pixel(x - 1, y - 1) | c.pixel(x - 2, y - 1) | c.pixel(x + 1, y - 1) | c.pixel(x + 2, y - 1)
    else:
        m |= c.pixel(x - 1, y) | c.pixel(x - 2, y + 1) | c.pixel(x + 1, y) | c.pixel(x + 2, y + 1)
    c.put(m, col, a)
    g.add(m, light, 0.6 * a)


def golpe_vampirico_frame(f):
    t = f / FRAMES
    c, g = Canvas(AW, AH), Canvas(AW, AH)
    ruined_cathedral(c, g, t)
    hit = 10
    sx, sy = GV_SIGIL
    foe_sigil(c, g, sx, sy, t, hit=(f - hit) if f >= hit else -99, r=7.5)
    px, py = GV_PIVOT
    def ang_at(f):
        if f <= 6:
            return math.radians(-55)
        if f <= 8:
            return math.radians(-55 - 55 * ease_in_out((f - 6) / 2.0))
        if f == 9:
            return math.radians(-40)
        if f <= 13:
            return math.radians(16)
        return math.radians(16 + (-55 - 16) * ease_in_out((f - 13) / 9.0)) if f <= 22 else math.radians(-55)
    ang = ang_at(f)
    gx, gy = px + math.cos(ang) * 5, py + math.sin(ang) * 5
    cpal = (KW['crim1'], KW['crim3'], KW['crim5'], K['white'])
    if f == 9:
        crescent(c, g, px, py, 39.5, -ang, math.radians(75), -1, 11.0, cpal, LW['crimson'], gain=1.6)
    elif 10 <= f <= 13:
        q = (f - 10) / 4.0
        crescent(c, g, px, py, 39.5, math.radians(-16), math.radians(120) * (1 - 0.6 * q), -1, 11 * (1 - 0.7 * q),
                 cpal, LW['crimson'], a=1 - 0.7 * q, dash=f >= 12, gain=1.5 * (1 - q))
    fury = 1.0 if 4 <= f <= 13 else 0.0
    sword(c, g, gx, gy, ang, t * 2 + 1.5 * fury, rune_gain=1.0 + 1.4 * fury, fury=fury,
          glint=(-4 + (f - 19) * 9.0 if 19 <= f <= 23 else None))
    impact(c, g, sx, sy, f - hit, r=9, kind='crim')
    sparks(c, g, f, hit, sx, sy, 1531, 12, (1.0, 2.6), 7, CRIM_SPARKS, LW['crimson'], gravity=0.15)
    # the swarm of blood bats carries its life back to her heart
    hx, hy = GV_HEART
    hy += math.sin(t * 2 * math.pi * 2) * 0.6
    if 11 <= f <= 18:
        rng = np.random.default_rng(1533)
        for i in range(12):
            d = rng.uniform(0, 2.5)
            u = (f - 11 - d) / 5.0
            if not 0 <= u <= 1:
                continue
            k = ease_in_out(u)
            bx = sx + (hx - sx) * k + math.sin(u * 7 + i) * 3
            by = sy + (hy - sy) * k - math.sin(k * math.pi) * (8 + i % 4 * 3)
            near = u > 0.75
            bat(c, g, bx, by, f * 2.2 + i, KP['heal3'] if near else KW['crim4'], LP['heal'] if near else LW['crimson'])
    fill = 0.25 if f < 13 else min(1.0, 0.25 + 0.75 * ease_out((f - 12) / 5.0))
    if f >= 20:
        fill = 1.0 - 0.75 * (f - 19) / 4.0
    green = {14: 0.6, 15: 0.9, 16: 1.0, 17: 0.8, 18: 0.5, 19: 0.25}.get(f, 0.0)
    life_heart(c, g, hx, hy, 1.0, fill=fill, green=green * 0.7, beat=1.0 if f in (16, 18) else 0.0)
    if f == 17:
        heal_star(c, g, hx, hy, 6)
    heal_crosses(c, g, f, 15, hx, hy - 3, count=4, life=7, spread=5, seed=1535)
    motes(c, g, t, 1537, 6, (4, 6, 84, 46), KW['crim4'], K['violet3'], LW['crimson'], gain=0.35)
    vignette(c, 0.72)
    if f == hit:
        shake(c, g, 1, 0)
    return c, g


# ---------------------------------------------------------------------------------------- Muralla Infranqueable
MI_SHIELD = (40.0, 32.0)
MI_GLASS = (76.0, 36.0)


def blizzard_wall(c, g, t):
    """The top of a fortress wall in a blizzard: merlons in the snow, a frozen tower, white haze."""
    gradient_bg(c, KP4['snow0'], KP4['ice1'], KP4['ice0'], 0.6)
    tower = c.rect(68, 6, 86, 51) | c.rect(66, 4, 88, 6)
    for x in (66, 72, 78, 84):
        tower |= c.rect(x, 1, x + 2, 4)
    c.put(tower, KP4['ice1'])
    c.put(tower & ~shift(tower, 0, 1), KP4['snow2'])
    c.put(c.rect(74, 14, 76, 20), K['ink'])
    wall = c.rect(0, 44, AW - 1, 51)
    for x in range(0, 66, 8):
        wall |= c.rect(x, 40, x + 4, 44)
    c.put(wall, KP4['ice2'])
    c.put(wall & ~shift(wall, 0, 1), KP4['snow3'])
    c.put(c.rect(0, 52, AW - 1, AH - 1), KP4['snow1'])
    c.put(c.rect(0, 52, AW - 1, 52), KP4['snow3'])


def muralla_infranqueable_frame(f):
    t = f / FRAMES
    c, g = Canvas(AW, AH), Canvas(AW, AH)
    blizzard_wall(c, g, t)
    x, y = MI_SHIELD
    gx, gy = MI_GLASS
    if f < 9:
        hourglass(c, g, gx, gy, 0.0, max(0.0, 0.3 - 0.3 * f / 8.0), stream=f < 8)
    elif f < 14:
        hourglass(c, g, gx, gy, math.pi * ease_in_out((f - 8) / 5.0), 0.0, stream=False)
    else:
        hourglass(c, g, gx, gy, 0.0, 1.0 - 0.7 * (f - 13) / 10.0, stream=True)
    contact_shadow(c, x, 52, 9)
    shield(c, g, x, y, W=10.0, H=26.0, gem_gain=1.0 + (1.5 if 3 <= f <= 5 else 0), flash={3: 0.4}.get(f, 0.0))
    seal = 7
    if 3 <= f <= 21:
        a = f - 3
        k = ease_out(min(1.0, a / 2.0))
        fade = 1.0 if f <= 19 else max(0.0, 1 - (f - 19) / 3.0)
        size = 26 * k
        ward_n(c, g, x, y + 2, size, a=fade, squash=1.0)
        if f >= seal:                                                   # sealed in gold: it lasts another turn
            sk = min(1.0, (f - seal + 1) / 2.0) * fade
            ring = diamond_ring(c, x, y + 2, 5 + size + 2, 0.6, squash=1.0)
            c.put(ring, KW['gold2'], sk)
            g.add(ring, LW['gold'], 0.5 * sk)
            r = 5 + size + 2
            for px_, py_ in ((x, y + 2 - r), (x + r, y + 2), (x, y + 2 + r), (x - r, y + 2)):
                rv = c.disc(px_, py_, 1.6)
                c.put(outline_mask(rv), K['ink'], sk)
                c.put(rv, KW['gold3'], sk)
                g.add(rv, LW['gold'], 0.6 * sk)
            if f == seal:
                gold_star(c, g, x, y + 2 - r, 6)
    if f == 3:
        blue_star(c, g, x, y - 12, 7)
    if 9 <= f <= 13:                                                    # the turn changes: the ward does not fade
        k = 1 - abs(f - 11) / 3.0
        c.put(diamond_ring(c, x, y + 2, 33, 1.0, squash=1.0), K['white'], 0.5 * k)
    snowfall(c, g, t, count=60, wind=1.2, seed=1541, a=0.95)
    if 14 <= f <= 18:                                                   # gusts of snow break on the ward
        for i in range(5):
            yy = 18 + i * 6
            xx = 88 - (f - 14) * 10 - i * 3
            m = c.line(xx, yy, xx + 6, yy - 1)
            c.put(m & (c.x > x + 30), KP4['snow3'], 0.8)
            if xx < x + 34:
                star4(c, g, x + 31, yy, 2, core=K['white'], col=KB['b3'], edge=KB['b2'], light=LB, gain=0.7)
    vignette(c, 0.72)
    return c, g


# ---------------------------------------------------------------------------------------- Voluntad de Hierro
VH_HEART = (44.0, 32.0)
VH_DROPS = (2, 8, 14)


def mountain_shrine(c, g, t):
    """A mountain top at night above the clouds: two stone lanterns with a cold flame, peaks far off."""
    gradient_bg(c, K['night0'], K['night2'], K['teal1'], 0.6)
    twinkling_stars(c, g, t, 1551, 18, (2, 2, 86, 30), avoid=(44, 30, 16))
    peaks = c.poly([(0, 40), (12, 30), (22, 37), (66, 37), (76, 29), (88, 36), (88, 64), (0, 64)])
    c.put(peaks, K['teal0'])
    c.put(peaks & ~shift(peaks, 0, 1), K['teal3'], 0.6)
    for i, y in enumerate((42, 46)):
        off = (t * (1 + i) * AW) % AW
        cl = (np.abs(c.y - y) < 2) & (((c.x + off + i * 13) % 31) < 22)
        c.put(cl, K['teal3'], 0.4)
    for i, x in enumerate((12.0, 76.0)):
        lamp = c.rect(int(x) - 1, 40, int(x) + 1, 51) | c.rect(int(x) - 3, 33, int(x) + 3, 39) | \
            c.rect(int(x) - 4, 31, int(x) + 4, 32)
        c.put(outline_mask(lamp), K['ink'])
        c.put(lamp, KP3['rock2'])
        c.put(lamp & (c.x < x - 1), KP3['rock3'])
        win = c.rect(int(x) - 1, 35, int(x) + 1, 37)
        lit = 0.7 + 0.3 * math.sin(t * 4 * math.pi + i)
        c.put(win, K['cyan2'] if lit > 0.8 else K['cyan1'])
        g.add(win, L['cyan'], 0.5 * lit)
    c.put(c.rect(0, 52, AW - 1, AH - 1), KP3['rock1'])
    c.put(c.rect(0, 52, AW - 1, 52), KP3['rock3'])


def iron_cage(c, g, x, y, s, flash=0.0):
    """An iron cage round the heart: a cap plate on top, five bars down to a ring at the bottom, a hoop, rivets."""
    top_y = y - 9 * s
    bot_y = y + 8.5 * s
    cap = c.rect(int(x - 8 * s), int(top_y - 1), int(x + 8 * s), int(top_y))
    ring_b = c.rect(int(x - 3 * s), int(bot_y), int(x + 3 * s), int(bot_y) + 1)
    bars = np.zeros((AH, AW), bool)
    for bx in (-7.5, -3.8, 0, 3.8, 7.5):
        k = abs(bx) / 7.5
        x_top = x + bx * s
        x_bot = x + bx * s * 0.35
        bars |= c.line(x_top, top_y, x_top + (x_bot - x_top) * 0.45, y + 1) | \
            c.line(x_top + (x_bot - x_top) * 0.45, y + 1, x_bot, bot_y)
    hoop = c.rect(int(x - 7.5 * s), int(y - 1), int(x + 7.5 * s), int(y - 1))
    m = cap | ring_b | bars | hoop
    c.put(outline_mask(m) & ~heart_mask(c, x, y, s), K['ink'])
    c.put(m, KW['iron2'])
    c.put(m & (c.x < x - 1), KW['iron3'])
    c.put(cap & (c.y < top_y - 0.5), K['steel2'])
    for rx in (-7, 0, 7):
        c.put(c.pixel(x + rx * s, top_y - 0.5), KW['gold2'])
    if flash > 0:
        c.put(cap, K['white'], flash)
        g.add(dilate(cap), LW['steel'], 0.8 * flash)
    return cap


def voluntad_hierro_frame(f):
    t = f / FRAMES
    c, g = Canvas(AW, AH), Canvas(AW, AH)
    mountain_shrine(c, g, t)
    x, y = VH_HEART
    power_ring(c, g, x, 52, 18, 2.8, t, 0.5, col=KW['plate2'], light=LW['steel'])
    hy = y + math.sin(t * 2 * math.pi * 2) * 0.5
    passed = sum(1 for d in VH_DROPS if f >= d + 6)
    if f >= 21:
        passed = 0
    fill = 0.9 - 0.04 * passed
    life_heart(c, g, x, hy, 1.3, fill=fill)
    flash = max([max(0.0, 1 - (f - d - 4) / 2.0) for d in VH_DROPS if 0 <= f - d - 4 <= 2], default=0.0)
    top = iron_cage(c, g, x, hy, 1.3, flash=flash)
    cap_y = hy - 9 * 1.3 - 2
    for i, d in enumerate(VH_DROPS):
        land = drip(c, g, f, d, x + (i - 1) * 3 + 0.5, -4, cap_y, fall=3, s=1.8, swell=1, seed=1553 + i)
        if land == 0:
            star4(c, g, x + (i - 1) * 3, cap_y, 5, core=K['white'], col=K['steel4'], edge=K['steel2'],
                  light=LW['steel'], gain=1.2)
        if 0 <= land <= 3:                                              # it breaks on the iron and runs off the sides
            for side in (-1, 1):
                px = x + (i - 1) * 3 + side * (2 + land * 2.5)
                py = cap_y + land * land * 0.8
                blood_drop(c, g, px, py, 0.7, a=1 - land / 4)
        if 1 <= land <= 3:                                              # only one tiny drop gets through: 1 de vida
            ty = cap_y + 3 + (land - 1) * 3
            m = c.pixel(x + (i - 1) * 3, ty) | c.pixel(x + (i - 1) * 3, ty + 1)
            c.put(m, KW['crim5'])
            g.add(m, LW['crimson'], 0.8)
    vignette(c, 0.72)
    return c, g


ILLUSTRATIONS = {
    'baluarte': baluarte_frame,
    'furia_desatada': furia_desatada_frame,
    'gran_golpe': gran_golpe_frame,
    'gran_muralla': gran_muralla_frame,
    'ira_carmesi': ira_carmesi_frame,
    'piel_acero': piel_acero_frame,
    'bastion': bastion_frame,
    'berserker': berserker_frame,
    'doblar_escudo': doblar_escudo_frame,
    'ejecucion': ejecucion_frame,
    'escudo_impenetrable': escudo_impenetrable_frame,
    'golpe_vampirico': golpe_vampirico_frame,
    'muralla_infranqueable': muralla_infranqueable_frame,
    'voluntad_hierro': voluntad_hierro_frame,
}


def render(card_id):
    fn = ILLUSTRATIONS[card_id]
    base, glow = [], []
    for f in range(FRAMES):
        b, gl = fn(f)
        base.append(b)
        glow.append(gl)
    return base, glow
