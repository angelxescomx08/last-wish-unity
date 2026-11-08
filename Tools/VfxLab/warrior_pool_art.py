"""Animated pixel-art illustrations for the warrior's loot cards, the commons (88x64, 24 frames at 12 fps, seamless).

Same technique, palette and helpers as warrior_art.py and the rogue's loot cards (docs/art-style.md). No figure: the
subject is her gear (the runed longsword and the crimson heater shield of warrior_hero.html, and her other kit) and
what it touches; each card has its own scene so the hand never looks repeated. Her own mechanics get one picture
each, used on every card:
  - losing HP     a drop of her blood falls from above (her hand, out of the picture) and splashes;
  - Furia         that blood on her gear: crimson runes and a crimson aura (the game's Furia red);
  - Aguante       the block she already has is a faint blue ward; a gold pulse runs from the shield's gem;
  - Fuerza        crimson chevrons rising (as in Guardia Férrea);
  - healing       green crosses rising and her life, a crystal heart, filling up (the game's heal green);
  - block         the blue diamond ward of the game's block colour; drawing = a card flipping up.

  corte_sangriento  Corte Sangriento (13; pierdes 3 de vida): her blood lands on the guard, the runes turn crimson and
                    a crimson crescent cleaves an enemy sigil.
  embate_firme      Embate Firme (12; Aguante: +6): behind her raised shield the gold pulse feeds the sword, which
                    rams an iron-banded gate; gold "+" marks.
  escudo_sangre     Escudo de Sangre (14 de escudo; pierdes 3 de vida): three drops of blood on the planted shield,
                    crimson veins, then a big blue ward with a crimson rim.
  golpe_doble       Golpe Doble (5 + 5): two cuts, down and back up, dent an enemy breastplate on its stand.
  herida_abierta    Herida Abierta (pierdes 3 de vida; roba 2): two drops run down her blade into a basin and each
                    splash becomes a card.
  muro_acero        Muro de Acero (14 de escudo; Aguante: +1 Fuerza): iron pavises slam down into a wall that stops
                    the arrows; the gold pulse turns the sword's runes crimson.
  postura_firme     Postura Firme (8 de escudo; Furia: +5): on a stormy cliff the shield stamps into the rock;
                    with her blood on it, a second ward.
  tajo_brutal       Tajo Brutal (8; Furia: +5): with her blood on the blade, a crimson cut through an enemy banner;
                    the banner unrolls again.
  vendaje           Vendaje (te curas 5; Aguante: 4 más): a bandage winds round her cracked life-heart and seals it;
                    the shield's gold pulse heals more.
"""
import math

import numpy as np

from pixelkit import Canvas, hexc, dilate, erode, shift
from rogue_art import (AW, AH, FRAMES, K, L, ease_out, ease_in_out, frac, gradient_bg, vignette, star4, motes,
                       outline_mask, crescent)
from mage_art import twinkling_stars, sparks, ellipse_ring, flame
from warrior_art import (KW, LW, shake, falling_sparks, dust_puffs, rising_chevrons, sword_labels, paint_sword,
                         blade_point, heater_labels, paint_heater, WARM_SPARKS, CYAN_SPARKS)
from rogue_pool_art import KR, LR, KB, LB, GOLD_SPARKS, mana_drop, speed_lines, converge, diamond_ring, sigil
from rogue_pool_art_2 import paint_card
from rogue_pool_art_3 import plus_marks, rising_card, ward_rings, slash_line
from rogue_pool_art_5 import enemy_sigil, power_ring
from rogue_pool_art_6 import K6

KP = dict(K6)
KP.update({
    'heal0': hexc('#0e2a14'), 'heal1': hexc('#1f5a2a'), 'heal2': hexc('#3fae4a'), 'heal3': hexc('#78ec78'),
    'heal4': hexc('#d4ffd0'),
    'linen0': hexc('#3a3040'), 'linen1': hexc('#7a6c70'), 'linen2': hexc('#c2b4a6'), 'linen3': hexc('#ece2d0'),
    'glass_r0': hexc('#16060e'), 'glass_r1': hexc('#2a0c18'),
    'canvas0': hexc('#0c1418'), 'canvas1': hexc('#14222a'), 'canvas2': hexc('#1e323c'), 'canvas3': hexc('#2c4652'),
    'oak0': hexc('#1a0f0c'), 'oak1': hexc('#301c16'), 'oak2': hexc('#4a2c20'), 'oak3': hexc('#6a4430'),
    'enemy0': hexc('#120a1c'), 'enemy1': hexc('#221436'), 'enemy2': hexc('#38225a'), 'enemy3': hexc('#553484'),
})
LP = dict(LR)
LP.update({'heal': np.array([0.35, 1.0, 0.4, 1]), 'blood': LW['crimson']})
BLOOD_SPARKS = (KW['crim5'], KW['crim4'], KW['crim2'])
HEAL_SPARKS = (KP['heal4'], KP['heal3'], KP['heal1'])


# ---------------------------------------------------------------------------------------- shared vocabulary
def sage(f, born):
    """Signed age on the loop: (f - born) wrapped into [-12, 12)."""
    return (f - born + 12) % FRAMES - 12


def foe_sigil(c, g, x, y, t, **kw):
    """An enemy's crimson sigil (rogue_pool_art_5.enemy_sigil) turning a whole number of times per loop."""
    enemy_sigil(c, g, x, y, t * 4.0 / 3.0, **kw)


def blood_drop(c, g, x, y, s=1.0, a=1.0, stretch=1.0, gain=1.0):
    """One drop of her blood: round bottom centred at (x, y), point up; stretch > 1 when falling fast."""
    r = 1.7 * s
    dx, dy = c.x - x, (c.y - y)
    body = np.hypot(dx, dy) <= r
    up = (dy < 0) & (dy > -r * 2.4 * stretch) & (np.abs(dx) <= r * np.clip(1 + dy / (r * 2.4 * stretch), 0, 1) ** 0.8)
    m = body | up
    if not m.any():
        return m
    c.put(outline_mask(m), K['ink'], a)
    c.put(m, KW['crim2'], a)
    c.put(m & (dx + dy * 0.5 < 0.2), KW['crim3'], a)
    c.put(m & (dx + dy * 0.5 < -0.9), KW['crim4'], a)
    c.put(c.pixel(x - r * 0.45, y - r * 0.4) & m, KW['crim5'], a)
    g.add(m, LW['crimson'], 0.35 * a * gain)
    return m


def drip(c, g, f, born, x, y0, y1, fall=3, s=1.0, swell=2, seed=0, gain=1.0):
    """A drop forms at (x, y0) for `swell` frames, falls to y1 in `fall` frames and splashes on frame born+swell+fall.
    Returns the age relative to the splash (0 = the splash frame)."""
    a = f - born
    land = swell + fall
    if 0 <= a < swell:
        blood_drop(c, g, x, y0 + a * 0.5, s * (0.6 + 0.4 * (a + 1) / swell), gain=gain)
    elif swell <= a < land:
        k = (a - swell + 1) / fall
        y = y0 + (y1 - y0) * k * k
        blood_drop(c, g, x, y, s, stretch=1.0 + 0.8 * k, gain=gain)
        for j in range(1, 3):                                      # streak behind the fast drop
            m = c.pixel(x, y - 2.5 * s - j * 1.2 * k * 2)
            c.put(m, KW['crim3'], 0.6 / j)
    splash(c, g, f, born + land, x, y1, seed, gain=gain)
    return a - land


def splash(c, g, f, born, x, y, seed=0, count=7, gain=1.0):
    """Crimson droplets thrown up where a drop lands, plus a flat ring."""
    age = f - born
    if not 0 <= age <= 6:
        return
    if age <= 2:
        ring = ellipse_ring(c, x, y, 1.5 + age * 1.6, 0.6 + age * 0.4, 0.5)
        c.put(ring, KW['crim4'] if age == 0 else KW['crim3'], 1 - age / 3)
        g.add(ring, LW['crimson'], 0.6 * (1 - age / 3) * gain)
    sparks(c, g, f, born, x, y - 0.5, seed + 401, count, (0.5, 1.2), 5, BLOOD_SPARKS, LW['crimson'], gravity=0.22,
           spread=(0.35, math.pi - 0.35), gain=0.6 * gain)


def heart_mask(c, x, y, s=1.0):
    """Heart shape centred at (x, y): two lobes and a point, about 13 x 12 px at s = 1."""
    dx, dy = (c.x - x) / s, (c.y - y) / s
    lobes = (np.hypot(dx + 3.2, dy + 1.6) <= 3.6) | (np.hypot(dx - 3.2, dy + 1.6) <= 3.6)
    point = (dy >= -1.6) & (np.abs(dx) <= (6.4 - (dy + 1.6) * 1.0)) & (dy <= 5.6)
    return lobes | point


def life_heart(c, g, x, y, s=1.0, fill=1.0, crack=0.0, green=0.0, flash=0.0, a=1.0, beat=0.0):
    """Her life: a crystal heart in a thin gold setting. fill 0..1 = how much of it is red (the rest is dark glass);
    crack 0..1 shows the wound; green 0..1 tints the light while healing; beat swells it by a pixel."""
    s = s * (1 + 0.08 * beat)
    m = heart_mask(c, x, y, s)
    inner = erode(m)
    core = erode(inner)
    top, bot = y - 5.2 * s, y + 5.6 * s
    level = bot - (bot - top) * np.clip(fill, 0, 1)
    full = core & (c.y >= level)
    empty = core & ~full
    c.put(outline_mask(m), K['ink'], a)
    c.put(m & ~inner, KW['gold1'], a)
    c.put(m & ~inner & (c.x + c.y < x + y - 2), KW['gold2'], a)
    c.put(m & ~inner & (c.x + c.y < x + y - 7), KW['gold3'], a)
    c.put(inner & ~core, KW['crim1'], a)
    c.put(empty, KP['glass_r1'], a)
    c.put(empty & (c.x < x - 1) & (c.y < y - 1), KP['glass_r0'], a)
    dx, dy = c.x - x, c.y - y
    c.put(full, KW['crim2'], a)
    c.put(full & (dx + dy * 0.8 < 1.5 * s), KW['crim3'], a)
    c.put(full & (dx + dy * 0.8 < -1.8 * s), KW['crim4'], a)
    c.put(full & (np.abs(c.y - level) < 0.8), KW['crim5'], a)                 # the bright surface line
    glint = core & (np.hypot(dx + 3.2 * s, dy + 2.2 * s) <= 1.0 * s)
    c.put(glint, K['white'], a)
    g.add(full, LW['crimson'], 0.35 * a)
    g.add(glint, L['white'], 0.4 * a)
    if crack > 0:
        cr = (c.line(x + 0.5, y - 3.5 * s, x - 1.0, y - 0.5) | c.line(x - 1.0, y - 0.5, x + 1.5, y + 1.5 * s) |
              c.line(x + 1.5, y + 1.5 * s, x + 0.5, y + 3.5 * s)) & inner
        c.put(cr, K['ink'], crack * a)
        c.put(cr & shift(cr, -1, 0) & (c.y < y), KW['crim5'], 0.6 * crack * a)
    if green > 0:
        c.put(full, KP['heal2'], 0.7 * green * a)
        c.put(full & (dx + dy * 0.8 < -1.8 * s), KP['heal3'], green * a)
        c.put(m & ~inner, KP['heal3'], 0.6 * green * a)
        g.add(m, LP['heal'], 0.7 * green * a)
    if flash > 0:
        c.put(core, K['white'], flash * a)
        g.add(m, L['white'], 0.8 * flash * a)
    return m


def heal_crosses(c, g, f, born, x, y, count=3, life=8, spread=8, seed=0, size=1, gain=1.0):
    """Green "+" crosses rising and fading: healing."""
    age = f - born
    if not 0 <= age <= life:
        return
    rng = np.random.default_rng(seed)
    for i in range(count):
        d = rng.uniform(0, 2.5)
        a = age - d
        if a < 0:
            continue
        px = x + rng.uniform(-spread, spread) + math.sin(a * 0.9 + i) * 0.8
        py = y - a * rng.uniform(1.3, 2.0)
        k = 1 - a / (life + 1)
        if k <= 0:
            continue
        arm = size + (1 if i % 3 == 0 else 0)
        m = c.pixel(px, py)
        for j in range(1, arm + 1):
            m |= c.pixel(px - j, py) | c.pixel(px + j, py) | c.pixel(px, py - j) | c.pixel(px, py + j)
        c.put(outline_mask(m), KP['heal0'], 0.7 * k)
        c.put(m, KP['heal3'] if a < life * 0.6 else KP['heal2'], min(1.0, k * 1.5))
        c.put(c.pixel(px, py), KP['heal4'], min(1.0, k * 1.5))
        g.add(m, LP['heal'], 0.8 * k * gain)


def blood_aura(c, g, m, k, t, width=2):
    """Furia: a flickering crimson outline round a silhouette, a few pixels dripping off its bottom."""
    if k <= 0:
        return
    ring = m.copy()
    for _ in range(width):
        ring = dilate(ring)
    ring &= ~m
    wav = (np.floor(c.x * 0.7 + c.y * 0.5 + t * 24) % 3) != 0
    c.put(ring & wav, KW['crim3'], 0.55 * k)
    c.put(outline_mask(m) & ~m & wav, KW['crim4'], 0.8 * k)
    g.add(ring, LW['crimson'], 0.55 * k)


def gold_pulse(c, g, x0, y0, x1, y1, p, trail=0.35):
    """Aguante: a gold bead running from (x0, y0) to (x1, y1), p 0..1, with a fading trail."""
    if not 0 <= p <= 1:
        return
    n = max(2, int(math.hypot(x1 - x0, y1 - y0)))
    for i in range(n + 1):
        u = i / n
        if u > p or u < p - trail:
            continue
        k = 1 - (p - u) / trail
        m = c.pixel(x0 + (x1 - x0) * u, y0 + (y1 - y0) * u)
        c.put(m, KW['gold3'] if k > 0.6 else KW['gold2'], min(1.0, 0.4 + k))
        g.add(m, LW['gold'], 0.9 * k)
    hx, hy = x0 + (x1 - x0) * p, y0 + (y1 - y0) * p
    head = c.disc(hx, hy, 1.3)
    c.put(head, KW['gold3'])
    c.put(c.pixel(hx, hy), K['white'])
    g.add(dilate(head), LW['gold'], 0.9)


def faint_ward(c, g, m, t, a=0.5):
    """The block she already has (the Aguante condition): a faint blue outline that breathes round the shield."""
    ring = dilate(dilate(m)) & ~dilate(m)
    br = 0.75 + 0.25 * math.sin(t * 2 * math.pi * 2)
    c.put(ring, KB['b1'], a * br)
    c.put(ring & (((c.x + c.y + t * 24) % 6) < 1.5), KB['b2'], a * br)
    g.add(ring, LB, 0.3 * a * br)


def sword(c, g, gx, gy, ang, t, glint=None, rune_gain=1.0, fury=0.0, blade=34.0, s=1.0, cut=None, ghost=None):
    """Her longsword placed by its crossguard. cut = local u beyond which the blade is hidden (in a wall or a gate)."""
    lab, u, v = sword_labels(c, round(gx * 2) / 2, round(gy * 2) / 2, ang, s=s, blade=blade)
    if cut is not None:
        lab[(u > cut) & (lab > 0)] = 0
    paint_sword(c, g, lab, u, t, glint, rune_gain=rune_gain, ghost=ghost, fury=fury, blade=blade)
    return lab


def shield(c, g, cx, cy, W=11.5, H=30.0, sx=1.0, sy=1.0, gem_gain=1.0, glint=None, flash=0.0):
    lab = heater_labels(c, cx, cy, W=W, H=H, sx=sx, sy=sy)
    paint_heater(c, g, lab, cx, cy, gem_gain=gem_gain, glint=glint, flash=flash)
    return lab


def shield_gem(cx, cy, H=30.0):
    """Where the gem of a heater shield centred at (cx, cy) sits."""
    return cx, cy - H * 0.44 + 8.5


def contact_shadow(c, x, y, rx):
    sh = c.disc(x, y, rx) & (np.abs(c.y - y) <= 1.2)
    c.put(sh, K['ink'], 0.55)
    c.put(c.disc(x, y, rx * 0.6) & (np.abs(c.y - y) <= 0.8), K['ink'], 0.85)


def floor_slabs(c, y0, tone=None, step=13):
    """Flagstone floor from y0 down: top edge lit, two courses of joints."""
    tone = tone or (KW['stone0'], KW['stone1'], KW['stone2'], KW['stone3'])
    c.put(c.rect(0, y0, AW - 1, AH - 1), tone[1])
    c.put(c.rect(0, y0, AW - 1, y0), tone[3])
    c.put(c.rect(0, y0 + 1, AW - 1, y0 + 1), tone[2], 0.6)
    for row, y in enumerate((y0 + 4, y0 + 8)):
        if y >= AH:
            break
        c.put(c.rect(0, y, AW - 1, y), tone[0])
        for x in range((row * 6) % step, AW, step):
            c.put(c.rect(x, y - 3, x, y - 1), tone[0])


def candle(c, g, x, y, t, ph, h=5):
    """A wax candle standing at (x, y) with a small flame."""
    wax = c.rect(int(x) - 1, int(y) - h, int(x) + 1, int(y))
    c.put(outline_mask(wax), K['ink'])
    c.put(wax, KP['linen2'])
    c.put(wax & (c.x > x + 0.5), KP['linen1'])
    flame(c, g, x + 0.5, y - h - 1.5, 4.5, 1.6, t, ph, sway_amp=0.6, gain=0.8, side=False, flicks=False)
    g.add(c.disc(x + 0.5, y - h - 3, 5) & ~c.disc(x + 0.5, y - h - 3, 3), L['warm'], 0.08)


# ---------------------------------------------------------------------------------------- Corte Sangriento
CS_PIVOT = (24.0, 46.0)
CS_IDLE = math.radians(-58)
CS_WIND = math.radians(-118)
CS_END = math.radians(18)
CS_SIGIL = (67.0, 30.0)


def crimson_field(c, g, t):
    """A battlefield at night under a low blood-red moon: broken spears and a cairn, mist in the dips."""
    gradient_bg(c, K['vio1'], K['vio2'], KP['blood1'], 0.62)
    mx, my = 18.0, 14.0
    c.put(c.disc(mx, my, 14), KP['blood2'], 0.25)
    c.put(c.disc(mx, my, 10), KW['crim1'], 0.35)
    moon = c.disc(mx, my, 6.5)
    c.put(moon, KW['crim2'])
    c.put(moon & (c.x + c.y < mx + my - 1), KW['crim3'])
    c.put(moon & c.disc(mx - 2.5, my - 2, 2.0), KW['crim3'], 0.6)
    c.put(moon & c.disc(mx + 3, my + 2.5, 1.6), KW['crim1'])
    g.add(moon, LW['crimson'], 0.12)
    twinkling_stars(c, g, t, 503, 14, (2, 2, 86, 30), avoid=(mx, my, 13), light=L['violet'])
    hills = c.poly([(0, 44), (14, 40), (30, 43), (46, 39), (60, 42), (74, 38), (88, 41), (88, 64), (0, 64)])
    c.put(hills, K['vio0'])
    c.put(hills & ~shift(hills, 0, 1), KP['blood2'], 0.7)
    for x, h, lean in ((8, 12, -2), (15, 9, 1), (78, 13, 2), (84, 8, -1), (52, 7, 1)):
        sp = c.line(x, 47, x + lean, 47 - h)
        c.put(sp, K['roof'])
        c.put(c.pixel(x + lean, 47 - h), K['steel0'])
    cairn = c.poly([(36, 47), (38, 43), (41, 41), (44, 43), (45, 47)])
    c.put(cairn, K['roof2'])
    ground = c.rect(0, 50, AW - 1, AH - 1)
    c.put(ground, KP['blood0'])
    c.put(c.rect(0, 50, AW - 1, 50), KP['blood2'])
    for i, y in enumerate((48, 52)):                              # drifting mist bands
        off = (t * (1 + i) * AW) % AW
        band = (np.abs(c.y - y) <= 1) & (((c.x + off) % 29) < 17)
        c.put(band, K['vio3'], 0.25)
    for x in (6, 23, 58, 71):
        c.put(c.rect(x, 54 + (x % 3), x + 2, 54 + (x % 3)), KP['blood2'])


def cs_angle(f):
    """Blade angle: hover (0-8), wind-up (9-11), the cut (12-13), follow-through (14-17), back (18-23)."""
    if f <= 8:
        return CS_IDLE
    if f <= 11:
        return CS_IDLE + (CS_WIND - CS_IDLE) * ease_in_out((f - 8) / 3.0)
    if f == 12:
        return math.radians(-30)
    if f <= 17:
        return CS_END + math.radians({13: 0, 14: 5, 15: 3, 16: 1, 17: 0}[f])
    return CS_END + (CS_IDLE - CS_END) * ease_in_out((f - 17) / 6.0)


def corte_sangriento_frame(f):
    t = f / FRAMES
    c, g = Canvas(AW, AH), Canvas(AW, AH)
    crimson_field(c, g, t)
    hit = 13
    age = f - hit
    px, py = CS_PIVOT
    py += math.sin(t * 2 * math.pi * 2) * 0.8 if (f <= 8 or f >= 21) else 0.0
    ang = cs_angle(f)
    R = 5 + 34
    sx, sy = CS_SIGIL
    foe_sigil(c, g, sx, sy, t, hit=age if age >= 0 else -99, r=7.0)
    # the price: one drop of her blood falls from above onto the crossguard gem
    gx, gy = px + math.cos(ang) * 5, py + math.sin(ang) * 5
    fury = 1.0 if 7 <= f <= 20 else 0.0
    climb = {6: 0.2, 7: 0.45, 8: 0.75}.get(f, 1.0 if 9 <= f <= 20 else 0.0)

    cpal = (KW['crim1'], KW['crim3'], KW['crim5'], K['white'])
    if f == 12:
        crescent(c, g, px, py, R + 0.5, -ang, min(1.8, ang - CS_WIND), -1, 10.0, cpal, LW['crimson'], gain=1.5)
    elif 13 <= f <= 18:
        q = (f - 13) / 6.0
        crescent(c, g, px, py, R + 0.5, -CS_END, 2.1 * (1 - 0.7 * q), -1, 9.5 * (1 - q * 0.75), cpal, LW['crimson'],
                 a=1.0 - q * 0.85, dash=f >= 15, gain=1.5 * (1 - q))
    lab = sword(c, g, gx, gy, ang, t * 2 + (1.5 if 9 <= f <= 13 else 0),
                glint=(-4 + (f - 19) * 9.0 if 19 <= f <= 23 else None),
                rune_gain=1.0 + 1.2 * climb, fury=fury)
    land = drip(c, g, f, 2, gx + 0.5, -3, gy - 1, fall=3, s=1.5, swell=1, seed=3)
    if land == 0:
        star4(c, g, gx, gy - 1, 5, core=K['white'], col=KW['crim5'], edge=KW['crim4'], light=LW['crimson'], gain=1.3)
    if 6 <= f <= 8:                                              # the blood climbs the fuller
        u_top = climb * 34
        ux, uy = math.cos(ang), math.sin(ang)
        hx_, hy_ = gx + ux * u_top, gy + uy * u_top
        star4(c, g, hx_, hy_, 2, core=KW['crim5'], col=KW['crim4'], edge=KW['crim3'], light=LW['crimson'], gain=0.9)
    if 8 <= f <= 11:
        blood_aura(c, g, lab > 0, {8: 0.5, 9: 0.8, 10: 1.0, 11: 1.0}[f], t, width=1)
    tx, ty = blade_point(gx, gy, ang)
    if f == 11:
        star4(c, g, tx, ty, 4, core=K['white'], col=KW['crim5'], edge=KW['crim4'], light=LW['crimson'], gain=1.2)

    # the hit: flash on the sigil, crimson star, blood flung off the edge along the swing, sparks
    if f == hit:
        c.put(c.disc(sx, sy, 7.5), KW['crim4'], 0.6)
        c.put(c.disc(sx, sy, 4.5), KW['crim5'])
        c.put(c.disc(sx, sy, 2.2), K['white'])
        g.add(c.disc(sx, sy, 8), LW['crimson'], 1.3)
    if 0 <= age <= 3:
        star4(c, g, sx, sy, {0: 12, 1: 8, 2: 5, 3: 2}[age], core=K['white'], col=KW['crim5'], edge=KW['crim4'],
              light=LW['crimson'], gain=1.4)
    sparks(c, g, f, hit, sx, sy, 517, 12, (1.2, 2.8), 7, BLOOD_SPARKS, LW['crimson'], gravity=0.2,
           spread=(-0.9, 2.2))
    falling_sparks(c, g, f, hit, sx - 2, sy + 2, 521, 8, (1.0, 2.2), 9, WARM_SPARKS, L['warm'], 51, (-0.4, 2.6))
    sparks(c, g, f, hit - 1, 40, 30, 531, 8, (0.8, 1.6), 6, BLOOD_SPARKS, LW['crimson'], gravity=0.3,
           spread=(-0.6, 0.8), gain=0.6)                         # blood flung off the edge

    motes(c, g, t, 541, 8, (4, 8, 84, 48), KW['crim4'], K['violet3'], LW['crimson'], gain=0.45)
    vignette(c, 0.72)
    if f == hit:
        shake(c, g, 1, 0)
    elif f == hit + 1:
        shake(c, g, -1, 0)
    return c, g


# ---------------------------------------------------------------------------------------- Embate Firme
EF_SHIELD = (22.0, 41.0)
EF_GUARD = (20.0, 19.0)
EF_DOOR = 64


def gate_courtyard(c, g, t):
    """A castle courtyard: a wall of cold stone, a torch, and a heavy oak gate with iron bands on the right."""
    gradient_bg(c, K['teal0'], K['teal2'], K['teal1'], 0.6)
    wall = c.rect(0, 0, AW - 1, 51)
    c.put(wall, KW['stone1'])
    for row, y in enumerate(range(3, 51, 6)):
        c.put(c.rect(0, y, AW - 1, y), KW['stone0'])
        for x in range(0 if row % 2 else 7, AW, 14):
            c.put(c.rect(x, y + 1, x, y + 5), KW['stone0'])
            c.put(c.rect(x + 1, y + 1, x + 4, y + 1), KW['stone2'], 0.6)
    # torch on the wall between shield and gate (warm accent)
    tx, ty = 44.0, 33.0
    lit = np.clip(1 - np.hypot(c.x - tx, (c.y - ty) * 1.2) / 22, 0, 1)
    c.put(wall & (lit > 0.45), KW['stone2'])
    c.put(wall & (lit > 0.7), KW['stone3'])
    c.put(c.rect(43, 34, 45, 39), KW['iron1'])
    c.put(c.rect(42, 34, 46, 34), KW['iron2'])
    flame(c, g, tx + 0.5, ty, 6, 2.2, t, 0.3, gain=0.9, side=False)
    # the gate: oak planks, iron bands with rivets, an enemy crest
    gate = c.rect(EF_DOOR, 6, AW - 1, 51)
    c.put(outline_mask(gate), K['ink'])
    c.put(gate, KP['oak1'])
    for x in range(EF_DOOR, AW, 5):
        c.put(c.rect(x, 7, x, 51), KP['oak0'])
        c.put(c.rect(x + 1, 7, x + 1, 51), KP['oak2'], 0.7)
    for y in (13, 33, 46):
        band = c.rect(EF_DOOR, y, AW - 1, y + 2)
        c.put(band, KW['iron1'])
        c.put(c.rect(EF_DOOR, y, AW - 1, y), KW['iron3'])
        for x in range(EF_DOOR + 2, AW, 6):
            c.put(c.pixel(x, y + 1), KW['iron3'])
    arch = c.rect(EF_DOOR - 3, 4, EF_DOOR - 1, 51)
    c.put(arch, KW['stone2'])
    c.put(c.rect(EF_DOOR - 3, 4, EF_DOOR - 3, 51), KW['stone3'])
    floor_slabs(c, 52)


def embate_firme_frame(f):
    t = f / FRAMES
    c, g = Canvas(AW, AH), Canvas(AW, AH)
    gate_courtyard(c, g, t)
    hit = 12
    age = f - hit
    jolt = {12: 2, 13: 1, 14: 1}.get(f, 0)
    # the crest on the gate (an enemy sigil carved in the wood)
    foe_sigil(c, g, 75 + jolt, 23, t, hit=age if age >= 0 else -99, r=5.5, a=0.7)
    # the sword: hover, pull back, ram, held in the wood, pulled out
    dx = {9: -2, 10: -4, 11: 6, 12: 9, 13: 9, 14: 9, 15: 8, 16: 5, 17: 3, 18: 1}.get(f, 0)
    bob = math.sin(t * 2 * math.pi * 2) * 0.7 if (f <= 8 or f >= 20) else 0.0
    gx, gy = EF_GUARD[0] + dx, EF_GUARD[1] + bob
    tip_x = gx + 34
    cut = (EF_DOOR + 1 - gx) if tip_x > EF_DOOR + 1 else None
    if f == 11:
        speed_lines(c, g, gx + 6, gy, 1, 0, 1.0, 551, count=6, spread=4, length=(6, 12), back=4)
    pulse_p = {5: 0.0, 6: 0.33, 7: 0.66, 8: 1.0}.get(f, -1)
    aguante = 8 <= f <= 15
    sh_x, sh_y = EF_SHIELD
    shx = sh_x + {11: 1, 12: 2, 13: 2, 14: 1}.get(f, 0)
    lab = sword(c, g, gx, gy, 0.0, t * 2, glint=(-4 + (f - 18) * 9.0 if 18 <= f <= 22 else None),
                rune_gain=1.0 + (1.4 if aguante else 0), cut=cut)
    if aguante:                                                   # Aguante: the runes burn gold
        fl = (lab == 5) & ((np.floor(c.x) % 3) != 2)
        c.put(fl, KW['gold3'], 0.85)
        g.add(fl, LW['gold'], 0.9)
    contact_shadow(c, shx, 56.5, 9)
    slab = shield(c, g, shx, sh_y, gem_gain=1.0 + (1.6 if 4 <= f <= 8 else 0))
    faint_ward(c, g, slab > 0, t, a=0.55)
    gx0, gy0 = shield_gem(shx, sh_y)
    if f == 4:
        star4(c, g, gx0, gy0, 4, core=K['white'], col=KW['gold3'], edge=KW['gold2'], light=LW['gold'], gain=1.2)
    gold_pulse(c, g, gx0, gy0 - 2, gx + 1, gy, pulse_p)
    if f == 8:
        star4(c, g, gx + 1, gy, 5, core=K['white'], col=KW['gold3'], edge=KW['gold2'], light=LW['gold'], gain=1.3)

    # impact on the gate: gold star, splinters, the gate shakes, "+" marks (the extra damage)
    ix, iy = EF_DOOR + 1, gy
    if f == hit:
        c.put(c.disc(ix, iy, 6), KW['gold2'], 0.6)
        c.put(c.disc(ix, iy, 3.5), KW['gold3'])
        c.put(c.disc(ix, iy, 1.8), K['white'])
        g.add(c.disc(ix, iy, 7), LW['gold'], 1.3)
    if 0 <= age <= 3:
        star4(c, g, ix, iy, {0: 12, 1: 8, 2: 5, 3: 2}[age], core=K['white'], col=KW['gold3'], edge=KW['gold2'],
              light=LW['gold'], gain=1.4)
    if 0 <= age <= 8:
        rng = np.random.default_rng(557)
        for i in range(8):                                        # splinters
            a0 = rng.uniform(1.6, 4.6)
            spd = rng.uniform(1.0, 2.2)
            x = ix + math.cos(a0) * spd * age
            y = iy - math.sin(a0) * spd * age + 0.25 * age * age
            if y < 52:
                m = c.rect(int(x), int(y), int(x) + (i % 2), int(y))
                c.put(m, KP['oak3'] if i % 2 else KP['oak2'], 1 - age / 9)
    sparks(c, g, f, hit, ix, iy, 563, 9, (1.0, 2.4), 6, GOLD_SPARKS, LW['gold'], gravity=0.18, spread=(1.7, 4.6))
    for j in range(2):
        plus_marks(c, g, ix - 6 + j * 4, iy - 6, age - j * 2, count=2, col=KW['gold3'], light=LW['gold'])
    dust_puffs(c, ix + 2, 51, f, hit + 1, 3, 1.5, rise=0.4, life=6, seed=569, big=0.8)

    motes(c, g, t, 571, 7, (4, 6, 60, 50), KW['gold3'], K['teal3'], LW['gold'], gain=0.4)
    vignette(c, 0.72)
    if f == hit:
        shake(c, g, 1, 0)
    return c, g


# ---------------------------------------------------------------------------------------- Escudo de Sangre
ES_C = (44.0, 34.0)
ES_H = 28.0
ES_FLOOR = 54


def blood_crypt(c, g, t):
    """A crypt chapel: a niche of dark stone with an altar slab, two candles and an old crimson cloth."""
    gradient_bg(c, K['vio0'], K['vio1'], K['teal0'], 0.6)
    back = c.rect(0, 0, AW - 1, ES_FLOOR - 1)
    c.put(back, KP['temple0'])
    niche = c.rect(24, 6, 63, ES_FLOOR - 1) | (c.disc(43.5, 7, 20) & (c.y < 7))
    c.put(niche, KP['temple1'])
    c.put(outline_mask(niche) & ~niche, KP['temple2'])
    for row, y in enumerate(range(4, ES_FLOOR, 7)):
        c.put(c.rect(0, y, 22, y) | c.rect(65, y, AW - 1, y), K['ink'], 0.7)
        off = 0 if row % 2 else 5
        for x in list(range(off, 22, 10)) + list(range(65 + off, AW, 10)):
            c.put(c.rect(x, y + 1, x, y + 6), K['ink'], 0.7)
    lit = np.clip(1 - np.hypot(c.x - 43.5, c.y - 22) / 26, 0, 1)
    c.put(niche & (lit > 0.35), KP['temple2'], 0.6)
    # a small round window high in the niche, moonlight through violet glass
    win = c.disc(43.5, 9.5, 4.5)
    c.put(dilate(win), K['ink'])
    c.put(win, K['vio3'])
    c.put(win & (c.x + c.y < 52), K['moon0'])
    c.put(win & ((np.abs(c.x - 43.5) < 0.6) | (np.abs(c.y - 9.5) < 0.6)), K['ink'])
    g.add(win, L['violet'], 0.2)
    altar = c.rect(26, 47, 61, ES_FLOOR - 1)
    c.put(outline_mask(altar), K['ink'])
    c.put(altar, KP['temple2'])
    c.put(c.rect(26, 47, 61, 47), KP['temple3'])
    floor_slabs(c, ES_FLOOR, tone=(KP['temple0'], KP['temple1'], KP['temple2'], KP['temple3']))
    for i, x in enumerate((17.0, 70.0)):
        candle(c, g, x, ES_FLOOR - 1, t, i * 0.37, h=6 + i)


def veins(c, cx, cy, k, seed=7):
    """Crimson veins spreading over the shield's field from its gem, k 0..1."""
    rng = np.random.default_rng(seed)
    m = np.zeros((AH, AW), bool)
    for j, a0 in enumerate((-2.6, -1.9, -1.2, -0.5, 0.3, 1.0, 1.7, 2.4)):
        x, y = cx, cy
        ln = (5 + 7 * k) * rng.uniform(0.7, 1.1)
        for s_ in range(4):
            if (s_ + 1) / 4 > k + 0.15:
                break
            nx = x + math.cos(a0) * ln / 4 + rng.uniform(-0.8, 0.8)
            ny = y + math.sin(a0) * ln / 4 * 1.3 + rng.uniform(-0.6, 0.6)
            m |= c.line(x, y, nx, ny)
            x, y = nx, ny
    return m


def escudo_sangre_frame(f):
    t = f / FRAMES
    c, g = Canvas(AW, AH), Canvas(AW, AH)
    blood_crypt(c, g, t)
    lock = 12
    age = f - lock
    cx, cy = ES_C
    cy += math.sin(t * 2 * math.pi) * 0.6
    gx, gy = shield_gem(cx, cy, ES_H)
    contact_shadow(c, cx, ES_FLOOR - 7.5, 9)
    k_vein = {6: 0.25, 7: 0.4, 8: 0.55, 9: 0.75, 10: 0.9, 11: 1.0}.get(f, 1.0 if 12 <= f <= 17 else
                                                                       {18: 0.7, 19: 0.45, 20: 0.2}.get(f, 0.0))
    flash = {12: 0.55, 13: 0.2}.get(f, 0.0)
    slab = shield(c, g, cx, cy, W=10.5, H=ES_H, gem_gain=1.0 + (1.5 if 5 <= f <= 13 else 0), flash=flash,
                  glint=(-24 + (f - 18) * 8 if 18 <= f <= 22 else None))
    field = (slab == 3)
    if k_vein > 0:
        vm = veins(c, gx, gy, k_vein) & field
        c.put(vm, KW['crim5'] if 10 <= f <= 13 else KW['crim4'])
        c.put(dilate(vm) & field & ~vm, KW['crim1'], 0.6)
        g.add(vm, LW['crimson'], 0.7 * k_vein)
        if f >= 10 and f <= 13:
            c.put(field & ~vm, KW['crim3'], 0.5)
            g.add(field, LW['crimson'], 0.25)
    # three drops of her blood on the gem
    for j, born in enumerate((0, 3, 6)):
        drip(c, g, f, born, gx + (j - 1) * 0.0 + 0.5, -2, gy - 1, fall=3, s=1.0, swell=1, seed=601 + j)
    # the ward: a big blue diamond with a crimson inner rim (14 de escudo), held then fading
    if 0 <= age <= 8:
        ward_rings(c, g, cx, cy + 2, age, size=19, squash=1.05, life=9)
        if age >= 1:
            k = 1 - age / 9
            inner = diamond_ring(c, cx, cy + 2, 12 + min(age, 3), 0.5, squash=1.05)
            c.put(inner, KW['crim4'], k)
            g.add(inner, LW['crimson'], 0.5 * k)
    if f == lock:
        star4(c, g, cx, cy - ES_H * 0.44 - 2, 8, core=K['white'], col=KB['b3'], edge=KB['b2'], light=LB, gain=1.3)
    sparks(c, g, f, lock, cx, cy, 611, 10, (0.6, 1.6), 7, (KB['b3'], KB['b2'], KB['b1']), LB, gravity=-0.05,
           gain=0.8)
    motes(c, g, t, 617, 7, (14, 8, 74, 48), KW['crim4'], KB['b2'], LW['crimson'], gain=0.45)
    vignette(c, 0.74)
    return c, g


# ---------------------------------------------------------------------------------------- Golpe Doble
GD_PIVOT = (27.0, 41.0)
GD_PLATE = (66.0, 31.0)


def armory(c, g, t):
    """An armoury at night: a weapon rack on the wall, a tall window with moonlight, flagstones."""
    gradient_bg(c, K['teal0'], K['teal2'], K['teal1'], 0.6)
    wall = c.rect(0, 0, AW - 1, 51)
    c.put(wall, KW['stone1'])
    for row, y in enumerate(range(5, 51, 7)):
        c.put(c.rect(0, y, AW - 1, y), KW['stone0'], 0.8)
        for x in range(0 if row % 2 else 8, AW, 16):
            c.put(c.rect(x, y + 1, x, y + 6), KW['stone0'], 0.8)
    win = c.rect(40, 6, 49, 26) | (c.disc(44.5, 7, 4.6) & (c.y < 7))
    c.put(dilate(win), K['ink'])
    c.put(win, K['teal3'])
    c.put(win & (c.x < 44), K['moon0'], 0.5)
    c.put(win & ((np.abs(c.x - 44.5) < 0.6) | (np.abs(c.y - 16) < 0.6)), K['ink'])
    g.add(win, L['blue'], 0.15)
    beam = (np.abs((c.x - 44) - (c.y - 26) * 0.5) < 4 + (c.y - 26) * 0.1) & (c.y > 26)
    c.put(beam, K['teal3'], 0.25)
    # weapon rack: spears and an axe in silhouette
    rack = c.rect(3, 22, 19, 23) | c.rect(3, 38, 19, 39)
    for x in (5, 9, 13, 17):
        rack |= c.rect(x, 8 + (x % 3), x, 46)
    rack |= c.poly([(13, 10), (17, 8), (18, 13), (14, 13)])
    c.put(rack, K['roof2'])
    c.put(c.pixel(5, 8) | c.pixel(9, 9), K['steel0'])
    floor_slabs(c, 52)
    c.put(beam & (c.y >= 52), K['teal3'], 0.3)


def plate_stand(c, g, x, y, t, dents, jolt=0.0):
    """An enemy breastplate on a wooden T-stand (its centre at (x, y)). dents = [(x, y, angle, age)] of fresh cuts."""
    x = x + jolt
    post = c.rect(int(x) - 1, int(y) + 6, int(x) + 1, 52)
    foot = c.rect(int(x) - 7, 51, int(x) + 7, 52)
    bar = c.rect(int(x) - 11, int(y) - 9, int(x) + 11, int(y) - 8)
    wood = post | foot | bar
    c.put(outline_mask(wood), K['ink'])
    c.put(wood, KP['oak1'])
    c.put(wood & ~shift(wood, 0, 1), KP['oak2'])
    dx, dy = c.x - x, c.y - y
    body = (np.abs(dx) <= 10 - np.clip(dy - 1, 0, None) * 0.35) & (dy >= -8) & (dy <= 11)
    body &= ~((dy < -4) & (np.abs(dx) < 3.5 - (dy + 8) * 0.3))                      # neck opening
    body |= (np.hypot(dx + 9, dy + 6) <= 4.2) | (np.hypot(dx - 9, dy + 6) <= 4.2)   # pauldrons
    c.put(outline_mask(body), K['ink'])
    c.put(body, KP['enemy1'])
    c.put(body & (dx + dy * 0.6 < -3), KP['enemy2'])
    c.put(body & (np.abs(dx) < 1.2) & (dy > -4), KP['enemy2'])                       # the ridge down the middle
    c.put(body & (dx > 4) & (dy > 0), KP['enemy0'])
    rim = body & ~shift(body, 0, 1)
    c.put(rim, K['violet2'])
    g.add(rim, L['violet'], 0.2)
    c.put(body & (np.abs(dy - 9) < 0.7), KW['iron2'])                                  # belt
    sigil(c, g, x, y + 1, 3.0, 3.0, t * 2, 0.8, col=KW['crim3'], light=LW['crimson'])
    for cx_, cy_, ang, age in dents:
        if age < 0:
            continue
        ux, uy = math.cos(ang), math.sin(ang)
        cut = c.line(cx_ - ux * 6 + jolt, cy_ - uy * 6, cx_ + ux * 6 + jolt, cy_ + uy * 6) & body
        hot = max(0.0, 1 - age / 6.0)
        c.put(cut, K['ink'])
        c.put(shift(cut, 0, -1) & body & ~cut, K['steel2'] if hot < 0.3 else K['white'], 0.7 + 0.3 * hot)
        if hot > 0:
            g.add(cut | shift(cut, 0, -1), L['cyan'], 0.9 * hot)


def gd_angle(f):
    keys = {6: -75, 7: -100, 8: -10, 9: 28, 10: 30, 11: 5, 12: -42, 13: -50}
    if f in keys:
        return math.radians(keys[f])
    if f <= 5:
        return math.radians(-58)
    return math.radians(-50 + (-58 + 50) * ease_in_out((f - 13) / 7.0)) if f <= 20 else math.radians(-58)


def golpe_doble_frame(f):
    t = f / FRAMES
    c, g = Canvas(AW, AH), Canvas(AW, AH)
    armory(c, g, t)
    hit1, hit2 = 8, 12
    a1, a2 = f - hit1, f - hit2
    px, py = GD_PIVOT
    py += math.sin(t * 2 * math.pi * 2) * 0.7 if (f <= 5 or f >= 20) else 0.0
    ang = gd_angle(f)
    R = 39
    bx, by = GD_PLATE
    jolt = {8: 1.5, 9: 1, 10: -0.5, 12: -1.5, 13: -1, 14: 0.5}.get(f, 0.0)
    plate_stand(c, g, bx, by, t, [(bx - 1, by - 1, math.radians(35), a1), (bx + 1, by + 3, math.radians(-30), a2)],
                jolt=jolt)
    cpal = (K['cyan1'], K['cyan2'], K['steel3'], K['white'])
    if f == 8:
        crescent(c, g, px, py, R + 0.5, math.radians(10), math.radians(90), -1, 9.0, cpal, L['cyan'], gain=1.5)
    elif f in (9, 10):
        q = (f - 9) / 2.0
        crescent(c, g, px, py, R + 0.5, math.radians(-28), math.radians(100) * (1 - 0.5 * q), -1, 8 * (1 - 0.6 * q),
                 cpal, L['cyan'], a=0.8 - 0.5 * q, dash=f == 10, gain=1.2 * (1 - q))
    if f == 12:
        crescent(c, g, px, py, R + 0.5, math.radians(42), math.radians(72), 1, 9.0, cpal, L['cyan'], gain=1.5)
    elif f in (13, 14):
        q = (f - 13) / 2.0
        crescent(c, g, px, py, R + 0.5, math.radians(50), math.radians(80) * (1 - 0.5 * q), 1, 8 * (1 - 0.6 * q),
                 cpal, L['cyan'], a=0.8 - 0.5 * q, dash=f == 14, gain=1.2 * (1 - q))
    gx, gy = px + math.cos(ang) * 5, py + math.sin(ang) * 5
    sword(c, g, gx, gy, ang, t * 2 + (1.5 if 6 <= f <= 13 else 0),
          glint=(-4 + (f - 1) * 9.0 if 1 <= f <= 5 else None), rune_gain=1.6 if 6 <= f <= 13 else 1.0)
    tx, ty = blade_point(gx, gy, ang)
    if f == 7:
        star4(c, g, tx, ty, 4, gain=1.2)
    for hit, (hx, hy) in ((hit1, (bx - 1, by - 1)), (hit2, (bx + 1, by + 3))):
        age = f - hit
        if age == 0:
            c.put(c.disc(hx, hy, 5), K['cyan2'], 0.6)
            c.put(c.disc(hx, hy, 2.5), K['white'])
            g.add(c.disc(hx, hy, 6), L['cyan'], 1.2)
        if 0 <= age <= 2:
            star4(c, g, hx, hy, {0: 10, 1: 6, 2: 3}[age], gain=1.3)
        falling_sparks(c, g, f, hit, hx, hy, 640 + hit, 10, (1.2, 2.6), 9, WARM_SPARKS, L['warm'], 52, (-0.5, 2.4))
    motes(c, g, t, 653, 6, (24, 8, 70, 50), K['cyan3'], K['steel3'], L['cyan'], gain=0.4)
    vignette(c, 0.72)
    if f in (hit1, hit2):
        shake(c, g, 1 if f == hit1 else -1, 0)
    return c, g


# ---------------------------------------------------------------------------------------- Herida Abierta
HA_GUARD = (22.0, 13.0)
HA_ANG = math.radians(36)
HA_BASIN = (56.0, 41.0)


def war_tent(c, g, t):
    """Inside a war tent at night: canvas walls with seams and poles, a lantern swinging gently, a map table."""
    gradient_bg(c, KP['canvas1'], KP['canvas2'], KP['canvas0'], 0.6)
    lx, ly = 76.0, 13.0 + math.sin(t * 2 * math.pi) * 0.5
    lit = np.clip(1 - np.hypot(c.x - lx, (c.y - ly) * 1.1) / 40, 0, 1)
    c.put(c.rect(0, 0, AW - 1, 50), KP['canvas2'], np.clip(lit * 1.4, 0, 1))
    c.put(c.rect(0, 0, AW - 1, 50), KP['canvas3'], np.clip(lit * 1.6 - 0.7, 0, 1))
    for x in (12, 34, 58):                                       # seams sag between the poles
        seam = np.abs(c.x - x - np.sin(c.y / 9.0) * 1.2) < 0.5
        c.put(seam & (c.y < 50), KP['canvas0'])
    for x in (3, 84):
        c.put(c.rect(x, 0, x + 1, 50), KP['oak1'])
        c.put(c.rect(x, 0, x, 50), KP['oak2'])
    c.put(c.line(lx, 0, lx, ly - 3), KW['iron2'])
    lamp = c.rect(int(lx) - 2, int(ly) - 2, int(lx) + 2, int(ly) + 3)
    c.put(outline_mask(lamp), K['ink'])
    c.put(lamp, KW['iron1'])
    glass = c.rect(int(lx) - 1, int(ly) - 1, int(lx) + 1, int(ly) + 2)
    c.put(glass, K['window'])
    c.put(c.pixel(lx, ly), KW['spark2'])
    g.add(glass, L['warm'], 0.7)
    g.add(c.disc(lx, ly, 7) & ~glass, L['warm'], 0.08)
    table = c.rect(0, 51, AW - 1, AH - 1)
    c.put(table, KP['oak1'])
    c.put(c.rect(0, 51, AW - 1, 51), KP['oak3'])
    c.put(c.rect(0, 52, AW - 1, 52), KP['oak2'])
    mp = c.poly([(4, 55), (30, 54), (32, 62), (5, 63)])
    c.put(outline_mask(mp) & (c.y > 52), K['ink'])
    c.put(mp, KR['paper2'])
    c.put(mp & (c.x < 12), KR['paper3'])
    c.put(c.line(9, 58, 16, 57) | c.line(16, 57, 25, 59), KW['crim2'])
    c.put(c.pixel(25, 59) | c.pixel(24, 58), KW['crim3'])


def basin(c, g, x, y, t, ripple):
    """A stone basin of dark water on a short pedestal; ripple = frames since a drop landed (rings)."""
    bowl = (np.hypot((c.x - x) / 12.0, (c.y - y) / 3.5) <= 1) | \
        (c.rect(int(x) - 12, int(y), int(x) + 12, int(y) + 5) & (np.abs(c.x - x) <= 12 - np.clip(c.y - y, 0, None) ** 1.6 * 0.5))
    stem = c.rect(int(x) - 3, int(y) + 5, int(x) + 3, 48)
    foot = c.rect(int(x) - 7, 48, int(x) + 7, 50)
    m = bowl | stem | foot
    c.put(outline_mask(m), K['ink'])
    c.put(m, KP['temple2'])
    c.put(m & (c.x < x - 4), KP['temple3'])
    c.put(m & (c.x > x + 6), KP['temple1'])
    c.put(m & (np.abs(c.y - (y + 3)) < 0.6) & (np.abs(c.x - x) < 10), KP['temple1'])
    water = np.hypot((c.x - x) / 10.0, (c.y - y) / 2.4) <= 1
    c.put(water, KW['crim0'])
    c.put(water & (c.y < y - 0.5), KW['crim1'])
    c.put(water & (np.hypot((c.x - x + 3) / 3.0, (c.y - y + 0.6) / 0.8) <= 1), KW['crim2'])
    g.add(water, LW['crimson'], 0.1)
    if 0 <= ripple <= 5:
        ring = ellipse_ring(c, x, y, 1.5 + ripple * 1.5, 0.5 + ripple * 0.4, 0.5) & water
        c.put(ring, KW['crim4'], 1 - ripple / 6)
        g.add(ring, LW['crimson'], 0.5 * (1 - ripple / 6))


def herida_abierta_frame(f):
    t = f / FRAMES
    c, g = Canvas(AW, AH), Canvas(AW, AH)
    war_tent(c, g, t)
    gx, gy = HA_GUARD
    gy += math.sin(t * 2 * math.pi * 2) * 0.6
    ux, uy = math.cos(HA_ANG), math.sin(HA_ANG)
    vx, vy = -uy, ux
    tx, ty = gx + ux * 34, gy + uy * 34
    bx, by = HA_BASIN
    lands = (7, 13)
    rip = min([f - l for l in lands if f - l >= 0], default=-1)
    basin(c, g, bx, by, t, rip)
    contact_shadow(c, bx, 50.5, 9)
    sword(c, g, gx, gy, HA_ANG, t * 2, glint=(-4 + (f - 16) * 9.0 if 16 <= f <= 20 else None))
    # two drops run down the lower edge of the blade, fall from the point into the basin
    for j, born in enumerate((1, 7)):
        a = f - born
        if 0 <= a <= 3:                                           # sliding along the edge
            k = ease_in_out((a + 1) / 4.0)
            u = 3 + k * 30
            blood_drop(c, g, gx + ux * u + vx * 2.6, gy + uy * u + vy * 2.6 + 1, 0.9)
            for b in range(1, int(u - 3), 2):                    # the wet trail it leaves
                m = c.pixel(gx + ux * (u - b) + vx * 2.6, gy + uy * (u - b) + vy * 2.6)
                c.put(m, KW['crim3'], 0.7 * (1 - b / u))
        elif 4 <= a <= 5:                                         # falls from the point
            k = (a - 3) / 2.0
            yy = ty + 1 + (by - 2 - ty) * k * k
            blood_drop(c, g, tx + 1, yy, 1.0, stretch=1.0 + k)
        splash(c, g, f, born + 6, tx + 1, by - 1, seed=661 + j)
        rising_card(c, g, f, born + 6, tx + 1 + (j * 2 - 1) * 5, by - 4, 9, life=8, seed=j)
    if f in (7, 13):
        star4(c, g, tx + 1, by - 2, 4, core=K['white'], col=KW['crim5'], edge=KW['crim4'], light=LW['crimson'])
    motes(c, g, t, 671, 6, (8, 8, 80, 48), K['violet3'], KW['spark1'], L['violet'], gain=0.4)
    vignette(c, 0.72)
    return c, g


# ---------------------------------------------------------------------------------------- Muro de Acero
MA_SHIELD = (44.0, 24.0)
MA_FLOOR = 56
MA_PAVISES = (12, 28, 44, 60, 76)


def dusk_field(c, g, t):
    """A battlefield at dusk: teal sky with a band of light at the horizon, far hills with enemy banners."""
    gradient_bg(c, K['teal1'], K['teal2'], K['teal3'], 0.62)
    c.put(np.abs(c.y - 40) < 2.5, K['moon0'], 0.18)
    twinkling_stars(c, g, t, 701, 10, (2, 2, 86, 24), avoid=(44, 20, 16))
    hills = c.poly([(0, 42), (12, 38), (26, 41), (40, 37), (56, 40), (70, 36), (88, 39), (88, 64), (0, 64)])
    c.put(hills, K['teal0'])
    c.put(hills & ~shift(hills, 0, 1), K['teal3'], 0.8)
    for x, y in ((74, 36), (82, 38)):                             # enemy banners far off on the right
        c.put(c.line(x, y, x, y - 8), K['roof'])
        flag = c.rect(x + 1, y - 8, x + 4, y - 5)
        c.put(flag, K['violet1'])
    ground = c.rect(0, 48, AW - 1, AH - 1)
    c.put(ground, KW['stone0'])
    c.put(c.rect(0, 48, AW - 1, 48), K['teal3'])
    for i in range(14):
        x = (i * 37) % AW
        c.put(c.rect(x, 50 + (i * 5) % 12, x + 1, 50 + (i * 5) % 12), KW['stone2'])


def pavise(c, g, x, top, keep, alpha=1.0, flash=0.0):
    """A tall iron pavise (a standing wall shield): rounded top, central ridge, crimson band, gold rivets."""
    dx = c.x - x
    m = (np.abs(dx) <= 6.5) & (c.y >= top + 2) & (c.y <= MA_FLOOR)
    m |= (np.hypot(dx / 6.5, (c.y - top - 3) / 3.0) <= 1)
    m &= keep
    if not m.any():
        return m
    c.put(outline_mask(m) & keep, K['ink'], alpha)
    c.put(m, KW['iron1'], alpha)
    c.put(m & (dx < -3), KW['iron2'], alpha)
    c.put(m & (np.abs(dx) < 1.0), KW['iron3'], alpha)
    c.put(m & (dx > 4), KW['iron0'], alpha)
    rim = m & ~erode(m)
    c.put(rim, K['steel1'], alpha)
    c.put(rim & (dx < 0), K['steel2'], alpha)
    band = m & (np.abs(c.y - (top + 8)) < 1.6) & ~rim
    c.put(band, KW['crim3'], alpha)
    c.put(band & (c.y < top + 7.5), KW['crim4'], alpha)
    for ry in (top + 13, top + 19):
        for rx in (x - 4, x + 4):
            c.put(c.pixel(rx, ry) & m, KW['gold2'], alpha)
    if flash > 0:
        c.put(rim, K['white'], flash)
        g.add(rim, LW['steel'], flash)
    return m


def arrow_w(c, g, tx, ty, ang, a=1.0):
    """An enemy arrow, head at (tx, ty), flying along `ang`: dark shaft, steel head, violet fletching."""
    ux, uy = math.cos(ang), math.sin(ang)
    shaft = c.line(tx - ux * 2, ty - uy * 2, tx - ux * 12, ty - uy * 12)
    head = c.line(tx, ty, tx - ux * 2, ty - uy * 2) | c.pixel(tx - ux * 2 - uy, ty - uy * 2 + ux) | \
        c.pixel(tx - ux * 2 + uy, ty - uy * 2 - ux)
    fl = c.line(tx - ux * 10 - uy * 1.5, ty - uy * 10 + ux * 1.5, tx - ux * 12, ty - uy * 12) | \
        c.line(tx - ux * 10 + uy * 1.5, ty - uy * 10 - ux * 1.5, tx - ux * 12, ty - uy * 12)
    c.put(shaft, KP['oak2'], a)
    c.put(fl, K['violet2'], a)
    c.put(head, K['steel3'], a)
    g.add(fl, L['violet'], 0.3 * a)


def muro_acero_frame(f):
    t = f / FRAMES
    c, g = Canvas(AW, AH), Canvas(AW, AH)
    dusk_field(c, g, t)
    rise = {0: 4, 1: 5, 2: 6, 3: 7}                              # each pavise's rise frame, from the middle outwards
    order = {44: 0, 28: 1, 60: 1, 12: 2, 76: 3}
    sx, sy = MA_SHIELD
    # the planted sword behind the shield
    fury = 1.0 if 15 <= f <= 20 else 0.0
    lab = sword(c, g, sx, sy - 16, math.pi / 2, t * 2, rune_gain=1.0 + (1.3 if fury else 0), fury=fury, blade=30)
    slab = shield(c, g, sx, sy, W=9.5, H=24, gem_gain=1.0 + (1.5 if 12 <= f <= 14 else 0))
    faint_ward(c, g, slab > 0, t, a=0.6)
    gx0, gy0 = shield_gem(sx, sy, 24)
    gold_pulse(c, g, gx0, gy0, sx, sy - 16, {13: 0.0, 14: 0.5, 15: 1.0}.get(f, -1))
    if f == 15:
        star4(c, g, sx, sy - 17, 7, core=K['white'], col=KW['crim5'], edge=KW['crim4'], light=LW['crimson'], gain=1.4)
    for side, born in ((-1, 15), (1, 16)):
        rising_chevrons(c, g, f, sx + side * 9, sy - 12, born, 5, KW['crim5'], LW['crimson'], count=2, gain=0.9)
    # the pavises rise out of the ground, hold, and sink back
    keep = c.y <= MA_FLOOR
    for x in MA_PAVISES:
        r0 = rise[order[x]]
        a = f - r0
        if a < 0 or f >= 22:
            continue
        full = 34
        if a <= 1:
            top = MA_FLOOR - full * (0.6 if a == 0 else 1.08)
        elif a == 2:
            top = MA_FLOOR - full
        elif f <= 17:
            top = MA_FLOOR - full
        else:
            top = MA_FLOOR - full * (1 - (f - 17) / 5.0)
        pavise(c, g, x, top, keep, flash=0.5 if a == 1 else 0.0)
        if a in (0, 1):
            dust_puffs(c, x, MA_FLOOR - 1, f, r0, 3, 1.4, rise=0.5, life=5, seed=710 + x, big=0.8)
        if 18 <= f <= 21:
            dust_puffs(c, x, MA_FLOOR - 1, f, 18, 2, 1.0, rise=0.3, life=4, seed=720 + x, big=0.6)
    if 8 <= f <= 15:                                              # 14 de escudo along the wall
        ward_rings(c, g, 44, 40, f - 8, size=26, squash=0.42, life=8)
    # arrows from the right clatter off the wall
    for j, (born, y) in enumerate(((9, 31), (11, 38), (12, 27))):
        a = f - born
        hx = 84 - a * 9 + 9
        if 0 <= a <= 1:
            arrow_w(c, g, hx, y + a * 1.5, math.pi - 0.15)
        if a == 2:
            star4(c, g, 69, y + 3, 4, core=K['white'], col=KW['spark2'], edge=KW['spark1'], light=L['warm'])
        falling_sparks(c, g, f, born + 2, 69, y + 3, 730 + j, 7, (1.0, 2.0), 8, WARM_SPARKS, L['warm'], MA_FLOOR - 2,
                       (-0.3, 1.6))
        if 3 <= a <= 6:                                           # the broken arrow drops
            arrow_w(c, g, 72 + (a - 3), y + 4 + (a - 3) ** 2, math.pi * 0.5 + 0.9, a=1 - (a - 3) / 4)
    motes(c, g, t, 741, 7, (6, 6, 82, 46), KW['plate3'], K['cyan3'], LW['steel'], gain=0.4)
    vignette(c, 0.72)
    if f == 4:
        shake(c, g, 0, 1)
    return c, g


# ---------------------------------------------------------------------------------------- Postura Firme
PF_C = (42.0, 32.0)
PF_FLOOR = 50


def storm_cliff(c, g, t, flash):
    """A cliff edge in a storm: low clouds, slanting rain, the sea far below on the right."""
    gradient_bg(c, KP['storm1'], KP['storm2'], KP['storm0'], 0.6)
    for i, (y, sp) in enumerate(((8, 1), (15, 2), (22, 1))):     # cloud bands drifting
        off = (t * sp * AW) % AW
        band = (np.abs(c.y - y - np.sin((c.x + off) / 7.0) * 1.5) < 3) & (((c.x + off + i * 11) % 31) < 22)
        c.put(band, KP['storm3'] if i != 1 else KP['storm2'], 0.6)
    if flash > 0:
        c.put(np.ones((AH, AW), bool) & (c.y < PF_FLOOR), K['moon0'], 0.3 * flash)
        bolt = c.line(70, 0, 66, 9) | c.line(66, 9, 71, 15) | c.line(71, 15, 67, 26)
        c.put(bolt, K['white'], flash)
        g.add(dilate(bolt), L['blue'], flash)
    sea = c.rect(0, 38, AW - 1, AH - 1)
    c.put(sea, K['teal1'])
    c.put(c.rect(0, 38, AW - 1, 38), K['teal3'])
    for i, y in enumerate((40, 43, 47, 52, 58)):
        off = (t * AW * (0.5 + 0.2 * i) + y * 7) % 14
        c.put(sea & (np.abs(c.y - y) < 0.6) & (((c.x + off) % 14) < 4 + i), K['teal2'] if i < 2 else K['teal3'])
    cliff = c.poly([(0, PF_FLOOR - 1), (56, PF_FLOOR - 2), (62, PF_FLOOR + 1), (60, AH), (0, AH)])
    c.put(cliff, KW['stone1'])
    c.put(cliff & ~shift(cliff, 0, 1), KW['stone3'])
    c.put(cliff & (c.y > PF_FLOOR + 4) & ((np.floor(c.x * 0.4 + c.y) % 7) == 0), KW['stone0'])
    for x in (8, 19, 31, 49):
        c.put(c.rect(x, PF_FLOOR + 2 + x % 3, x + 2, PF_FLOOR + 2 + x % 3), KW['stone2'])


def rain(c, g, t, slant, a=0.6):
    rng = np.random.default_rng(751)
    for i in range(34):
        x0, ph = rng.uniform(0, AW + 20), rng.random()
        u = frac(t * 3 + ph)
        y = u * (AH + 8) - 4
        x = x0 - u * (AH + 8) * slant
        m = c.line(x, y, x + slant * 3, y - 3)
        c.put(m, K['steel1'] if i % 3 else K['steel2'], a)


def postura_firme_frame(f):
    t = f / FRAMES
    c, g = Canvas(AW, AH), Canvas(AW, AH)
    flash = {17: 1.0, 18: 0.4}.get(f, 0.0)
    storm_cliff(c, g, t, flash)
    slant = 0.35 + (0.35 if 13 <= f <= 17 else 0.0)
    rain(c, g, t, slant, a=0.85)
    stamp = 6
    lift = {3: 1, 4: 3, 5: 4}.get(f, 0)
    sqx, sqy = {6: (1.06, 0.95), 7: (1.02, 0.98)}.get(f, (1, 1))
    cx, cy = PF_C
    shiver = 1 if (f in (14, 16)) else 0
    H = 30.0
    cy = cy - lift + H * 0.56 * (1 - sqy)
    contact_shadow(c, cx, PF_FLOOR - 0.5, 9 + (2 if lift == 0 else 0))
    if 0 <= f - stamp <= 8:                                       # cracks in the rock
        k = 1 - max(0, f - stamp - 4) / 5
        for a0 in (-2.8, -2.2, -0.4, 0.2):
            x1, y1 = cx + math.cos(a0) * 10, PF_FLOOR + abs(math.sin(a0)) * 2 + 1
            m = c.line(cx, PF_FLOOR, x1, y1) & (c.y >= PF_FLOOR - 1)
            c.put(m, K['ink'], k)
    fury_k = {10: 0.3, 11: 0.8}.get(f, 1.0 if 12 <= f <= 17 else {18: 0.7, 19: 0.4, 20: 0.15}.get(f, 0.0))
    slab = heater_labels(c, cx + shiver, cy, sx=sqx, sy=sqy)
    blood_aura(c, g, slab > 0, fury_k, t, width=2)
    paint_heater(c, g, slab, cx + shiver, cy, gem_gain=1.0 + 1.5 * fury_k, flash={6: 0.4, 11: 0.35}.get(f, 0.0),
                 glint=(-26 + (f - 19) * 9 if 19 <= f <= 23 else None))
    gx, gy = shield_gem(cx, cy, H)
    drip(c, g, f, 7, gx + 0.5, -3, gy - 1, fall=3, s=1.3, swell=1, seed=761)
    if f == 11:
        star4(c, g, gx, gy, 5, core=K['white'], col=KW['crim5'], edge=KW['crim4'], light=LW['crimson'], gain=1.3)
    if 0 <= f - stamp <= 7:
        ward_rings(c, g, cx, cy + 2, f - stamp, size=13, squash=1.0, life=8)
    if 0 <= f - 12 <= 8:                                          # Furia: a second, bigger ward with a crimson rim
        a = f - 12
        ward_rings(c, g, cx, cy + 2, a, size=19, squash=1.0, life=9)
        inner = diamond_ring(c, cx, cy + 2, 14 + min(a, 3) * 1.5, 0.5, squash=1.0)
        c.put(inner, KW['crim4'], 1 - a / 9)
        g.add(inner, LW['crimson'], 0.5 * (1 - a / 9))
    rng = np.random.default_rng(771)
    if 0 <= f - stamp <= 6:                                       # rock chips and dust
        a = f - stamp
        for i in range(6):
            a0 = rng.uniform(0.3, math.pi - 0.3)
            spd = rng.uniform(1.0, 2.0)
            x = cx + math.cos(a0) * spd * a
            y = PF_FLOOR - 1 - math.sin(a0) * spd * a + 0.3 * a * a
            if y < PF_FLOOR + 1 and a > 0:
                c.put(c.pixel(x, y), KW['stone3'])
        dust_puffs(c, cx, PF_FLOOR - 1, f, stamp, 4, 2.0, rise=0.4, life=6, seed=773)
    if 13 <= f <= 17:                                             # the gust: streaks and leaves from the right
        k = 1 - abs(f - 15) / 3
        for i in range(7):
            y = 12 + i * 5 + (i % 2) * 2
            x = 86 - (f - 13) * 10 - (i * 7) % 11
            m = c.line(x, y, x + 9, y - 1)
            c.put(m & ~(slab > 0), K['steel2'], 0.6 * k)
            lx, ly = x - 2, y + math.sin(f + i) * 1.5
            if i % 3 == 0:
                c.put(c.rect(int(lx), int(ly), int(lx) + 1, int(ly)), KR['leaf2'])
    motes(c, g, t, 781, 5, (10, 8, 76, 46), KB['b2'], KW['crim4'], LB, gain=0.4)
    vignette(c, 0.74)
    if f == stamp:
        shake(c, g, 0, 1)
    return c, g


# ---------------------------------------------------------------------------------------- Tajo Brutal
TB_PIVOT = (22.0, 44.0)
TB_IDLE = math.radians(-62)
TB_WIND = math.radians(-125)
TB_END = math.radians(12)
TB_BANNER = (65.0, 10.0)          # crossbar centre
TB_CUT = 27.0                     # height of the cut


def war_camp(c, g, t):
    """An enemy camp at night: dark tents, a far campfire, a palisade; the banner pole on the right."""
    gradient_bg(c, K['night0'], K['night2'], K['night1'], 0.6)
    twinkling_stars(c, g, t, 801, 14, (2, 2, 86, 30), avoid=(65, 24, 12))
    c.put(c.disc(40, 44, 12), K['night3'], 0.5)
    for x0, w, h in ((4, 16, 12), (30, 14, 10), (78, 14, 11)):    # tents
        tent = c.poly([(x0, 47), (x0 + w / 2, 47 - h), (x0 + w, 47)])
        c.put(tent, K['roof2'])
        c.put(c.line(x0 + w / 2, 47 - h, x0 + w / 2, 47 - h - 3), K['roof2'])
    pal = np.zeros((AH, AW), bool)
    for x in range(0, AW, 3):
        pal |= c.rect(x, 39 + (x % 2), x + 1, 47) & ~c.rect(x, 39, x + 1, 39 + (x % 2))
    c.put(pal & ~c.rect(26, 0, 50, AH), K['roof'])
    flame(c, g, 40, 46, 4, 1.8, t, 0.6, sway_amp=0.6, gain=0.7, side=False)
    ground = c.rect(0, 48, AW - 1, AH - 1)
    c.put(ground, K['night0'])
    c.put(c.rect(0, 48, AW - 1, 48), K['night3'])
    for i in range(10):
        x = (i * 29) % AW
        c.put(c.rect(x, 52 + (i * 3) % 9, x + 2, 52 + (i * 3) % 9), K['night2'])


def banner(c, g, t, length, cut_age, fury):
    """The enemy's war banner hanging from a crossbar. length = how far it is unrolled (px); cut_age >= 0: the part
    below TB_CUT has been cut away and is falling."""
    bx, by = TB_BANNER
    pole = c.rect(int(bx) + 9, int(by) - 3, int(bx) + 10, 48)
    c.put(outline_mask(pole), K['ink'])
    c.put(pole, KP['oak2'])
    bar = c.rect(int(bx) - 9, int(by) - 1, int(bx) + 10, int(by))
    c.put(bar, KW['iron2'])
    c.put(c.pixel(bx - 10, by - 1) | c.pixel(bx - 10, by), KW['gold1'])
    def cloth_mask(y_top, y_bot, dx0=0.0, dy0=0.0, swing=1.0):
        m = np.zeros((AH, AW), bool)
        for y in range(int(y_top), int(y_bot) + 1):
            if not 0 <= y + dy0 < AH:
                continue
            k = (y - by) / 36.0
            wv = math.sin(((y - by) / 8.0 - t * 2) * math.pi * 2) * (0.3 + 1.6 * k) * swing
            x0, x1 = bx - 7 + wv + dx0, bx + 7 + wv * 0.4 + dx0
            if y > by + 31:                                         # notched hem
                notch = (y - by - 30) * 0.9
                mid = (x0 + x1) / 2
                for x in range(int(round(x0)), int(round(x1)) + 1):
                    if abs(x - mid) >= notch and 0 <= x < AW:
                        m[int(y + dy0), x] = True
            else:
                m[int(y + dy0), max(0, int(round(x0))):int(round(x1)) + 1] = True
        return m

    def paint(m, a=1.0):
        c.put(outline_mask(m), K['ink'], a)
        c.put(m, KP['enemy2'], a)
        c.put(m & ((np.floor(c.x - c.y * 0.2) % 5) == 0), KP['enemy1'], a)
        c.put(m & ~shift(m, 1, 0), KP['enemy3'], a)
        emb = m & (np.hypot(c.x - bx, (c.y - by - 14)) <= 3.4) & ~(np.hypot(c.x - bx, (c.y - by - 14)) <= 1.4)
        c.put(emb, K['steel3'], a)
        eye = m & (np.hypot(c.x - bx, c.y - by - 14) <= 1.3)
        c.put(eye, KW['crim4'], a)
        g.add(eye | emb, L['violet'], 0.3 * a)

    full = 36
    if cut_age is None:
        paint(cloth_mask(by + 1, by + min(length, full)))
    else:
        paint(cloth_mask(by + 1, TB_CUT))
        if cut_age <= 6:                                          # the cut-off part falls and turns to smoke
            k = cut_age / 6
            fall = cut_age * 1.4 + cut_age * cut_age * 0.35
            lower = cloth_mask(TB_CUT + 1, by + full, dx0=cut_age * 0.8, dy0=fall, swing=1 + k)
            paint(lower, a=1 - k * 0.8)
            if cut_age >= 3:
                motes_area = lower & ((np.floor(c.x * 3 + c.y * 5 + cut_age) % 5) == 0)
                c.put(motes_area, K['violet3'], 1 - k)
                g.add(motes_area, L['violet'], 0.5 * (1 - k))
        edge = c.rect(int(bx) - 8, int(TB_CUT), int(bx) + 8, int(TB_CUT)) & dilate(cloth_mask(by + 1, TB_CUT))
        if cut_age <= 4:
            c.put(edge, KW['crim5'] if fury else K['cyan3'], 1 - cut_age / 5)
            g.add(edge, LW['crimson'] if fury else L['cyan'], 0.8 * (1 - cut_age / 5))


def tb_angle(f):
    if f <= 7:
        return TB_IDLE
    if f <= 10:
        return TB_IDLE + (TB_WIND - TB_IDLE) * ease_in_out((f - 7) / 3.0)
    if f == 11:
        return math.radians(-40)
    if f <= 15:
        return TB_END + math.radians({12: 0, 13: 4, 14: 2, 15: 0}[f])
    return TB_END + (TB_IDLE - TB_END) * ease_in_out(min(1.0, (f - 15) / 6.0))


def tajo_brutal_frame(f):
    t = f / FRAMES
    c, g = Canvas(AW, AH), Canvas(AW, AH)
    war_camp(c, g, t)
    hit = 12
    age = f - hit
    fury = 1.0 if 6 <= f <= 19 else 0.0
    if f < hit:
        banner(c, g, t, 36, None, fury)
    elif age <= 6:
        banner(c, g, t, 0, age, fury)
    else:
        banner(c, g, t, TB_CUT - TB_BANNER[1] + (36 - (TB_CUT - TB_BANNER[1])) * ease_out((age - 6) / 5.0), None, fury)
    px, py = TB_PIVOT
    py += math.sin(t * 2 * math.pi * 2) * 0.8 if (f <= 7 or f >= 21) else 0.0
    ang = tb_angle(f)
    R = 39
    cpal = (KW['crim1'], KW['crim3'], KW['crim5'], K['white'])
    if f == 11:
        crescent(c, g, px, py, R + 0.5, -ang, ang - TB_WIND, -1, 9.0, cpal, LW['crimson'], gain=1.4)
    elif 12 <= f <= 17:
        q = (f - 12) / 6.0
        crescent(c, g, px, py, R + 0.5, -TB_END, 2.3 * (1 - 0.7 * q), -1, 10.0 * (1 - q * 0.75), cpal,
                 LW['crimson'], a=1.0 - q * 0.85, dash=f >= 14, gain=1.5 * (1 - q))
    gx, gy = px + math.cos(ang) * 5, py + math.sin(ang) * 5
    lab = sword(c, g, gx, gy, ang, t * 2 + (1.5 if 8 <= f <= 12 else 0),
                glint=(-4 + (f - 18) * 9.0 if 18 <= f <= 22 else None), rune_gain=1.0 + 1.3 * fury, fury=fury)
    blood_aura(c, g, lab > 0, {6: 0.5, 7: 1.0, 8: 1.0, 9: 1.0, 10: 0.9, 11: 0.6}.get(f, 0.0), t, width=1)
    land = drip(c, g, f, 1, gx + 0.5, -3, gy - 1, fall=3, s=1.5, swell=1, seed=811)
    if land == 0:
        star4(c, g, gx, gy - 1, 5, core=K['white'], col=KW['crim5'], edge=KW['crim4'], light=LW['crimson'], gain=1.3)
    tx, ty = blade_point(gx, gy, ang)
    if f == 10:
        star4(c, g, tx, ty, 4, core=K['white'], col=KW['crim5'], edge=KW['crim4'], light=LW['crimson'], gain=1.2)
    hx, hy = TB_BANNER[0], TB_CUT
    if f == hit:
        c.put(c.disc(hx, hy, 6), KW['crim4'], 0.6)
        c.put(c.disc(hx, hy, 3), K['white'])
        g.add(c.disc(hx, hy, 7), LW['crimson'], 1.3)
    if 0 <= age <= 3:
        star4(c, g, hx, hy, {0: 11, 1: 7, 2: 4, 3: 2}[age], core=K['white'], col=KW['crim5'], edge=KW['crim4'],
              light=LW['crimson'], gain=1.4)
    for j in range(3):                                            # Furia: +5 de daño
        plus_marks(c, g, hx - 5 + j * 5, hy - 4, age - 1 - j, count=1)
    sparks(c, g, f, hit, hx, hy, 821, 10, (1.0, 2.4), 6, BLOOD_SPARKS, LW['crimson'], gravity=0.15)
    motes(c, g, t, 831, 7, (4, 8, 84, 46), KW['crim4'], K['violet3'], LW['crimson'], gain=0.4)
    vignette(c, 0.72)
    if f == hit:
        shake(c, g, 1, 0)
    return c, g


# ---------------------------------------------------------------------------------------- Vendaje
VJ_HEART = (40.0, 29.0)
VJ_S = 1.9
VJ_SHIELD = (72.0, 38.0)


def infirmary(c, g, t):
    """A field infirmary: canvas walls, a lantern, a cot and a crate of supplies, warm light pooling below."""
    gradient_bg(c, KP['canvas1'], KP['canvas2'], KP['canvas0'], 0.6)
    lx, ly = 14.0, 12.0
    lit = np.clip(1 - np.hypot(c.x - lx, (c.y - ly) * 1.1) / 42, 0, 1)
    c.put(c.rect(0, 0, AW - 1, 50), KP['canvas2'], np.clip(lit * 1.5, 0, 1))
    c.put(c.rect(0, 0, AW - 1, 50), KP['canvas3'], np.clip(lit * 1.7 - 0.8, 0, 1))
    for x in (28, 56):
        seam = np.abs(c.x - x - np.sin(c.y / 8.0) * 1.0) < 0.5
        c.put(seam & (c.y < 50), KP['canvas0'])
    c.put(c.line(lx, 0, lx, ly - 3), KW['iron2'])
    lamp = c.rect(int(lx) - 2, int(ly) - 2, int(lx) + 2, int(ly) + 3)
    c.put(outline_mask(lamp), K['ink'])
    c.put(lamp, KW['iron1'])
    glass = c.rect(int(lx) - 1, int(ly) - 1, int(lx) + 1, int(ly) + 2)
    flick = 0.8 + 0.2 * math.sin(t * 6 * math.pi)
    c.put(glass, K['window'])
    c.put(c.pixel(lx, ly), KW['spark2'])
    g.add(glass, L['warm'], 0.7 * flick)
    floor = c.rect(0, 51, AW - 1, AH - 1)
    c.put(floor, KP['oak0'])
    c.put(c.rect(0, 51, AW - 1, 51), KP['oak2'])
    for x in range(0, AW, 9):
        c.put(c.rect(x, 52, x, AH - 1), K['ink'], 0.6)
    crate = c.rect(4, 42, 20, 51)
    c.put(outline_mask(crate), K['ink'])
    c.put(crate, KP['oak1'])
    c.put(c.rect(4, 42, 20, 42) | c.rect(4, 46, 20, 46), KP['oak2'])
    cross = c.rect(11, 43, 13, 49) | c.rect(9, 45, 15, 47)
    c.put(cross, KW['crim3'])
    rolls = c.disc(9, 40, 2) | c.disc(14, 40.5, 1.7)
    c.put(outline_mask(rolls), K['ink'])
    c.put(rolls, KP['linen2'])
    c.put(rolls & (c.x + c.y < 48), KP['linen3'])


def bandage_bands(c, x, y, n, s):
    """Diagonal strips of linen wound round the heart: n 0..3 how many are on."""
    m = np.zeros((AH, AW), bool)
    hm = dilate(heart_mask(c, x, y, s))
    for i in range(2):
        if n <= i:
            break
        off = (i - 0.5) * 6.0 * s
        band = np.abs((c.x - x) * 0.55 + (c.y - y) * 0.84 - off) <= 1.0 * s
        part = min(1.0, n - i)
        m |= band & hm & (c.x <= x - 9 * s + 18 * s * part)
    return m


def vendaje_frame(f):
    t = f / FRAMES
    c, g = Canvas(AW, AH), Canvas(AW, AH)
    infirmary(c, g, t)
    hx, hy = VJ_HEART
    hy += math.sin(t * 2 * math.pi * 2) * 0.7
    # the shield leaning on the right: the block she has (Aguante)
    shx, shy = VJ_SHIELD
    contact_shadow(c, shx, 50.5, 8)
    slab = shield(c, g, shx, shy, W=9.0, H=24, gem_gain=1.0 + (1.6 if 11 <= f <= 13 else 0))
    faint_ward(c, g, slab > 0, t, a=0.6)
    heal1, heal2 = 9, 13
    fill = 0.42
    if f >= heal1:
        fill = 0.42 + 0.3 * ease_out(min(1.0, (f - heal1 + 1) / 2.0))
    if f >= heal2:
        fill = 0.72 + 0.23 * ease_out(min(1.0, (f - heal2 + 1) / 2.0))
    if f >= 20:
        fill = 0.95 - (0.95 - 0.42) * ease_in_out((f - 19) / 4.0)
    crack = 1.0 if f < heal1 else (0.0 if f < 20 else (f - 19) / 4.0)
    green = {9: 1.0, 10: 0.7, 11: 0.4, 12: 0.2, 13: 1.0, 14: 0.7, 15: 0.45, 16: 0.25, 17: 0.1}.get(f, 0.0)
    beat = 1.0 if f in (15, 18) else 0.0
    m = life_heart(c, g, hx, hy, VJ_S, fill=fill, crack=crack, green=green * 0.6, beat=beat,
                   flash={9: 0.2, 13: 0.2}.get(f, 0.0))
    # the bandage: a roll circles the heart and leaves strips wound round it
    wraps = {2: 0.3, 3: 0.6, 4: 0.9, 5: 1.2, 6: 1.5, 7: 1.8, 8: 2.0}.get(f, 2.0 if 9 <= f <= 18 else
                                                                           {19: 1.3, 20: 0.6}.get(f, 0.0))
    band = bandage_bands(c, hx, hy, wraps, VJ_S)
    c.put(outline_mask(band) & ~m, K['ink'])
    c.put(band, KP['linen2'])
    c.put(band & ~shift(band, 0, 1), KP['linen3'])
    c.put(band & ~shift(band, 0, -1), KP['linen1'])
    if 1 <= f <= 8:                                               # the roll orbiting
        th = (f - 1) / 8.0 * 2 * math.pi * 1.5 + math.pi
        rx_, ry_ = hx + math.cos(th) * 17, hy + math.sin(th) * 7
        roll = c.disc(rx_, ry_, 2.2)
        tail = c.line(rx_, ry_, hx + math.cos(th - 0.6) * 7, hy + math.sin(th - 0.6) * 4)
        c.put(tail, KP['linen2'])
        c.put(outline_mask(roll), K['ink'])
        c.put(roll, KP['linen2'])
        c.put(roll & (c.x + c.y < rx_ + ry_), KP['linen3'])
        c.put(c.pixel(rx_, ry_), KP['linen1'])
    if wraps >= 2 and 9 <= f <= 18:                               # the knot's two loose ends flutter
        kx, ky = hx + 6 * VJ_S, hy - 4
        for j, dy in enumerate((-1, 2)):
            end = c.line(kx, ky, kx + 4, ky + dy + math.sin(t * 8 * math.pi + j) * 1.2)
            c.put(end, KP['linen2'])
    heal_crosses(c, g, f, heal1, hx, hy - 4, count=4, life=8, spread=9, seed=851)
    heal_crosses(c, g, f, heal2, hx, hy - 4, count=4, life=8, spread=10, seed=853)
    sparks(c, g, f, heal1, hx, hy, 855, 9, (0.6, 1.6), 6, HEAL_SPARKS, LP['heal'], gravity=-0.04, gain=0.8)
    gx0, gy0 = shield_gem(shx, shy, 24)
    gold_pulse(c, g, gx0 - 1, gy0, hx + 11, hy, {10: 0.0, 11: 0.4, 12: 0.8, 13: 1.0}.get(f, -1))
    if f == 10:
        star4(c, g, gx0, gy0, 4, core=K['white'], col=KW['gold3'], edge=KW['gold2'], light=LW['gold'], gain=1.2)
    if f in (heal1, heal2):
        star4(c, g, hx, hy - 1, 7, core=K['white'], col=KP['heal3'], edge=KP['heal2'], light=LP['heal'], gain=1.3)
    motes(c, g, t, 861, 7, (20, 8, 64, 46), KP['heal3'], KW['spark1'], LP['heal'], gain=0.35)
    vignette(c, 0.72)
    return c, g


ILLUSTRATIONS = {
    'corte_sangriento': corte_sangriento_frame,
    'embate_firme': embate_firme_frame,
    'escudo_sangre': escudo_sangre_frame,
    'golpe_doble': golpe_doble_frame,
    'herida_abierta': herida_abierta_frame,
    'muro_acero': muro_acero_frame,
    'postura_firme': postura_firme_frame,
    'tajo_brutal': tajo_brutal_frame,
    'vendaje': vendaje_frame,
}


def render(card_id):
    fn = ILLUSTRATIONS[card_id]
    base, glow = [], []
    for f in range(FRAMES):
        b, gl = fn(f)
        base.append(b)
        glow.append(gl)
    return base, glow
