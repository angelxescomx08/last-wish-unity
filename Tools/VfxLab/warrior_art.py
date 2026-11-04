"""Animated pixel-art illustrations for the warrior's basic cards (88x64, 24 frames at 12 fps, seamless loop).

Same technique, palette and helpers as rogue_art.py and mage_art.py (docs/art-style.md): every frame has a base image
(opaque pixel art, readable without bloom) and an emissive light map, baked for the game by export_game_card_art.py.
No characters: each picture is her gear in action (the runed longsword and the crimson heater shield of her combat
sprite, Tools/VfxLab/warrior_hero.html), like the dagger pictures of the rogue and the spells of the mage, so nothing
is assembled from body parts and a redesign of the warrior sprite does not touch them.

  espadazo        Espadazo (6 de daño): the sword winds up, one heavy cleave down to the ground, cracks and debris.
  bloqueo         Bloqueo (5 de escudo): the planted shield takes an axe blow, slides back, sparks bounce on the floor.
  guardia_ferrea  Guardia Férrea (5 de escudo; Aguante: +1 Fuerza): iron plates lock into a bulwark round the shield
                  and the planted sword; with the guard held, the sword's runes turn crimson (Fuerza).
"""
import math

import numpy as np

from pixelkit import Canvas, hexc, dilate, erode, shift
from rogue_art import (AW, AH, FRAMES, K, L, ease_out, ease_in_out, frac, gradient_bg, vignette, star4, motes,
                       outline_mask, crescent)
from mage_art import twinkling_stars, sparks, ellipse_ring

# the rogue palette plus the warrior's own ramps (warrior_hero.html: crimson tabard and plume, gold trim, red grip)
KW = dict(K)
KW.update({
    'crim0': hexc('#25070f'), 'crim1': hexc('#44101c'), 'crim2': hexc('#6c1828'), 'crim3': hexc('#9c2434'),
    'crim4': hexc('#c83a40'), 'crim5': hexc('#ff7464'),
    'gold0': hexc('#5e4626'), 'gold1': hexc('#8a6a3a'), 'gold2': hexc('#d9b26a'), 'gold3': hexc('#f4dc9c'),
    'grip0': hexc('#2a0c14'), 'grip1': hexc('#4a1622'), 'grip2': hexc('#6e2632'),
    'iron0': hexc('#16141f'), 'iron1': hexc('#2a2838'), 'iron2': hexc('#454459'), 'iron3': hexc('#6e6c86'),
    'wood0': hexc('#1d1016'), 'wood1': hexc('#3a2126'), 'wood2': hexc('#5c3a36'),
    'stone0': hexc('#0a0d16'), 'stone1': hexc('#111827'), 'stone2': hexc('#18233a'), 'stone3': hexc('#22314d'),
    'spark0': hexc('#a8603a'), 'spark1': hexc('#f2b35e'), 'spark2': hexc('#ffe0b0'),
    'plate0': hexc('#1e2b48'), 'plate1': hexc('#35507e'), 'plate2': hexc('#5f86bf'), 'plate3': hexc('#a9c8f2'),
})
LW = dict(L)
LW.update({
    'crimson': np.array([1.0, 0.22, 0.18, 1]), 'gold': np.array([1.0, 0.78, 0.38, 1]),
    'steel': np.array([0.45, 0.62, 1.0, 1]),
})
WARM_SPARKS = (KW['spark2'], KW['spark1'], KW['spark0'])
CYAN_SPARKS = (K['cyan4'], K['cyan2'], K['cyan1'])


# ---------------------------------------------------------------------------------------- shared bits
def shake(c, g, dx, dy):
    """Screen shake on the impact frames: the whole picture moves by whole pixels (edges repeat)."""
    for cv in (c, g):
        p = np.roll(cv.px, (dy, dx), axis=(0, 1))
        if dy > 0:
            p[:dy] = p[dy]
        elif dy < 0:
            p[dy:] = p[dy - 1]
        if dx > 0:
            p[:, :dx] = p[:, dx:dx + 1]
        elif dx < 0:
            p[:, dx:] = p[:, dx - 1:dx]
        cv.px = p


def falling_sparks(c, g, f, born, x, y, seed, count, speed, life, cols, light, floor, spread, gravity=0.28, gain=1.0):
    """Metal sparks thrown from (x, y) that fall, hit the floor at `floor` and bounce once (y-up angles)."""
    rng = np.random.default_rng(seed)
    age = f - born
    for i in range(count):
        a0 = rng.uniform(*spread)
        spd = rng.uniform(*speed)
        lf = life + int(rng.integers(-1, 2))
        if not 0 < age <= lf:
            continue
        vx, vy = math.cos(a0) * spd, -math.sin(a0) * spd
        px, py = x, y
        for _ in range(age):                     # integrate per frame so the bounce is exact
            px += vx
            py += vy
            vy += gravity
            if py >= floor and vy > 0:
                py = floor - (py - floor) * 0.4
                vy *= -0.35
                vx *= 0.6
        a = 1 - age / (lf + 1)
        m = c.pixel(px, py)
        c.put(m, cols[0] if age < 3 else cols[1] if age < lf - 1 else cols[2])
        g.add(m, light, gain * a)
        if age < 4:
            m2 = c.line(px, py, px - vx * 0.8, py - (vy - gravity) * 0.8)
            c.put(m2 & ~m, cols[1], a)
            g.add(m2, light, 0.35 * a * gain)


def dust_puffs(c, x, y, f, born, count, spread, rise=0.5, life=7, seed=0, big=1.0):
    """Dust clouds kicked from the floor that drift out, grow and thin out (dithered)."""
    rng = np.random.default_rng(seed)
    age = f - born
    if not 0 <= age <= life:
        return
    k = 1 - age / (life + 1)
    for i in range(count):
        side = -1 if i % 2 == 0 else 1
        sp = rng.uniform(0.6, 1.0) * spread
        px = x + side * (2 + age * sp)
        py = y - age * rise * rng.uniform(0.6, 1.2)
        r = (0.9 + age * 0.45) * big
        m = c.disc(px, py, r)
        c.put(m, KW['dust1'], 0.75 * k)
        c.put(m & c.disc(px - side * 0.6, py + 0.5, r * 0.75), KW['dust0'], k)


def rising_chevrons(c, g, f, x, y, born, life, col, light, count=3, gain=1.0):
    """Small ^ marks that rise and fade: Fuerza going up."""
    age = f - born
    if not 0 <= age <= life:
        return
    k = 1 - age / (life + 1)
    for i in range(count):
        a = age - i * 1.5
        if a < 0:
            continue
        cy = y - a * 2.0
        cx = x + (i - (count - 1) / 2) * 5
        m = c.line(cx - 2, cy + 2, cx, cy) | c.line(cx, cy, cx + 2, cy + 2)
        c.put(m, col, min(1.0, k * 1.4))
        g.add(m, light, gain * k)


# ---------------------------------------------------------------------------------------- the longsword
def sword_labels(c, gx, gy, ang, s=1.0, blade=34.0):
    """Rasterise her runed longsword: crossguard centre at (gx, gy), blade pointing at `ang` (screen radians).
    Labels: 1 lit edge, 2 blade, 3 blade shade, 4 dark edge, 5 fuller (runes), 6 guard gold, 7 guard shade,
    8 grip, 9 grip wraps, 10 gem, 11 pommel gold, 12 pommel light. Returns labels and local (u, v)."""
    ux, uy = math.cos(ang), math.sin(ang)
    vx, vy = -uy, ux
    dx, dy = c.x - gx, c.y - gy
    u = (dx * ux + dy * uy) / s
    v = (dx * vx + dy * vy) / s
    av = np.abs(v)
    lab = np.zeros((c.h, c.w), int)
    wb = np.where(u < blade - 7.0, 2.7, 2.7 * np.clip((blade - u) / 7.0, 0, 1) ** 0.9)
    bl = (u >= 0) & (u <= blade) & (av <= wb + 0.05)
    lab[bl] = 2
    lab[bl & (v > 0.3)] = 3
    lab[bl & (v <= -wb + 0.95)] = 1
    lab[bl & (v >= wb - 0.95)] = 4
    lab[bl & (av <= 0.6) & (u > 2.5) & (u < blade - 9)] = 5
    sh = np.clip(av - 5.0, 0, None) * 0.55                  # quillons bend towards the blade
    guard = (av <= 8.2) & (u >= -2.4 + sh) & (u < 0.3 + sh)
    lab[guard] = 6
    lab[guard & (u > -0.9 + sh)] = 7
    lab[(av <= 1.3) & (u >= -2.2) & (u < 0.2)] = 10
    grip = (u >= -11.5) & (u < -2.4) & (av <= 1.75)
    lab[grip] = 8
    lab[grip & ((np.floor((u + v * 0.9 + 12) / 2.2) % 2) == 0)] = 9
    pd = np.hypot(u + 13.6, v)
    lab[pd <= 2.5] = 11
    lab[(pd <= 2.5) & (v < -0.4) & (u < -13.2)] = 12
    lab[pd <= 0.9] = 10
    return lab, u, v


def paint_sword(c, g, lab, u, t_rune, glint_u=None, rune_gain=1.0, ghost=None, fury=0.0, blade=34.0):
    """Paint sword labels. ghost=(colour, alpha, light) paints a translucent after-image instead of steel.
    fury 0..1 turns the runes from cyan to crimson (Fuerza / Furia)."""
    m = lab > 0
    if ghost is not None:
        col, a, light = ghost
        edge = outline_mask(m)
        c.put(edge, col, a)
        c.put(m, K['cyan0'], a * 0.35)
        c.put(m & (lab == 1), K['cyan3'], a * 0.8)
        g.add(edge, light, a * 0.45)
        return
    c.put(outline_mask(m), K['ink'])
    colors = {1: K['steel4'], 2: K['steel2'], 3: K['steel1'], 4: K['steel0'], 6: KW['gold2'], 7: KW['gold1'],
              8: KW['grip0'], 9: KW['grip2'], 11: KW['gold1'], 12: KW['gold3']}
    for k, col in colors.items():
        c.put(lab == k, col)
    c.put((lab == 6) & (np.roll(lab, 1, axis=0) != 6) & (np.roll(lab, 1, axis=1) != 6), KW['gold3'])
    fl = lab == 5
    wave = 0.5 + 0.5 * np.sin((u / 9.0 - t_rune) * math.pi * 2)
    pat = (np.floor(u) % 3) != 2
    if fury < 0.5:
        ramp, light = (K['cyan0'], K['cyan1'], K['cyan2'], K['cyan3']), L['cyan']
    else:
        ramp, light = (KW['crim1'], KW['crim3'], KW['crim4'], KW['crim5']), LW['crimson']
    c.put(fl, ramp[0])
    c.put(fl & pat, ramp[1])
    c.put(fl & pat & (wave > 0.55), ramp[2])
    c.put(fl & pat & (wave > 0.85), ramp[3])
    g.add(fl & pat, light, rune_gain * (0.25 + 0.75 * wave))
    gem = lab == 10
    c.put(gem, K['cyan3'] if fury < 0.5 else KW['crim5'])
    g.add(gem, L['cyan'] if fury < 0.5 else LW['crimson'], 0.9 * rune_gain)
    if glint_u is not None:
        gl = (lab == 1) & (np.abs(u - glint_u) <= 2.6)
        c.put(gl, K['white'])
        g.add(gl, L['white'], 0.8)
        g.add((lab == 2) & (np.abs(u - glint_u) <= 1.2), L['cyan'], 0.4)


def blade_point(gx, gy, ang, s=1.0, blade=34.0):
    return gx + math.cos(ang) * blade * s, gy + math.sin(ang) * blade * s


# ---------------------------------------------------------------------------------------- the heater shield
def heater_labels(c, cx, cy, W=11.5, H=30.0, sx=1.0, sy=1.0):
    """Her crimson heater shield facing the viewer; (cx, cy) is the centre of its height. sx/sy squash it.
    Labels: 1 steel rim, 2 gold trim, 3 crimson field, 4 emblem sword (gold), 5 emblem shade, 6 gem, 7 gem light,
    8 rivets."""
    dx = (c.x - cx) / sx
    dy = (c.y - cy) / sy
    top, bot = -H * 0.44, H * 0.56
    waist = top + H * 0.42
    cham = np.clip(top + 2.6 - dy, 0, None)                 # cut top corners
    upper = (dy >= top) & (dy <= waist) & (np.abs(dx) <= W - cham)
    k = np.clip((dy - waist) / (bot - waist), 0, 1)
    lower = (dy > waist) & (dy <= bot) & (np.abs(dx) <= W * (1 - k ** 1.6) + 0.3)
    m = upper | lower
    lab = np.zeros((c.h, c.w), int)
    lab[m] = 3
    e1 = erode(m)
    e2 = erode(e1)
    lab[m & ~e1] = 1
    lab[e1 & ~e2] = 1
    lab[e2 & ~erode(e2)] = 2
    inner = erode(e2)
    # emblem: a gold sword pointing down, crossguard high, gem on the crossguard
    gy = top + 8.5
    blade = inner & (np.abs(dx) <= 1.2) & (dy > gy + 1) & (dy < bot - 6.5 + 0 * dx)
    tipk = np.clip((dy - (bot - 10.5)) / 4.0, 0, 1)
    blade &= np.abs(dx) <= 1.2 * (1 - tipk) + 0.25
    cross = inner & (np.abs(dy - gy) <= 0.8) & (np.abs(dx) <= 6.0)
    hilt = inner & (np.abs(dx) <= 0.7) & (dy < gy) & (dy > gy - 4.0)
    pom = (np.hypot(dx, dy - (gy - 5.0)) <= 1.3) & inner
    emb = blade | cross | hilt | pom
    lab[emb] = 4
    lab[emb & (dx > 0.2)] = 5
    gem = (np.abs(dx) / 2.4 + np.abs(dy - gy) / 2.6) <= 1.0
    lab[gem] = 6
    lab[gem & (dx + (dy - gy) < -0.6)] = 7
    for rx, ry in ((-W + 4.2, top + 3.6), (W - 4.2, top + 3.6), (-W + 5.2, waist + 3.5), (W - 5.2, waist + 3.5)):
        lab[(np.abs(dx - rx) < 0.6) & (np.abs(dy - ry) < 0.6) & inner] = 8
    return lab


def paint_heater(c, g, lab, cx, cy, gem_gain=1.0, glint=None, flash=0.0):
    m = lab > 0
    c.put(outline_mask(m), K['ink'])
    dx, dy = c.x - cx, c.y - cy
    light = -(dx * 0.75 + dy * 0.6)
    field = lab == 3
    c.put(field, KW['crim2'])
    c.put(field & (light > -4), KW['crim3'], np.clip((light + 4) / 7, 0, 1))
    c.put(field & (light > 5), KW['crim4'], np.clip((light - 5) / 6, 0, 1) * 0.9)
    c.put(field & (light < -7), KW['crim1'], np.clip((-7 - light) / 5, 0, 1))
    c.put(field & (dx > 0) & (np.roll(lab, -1, axis=1) == 2), KW['crim1'])          # inner bevel shade, right side
    rim = lab == 1
    c.put(rim, K['steel1'])
    c.put(rim & (light > -4), K['steel2'])
    c.put(rim & (light > 5), K['steel3'])
    c.put(rim & (light < -9), K['steel0'])
    c.put(rim & (light > 10), K['steel4'])
    tr = lab == 2
    c.put(tr, KW['gold1'])
    c.put(tr & (light > -2), KW['gold2'])
    c.put(tr & (light > 8), KW['gold3'])
    c.put(lab == 4, KW['gold2'])
    c.put((lab == 4) & (light > 6), KW['gold3'])
    c.put(lab == 5, KW['gold1'])
    c.put(lab == 8, KW['gold1'])
    c.put((lab == 8) & (light > 0), KW['gold3'])
    gem = (lab == 6) | (lab == 7)
    c.put(lab == 6, K['cyan2'])
    c.put(lab == 7, K['cyan4'])
    g.add(gem, L['cyan'], 0.9 * gem_gain)
    g.add(lab == 7, L['white'], 0.35 * gem_gain)
    if glint is not None:                         # a highlight sweeping down the rim
        gl = (rim | tr) & (np.abs((dx + dy) - glint) <= 1.5)
        c.put(gl, K['white'])
        g.add(gl, L['white'], 0.7)
    if flash > 0:                                 # the face lights up on a hit
        c.put(field, KW['crim5'] if flash > 0.4 else KW['crim4'])
        c.put(rim | tr, K['white'] if flash > 0.4 else K['steel4'])
        g.add(rim | tr, LW['steel'], flash * 0.8)
        g.add(field, LW['crimson'], flash * 0.35)


# ---------------------------------------------------------------------------------------- Espadazo
S_PIVOT = (29.0, 41.0)        # where her hands would hold the grip; the sword turns about it
S_HAND = 5.0                  # pivot -> crossguard
S_BLADE = 34.0
S_IDLE = math.radians(-66)
S_WIND = math.radians(-112)
S_HIT = math.radians(27)
S_GROUND = 56


def espadazo_scene(c, g, t):
    gradient_bg(c, K['night0'], K['night2'], K['night1'], 0.6)
    c.put(c.disc(66, 18, 24), K['night3'], 0.22)               # dithered glow behind the impact side
    c.put(c.disc(66, 18, 13), K['night3'], 0.35)
    twinkling_stars(c, g, t, 211, 20, (2, 2, 86, 34), avoid=(30, 12, 8))
    far = c.poly([(0, 40), (10, 35), (19, 38), (30, 31), (41, 37), (52, 33), (63, 37), (74, 32), (88, 36),
                  (88, 64), (0, 64)])
    c.put(far, K['vio0'])
    c.put(far & ~np.roll(far, 1, axis=0), K['moon0'], 0.55)
    # rampart with merlons, arrow slits that flicker
    wall = c.rect(0, 45, AW - 1, 52)
    for x in range(1, AW, 7):
        wall |= c.rect(x, 42, x + 3, 45)
    c.put(wall, K['roof'])
    c.put(outline_mask(wall) & (c.y < 46), K['roof2'])
    for i, x in enumerate((8, 22, 43, 57)):
        lit = 0.5 + 0.5 * math.sin((t * (1 + i % 2) + i * 0.29) * math.pi * 2)
        m = c.rect(x, 47, x, 48)
        c.put(m, K['window'] if lit > 0.3 else K['window2'])
        g.add(m, L['warm'], 0.25 * lit)
    # torn crimson banner on a pole, waving (two waves per loop): the cloth hangs from a crossbar on the left
    pole = c.line(81, 15, 81, 51)
    c.put(pole | c.line(82, 15, 82, 51), KW['iron1'])
    c.put(pole, KW['iron2'])
    c.put(c.rect(70, 16, 82, 16), KW['iron2'])
    c.put(c.rect(69, 16, 69, 16) | c.rect(83, 15, 83, 15), KW['gold2'])
    cloth = np.zeros((AH, AW), bool)
    shade_rows = {}
    for y in range(17, 44):
        k = (y - 17) / 27.0
        wav = math.sin(((y - 17) / 8.0 - t * 2) * math.pi * 2)
        dxw = wav * (0.3 + 2.2 * k)
        x0, x1 = 71 + dxw, 80 + dxw * 0.35
        if y >= 37:                                             # swallowtail: a notch opens upward from the hem
            notch = (y - 36) * 0.75
            mid = (x0 + x1) / 2
            for x in range(int(round(x0)), int(round(x1)) + 1):
                if abs(x - mid) >= notch:
                    cloth[y, x] = True
        else:
            cloth[y, int(round(x0)):int(round(x1)) + 1] = True
        shade_rows[y] = wav
    c.put(outline_mask(cloth), K['ink'])
    c.put(cloth, KW['crim3'])
    for y, wv in shade_rows.items():
        row = cloth & (c.y > y) & (c.y < y + 1)
        if wv < -0.45:
            c.put(row, KW['crim2'])
        elif wv > 0.7:
            c.put(row & (c.x < 76), KW['crim4'])
    c.put(cloth & (c.y < 18.5), KW['gold1'])
    emb = cloth & ((np.abs(c.y - 24) < 0.6) & (np.abs(c.x - 75.5 - shade_rows[24] * 0.9) < 2.2))
    emb |= cloth & (np.abs(c.x - 75.5 - np.interp(c.y, list(shade_rows), list(shade_rows.values())) * 0.9) < 0.6) & (c.y > 21) & (c.y < 32)
    c.put(emb, KW['gold2'])
    # courtyard: flagstones
    ground = c.rect(0, 53, AW - 1, AH - 1)
    c.put(ground, KW['stone1'])
    c.put(c.rect(0, 53, AW - 1, 53), KW['stone3'])
    for row, y in enumerate((56, 60)):
        c.put(c.rect(0, y, AW - 1, y), KW['stone0'])
        for x in range((row * 6) % 13, AW, 13):
            c.put(c.rect(x, y - (3 if row else 2), x, y - 1), KW['stone0'])
    c.put(c.rect(0, 54, AW - 1, 54), KW['stone2'], 0.6)


def sword_angle(f):
    """Angle of the blade per frame: hover (0-8), wind-up (9-12), cleave (13-14), follow-through (15-17),
    rise back (18-23)."""
    if f <= 8:
        return S_IDLE
    if f <= 12:
        return S_IDLE + (S_WIND - S_IDLE) * ease_in_out(min(1.0, (f - 8) / 3.0))
    if f == 13:
        return math.radians(-14)
    if f <= 17:
        return S_HIT + math.radians({14: 0, 15: 4, 16: 2, 17: 0}[f])
    k = (f - 17) / 7.0
    return S_HIT + (S_IDLE - S_HIT) * ease_in_out(k)


def espadazo_frame(f):
    t = f / FRAMES
    c, g = Canvas(AW, AH), Canvas(AW, AH)
    espadazo_scene(c, g, t)
    hit = 14
    age = f - hit
    px, py = S_PIVOT
    bob = math.sin(t * 2 * math.pi * 2) * 0.8 if (f <= 8 or f >= 21) else 0.0
    dip = {12: 1.0, 13: 0.0}.get(f, 0.0)                         # crouch into the wind-up
    py += bob + dip
    ang = sword_angle(f)
    R = S_HAND + S_BLADE                                          # tip radius about the pivot
    ix, iy = px + math.cos(S_HIT) * R, py + math.sin(S_HIT) * R  # impact spot (the tip on the hit frame)
    ix, iy = ix - 1, S_GROUND - 1

    # ground crack: dark fissures from the impact, cyan light seeping out, fading
    if 0 <= age <= 9:
        k = 1 - max(0, age - 3) / 7.0
        rng = np.random.default_rng(5)
        for j, a0 in enumerate((-2.9, -2.55, -0.3, -0.05, 0.25)):
            ln = (4 + 5 * min(1.0, (age + 1) / 2.0)) * rng.uniform(0.7, 1.2)
            x0, y0 = ix, iy + 1
            pts = [(x0, y0)]
            for s_ in range(3):
                x0 += math.cos(a0) * ln / 3 + rng.uniform(-0.8, 0.8)
                y0 += abs(math.sin(a0)) * ln / 9 + rng.uniform(-0.4, 0.6)
                pts.append((x0, y0))
            for (a, b), (cc, d) in zip(pts, pts[1:]):
                m = c.line(a, b, cc, d) & (c.y >= 54)
                c.put(m, K['ink'], k)
                c.put(m & ~shift(m, 1, 0), K['cyan1'], 0.8 * k * (1 if age < 5 else 0.5))
                g.add(m, L['cyan'], 0.5 * k * max(0.2, 1 - age / 6))
    # shock ring on the floor
    if 0 <= age <= 5:
        rad = {0: 3, 1: 7, 2: 11, 3: 14, 4: 16, 5: 17}[age]
        a = {0: 1.0, 1: 0.85, 2: 0.6, 3: 0.4, 4: 0.25, 5: 0.12}[age]
        band = ellipse_ring(c, ix, iy + 1, rad, rad * 0.22, 0.55) & (c.y > 52)
        c.put(band, K['cyan3'], a)
        g.add(band, L['cyan'], 0.9 * a)

    # wind-up: motes pulled into the blade, a glint runs up to the tip
    if 9 <= f <= 12:
        rng = np.random.default_rng(71)
        k = (f - 9) / 3.0
        for i in range(10):
            uu = rng.uniform(10, R)
            a0 = rng.uniform(0, 2 * math.pi) + k * 1.3
            r0 = rng.uniform(8, 14) * (1 - ease_in_out(k)) + 1.5
            bx_, by_ = px + math.cos(ang) * uu, py + math.sin(ang) * uu
            m = c.pixel(bx_ + math.cos(a0) * r0, by_ + math.sin(a0) * r0)
            c.put(m, K['cyan3'] if i % 3 else KW['gold3'])
            g.add(m, L['cyan'] if i % 3 else LW['gold'], 0.9)

    # the cleave: one big crescent from behind her shoulder down to the ground, after-images, speed lines
    head_up = -ang                                                 # crescent angles are y-up
    cpal = (K['cyan1'], K['cyan2'], K['steel3'], K['white'])
    if f == 13:
        crescent(c, g, px, py, R + 0.5, head_up, min(1.75, ang - S_WIND), -1, 11.0, cpal, L['cyan'], a=1.0, gain=1.5)
    elif 14 <= f <= 19:
        q = (f - 14) / 6.0
        crescent(c, g, px, py, R + 0.5, -S_HIT, 1.9 * (1 - 0.7 * q), -1, 10.0 * (1 - q * 0.75),
                 cpal, L['cyan'], a=1.0 - q * 0.85, dash=f >= 16, gain=1.6 * (1 - q))
    if 13 <= f <= 16:                                              # speed lines along the arc
        k = {13: 1.0, 14: 0.9, 15: 0.5, 16: 0.25}[f]
        for i in range(6):
            th = S_WIND + (S_HIT - S_WIND) * (0.15 + 0.13 * i)
            rr = R - 9 - (i * 5) % 7
            sx_, sy_ = px + math.cos(th) * rr, py + math.sin(th) * rr
            tx_, ty_ = -math.sin(th), math.cos(th)
            ln = (5 + (i * 3) % 5) * k
            m = c.line(sx_, sy_, sx_ - tx_ * ln, sy_ - ty_ * ln)
            c.put(m, K['steel3'] if i % 2 else K['cyan3'], k)
            g.add(m, L['cyan'], 0.4 * k)

    # the sword
    gx, gy = px + math.cos(ang) * S_HAND, py + math.sin(ang) * S_HAND
    lab, u, v = sword_labels(c, round(gx * 2) / 2, round(gy * 2) / 2, ang)
    glint = -4 + (f - 18) * 9.0 if 18 <= f <= 22 else (-4 + f * 9.0 if 2 <= f <= 6 else None)
    charge = 10 <= f <= 14
    paint_sword(c, g, lab, u, t * 2 + (1.5 if charge else 0), glint, rune_gain=1.9 if charge else 1.0)
    tx, ty = blade_point(gx, gy, ang)
    if f == 12:                                                    # the anticipation gleam on the raised tip
        star4(c, g, tx, ty + 1, 4, gain=1.2)

    # impact: flash, star, debris and sparks thrown up, dust
    if f == hit:
        c.put(c.disc(ix, iy, 8.0), K['cyan2'], 0.75)
        c.put(c.disc(ix, iy, 5.0), K['cyan4'])
        c.put(c.disc(ix, iy, 2.6), K['white'])
        g.add(c.disc(ix, iy, 8.5), L['cyan'], 1.4)
    if hit <= f <= hit + 3:
        star4(c, g, ix, iy - 1, {0: 13, 1: 9, 2: 5, 3: 2}[age], gain=1.5)
    rng = np.random.default_rng(13)
    for i in range(7):                                             # stone chips
        a0 = rng.uniform(0.35, math.pi - 0.35)
        spd = rng.uniform(1.2, 2.4)
        if 0 < age <= 7:
            cx_ = ix + math.cos(a0) * spd * age
            cy_ = iy - math.sin(a0) * spd * age + 0.32 * age * age
            if cy_ < S_GROUND + 1:
                m = c.rect(int(cx_), int(cy_), int(cx_) + (i % 2), int(cy_) + ((i + age) % 2))
                c.put(outline_mask(m) & (c.y < S_GROUND), K['ink'], 0.8)
                c.put(m, KW['stone3'] if i % 3 else K['dust1'])
                c.put(m & c.pixel(cx_, cy_), K['steel1'])
    falling_sparks(c, g, f, hit, ix, iy - 1, 31, 14, (1.6, 3.4), 9, WARM_SPARKS, L['warm'], S_GROUND,
                   (0.25, math.pi - 0.25), gain=1.0)
    sparks(c, g, f, hit, ix, iy - 2, 37, 10, (1.4, 3.0), 7, CYAN_SPARKS, L['cyan'], gravity=0.18,
           spread=(0.3, math.pi - 0.3))
    dust_puffs(c, ix, S_GROUND - 1, f, hit + 1, 4, 2.0, rise=0.5, life=7, seed=3)

    motes(c, g, t, 223, 8, (2, 6, 86, 50), K['cyan3'], KW['gold3'], L['cyan'], gain=0.55)
    vignette(c, 0.7)
    if f == hit:
        shake(c, g, 0, 1)
    elif f == hit + 1:
        shake(c, g, -1, 0)
    return c, g


# ---------------------------------------------------------------------------------------- Bloqueo
B_C = (40.0, 40.0)            # shield centre (its point rests on the floor)
B_FLOOR = 57
B_HANDS = (60.0, -46.0)       # the axe swings about an attacker's hands above the picture
B_REACH = 76.0


def crypt_scene(c, g, t, light_x=26.0):
    """Crypt hall in cold teal: brick wall lit by a shaft from a high window, two pillars, flagstone floor."""
    gradient_bg(c, K['teal0'], K['teal2'], K['teal1'], 0.6)
    wall = c.rect(0, 0, AW - 1, 51)
    lit = np.clip(1 - np.hypot((c.x - light_x) / 34, (c.y - 18) / 30), 0, 1)
    c.put(wall, KW['stone1'])
    c.put(wall, KW['stone2'], np.clip(lit * 1.6, 0, 1))
    c.put(wall, KW['stone3'], np.clip(lit * 1.8 - 0.8, 0, 1))
    for row, y in enumerate(range(2, 51, 6)):                    # mortar
        c.put(c.rect(0, y, AW - 1, y), K['teal0'], 0.85)
        off = 0 if row % 2 else 6
        for x in range(off, AW, 12):
            c.put(c.rect(x, y + 1, x, y + 5), K['teal0'], 0.85)
            c.put(c.rect(x + 1, y + 1, x + 1, y + 1), KW['stone3'], np.clip(lit[y + 1, min(x + 1, AW - 1)] * 2, 0, 1))
    for x0 in (3, 77):                                           # pillars
        p = c.rect(x0, 0, x0 + 7, 51)
        c.put(p, KW['stone0'])
        c.put(c.rect(x0, 0, x0, 51), KW['stone2'])
        c.put(c.rect(x0 + 7, 0, x0 + 7, 51), K['ink'])
        for y in (10, 30):
            c.put(c.rect(x0 - 1, y, x0 + 8, y + 1), KW['stone2'])
            c.put(c.rect(x0 - 1, y + 1, x0 + 8, y + 1), K['ink'])
    # light shaft from a high window (dithered, slightly breathing)
    br = 0.85 + 0.15 * math.sin(t * 2 * math.pi)
    shaft = np.abs((c.x - light_x) - (c.y - 0) * 0.42) < 7 + c.y * 0.12
    c.put(shaft & (c.y < 52), K['teal3'], 0.32 * br)
    g.add(shaft & (c.y < 52), L['blue'], 0.05 * br)
    floor = c.rect(0, 52, AW - 1, AH - 1)
    c.put(floor, KW['stone1'])
    c.put(c.rect(0, 52, AW - 1, 52), KW['stone3'])
    c.put(c.rect(0, 53, AW - 1, 53), KW['stone2'], 0.6)
    for row, y in enumerate((56, 60)):
        c.put(c.rect(0, y, AW - 1, y), KW['stone0'])
        for x in range((row * 7) % 15, AW, 15):
            c.put(c.rect(x, y - 3, x, y - 1), KW['stone0'])
    c.put(shaft & (c.y >= 52), K['teal3'], 0.4 * br)


def axe_labels(c, hx, hy, th, s=1.4):
    """Enemy war-axe whose head centre is at (hx, hy) on a swing about B_HANDS at angle `th` (screen radians).
    The edge faces the swing (towards the shield). Labels: 1 haft, 2 haft band, 3 socket, 4 cheek, 5 cheek light,
    6 edge, 7 edge light, 8 curse rune, 9 back spike."""
    dx_, dy_ = -math.cos(th), -math.sin(th)                     # head -> hands
    bx_, by_ = -math.sin(th), math.cos(th)                      # edge direction (motion when th grows)
    x, y = c.x - hx, c.y - hy
    p = x * bx_ + y * by_
    q = x * dx_ + y * dy_
    lab = np.zeros((c.h, c.w), int)
    lab[(q >= 2.0 * s) & (q <= 60) & (np.abs(p) <= 1.2)] = 1
    lab[(q >= 2.0 * s) & (q <= 4.5 * s) & (np.abs(p) <= 1.7)] = 2
    p, q = p / s, q / s
    lab[(p >= -2.5) & (p <= 1.5) & (np.abs(q) <= 2.6)] = 3
    cheek = (p > 1.5) & (np.abs(q) <= 2.6 + (p - 1.5) * 0.8) & (p <= 8.6 - q * q / 14)
    lab[cheek] = 4
    lab[cheek & (q < -0.5) & (p < 6)] = 5
    edge = cheek & (p > 7.0 - q * q / 14)
    lab[edge] = 6
    lab[edge & (q < 0.5)] = 7
    lab[cheek & (np.abs(p - 4.2) <= 0.5) & (np.abs(q - 0.5) <= 1.6) & ((np.floor(q + 10) % 2) == 0)] = 8
    lab[(p < -2.5) & (p >= -6.5) & (np.abs(q) <= (p + 6.5) / 4.0 * 1.4)] = 9
    return lab


def paint_axe(c, g, lab, a=1.0, curse=1.0):
    m = lab > 0
    c.put(outline_mask(m), K['ink'], a)
    cols = {1: KW['wood1'], 2: KW['iron3'], 3: KW['iron2'], 4: KW['iron2'], 5: KW['iron3'], 6: K['steel2'],
            7: K['steel4'], 8: K['violet3'], 9: KW['iron3']}
    for k, col in cols.items():
        c.put(lab == k, col, a)
    c.put((lab == 1) & (np.roll(lab, 1, axis=1) != 1), KW['wood2'], a)
    head = (lab >= 3)
    rim = head & ~shift(head, 0, 1)                                 # violet rim light on the top edges (enemy)
    c.put(rim & (lab != 7), K['violet2'], a)
    g.add(rim, L['violet'], 0.25 * a * curse)
    g.add(lab == 8, L['violet'], 0.8 * a * curse)
    g.add(lab == 7, L['violet'], 0.12 * a * curse)


def axe_angle(f):
    """None = out of the picture. Wind-up at the right edge (6-9), swing (10-11), clang (12), recoil (13-16)."""
    keys = {5: 62, 6: 66, 7: 70, 8: 69, 9: 66, 10: 75, 11: 82, 12: 88, 13: 83, 14: 77, 15: 71, 16: 65, 17: 60}
    return math.radians(keys[f]) if f in keys else None


def bloqueo_frame(f):
    t = f / FRAMES
    c, g = Canvas(AW, AH), Canvas(AW, AH)
    crypt_scene(c, g, t)
    hit = 12
    age = f - hit
    cx0, cy0 = B_C

    # the shield: braces towards the blow (8-11), is knocked back (12-14), rocks back into place
    push = {8: 0.5, 9: 1, 10: 1, 11: 1, 12: -3, 13: -3, 14: -2, 15: -1, 16: 0, 17: 0.5}.get(f, 0)
    sx, sy = {10: (1.03, 0.98), 11: (1.04, 0.97), 12: (0.93, 1.03), 13: (0.96, 1.02), 14: (1.02, 0.99)}.get(f, (1, 1))
    cx = cx0 + push
    cy = cy0 + (30 * 0.56) * (1 - sy)                               # keep the point on the floor when squashed
    # warn glow: the steel-blue guard outline grows while the blow comes (anticipation)
    lab = heater_labels(c, cx, cy, sx=sx, sy=sy)
    m = lab > 0
    warn = {7: 0.25, 8: 0.45, 9: 0.65, 10: 0.85, 11: 1.0}.get(f, 0.0)
    if warn > 0:
        ring = dilate(dilate(m)) & ~dilate(m)
        c.put(ring, KW['plate2'], 0.55 * warn)
        g.add(ring, LW['steel'], 0.5 * warn)
    # block pulse after the hit: two outlines of the shield expand outward (gaining Escudo)
    if 0 <= age <= 6:
        grown = m.copy()
        prev = m.copy()
        steps = int(2 + age * 1.6)
        for i in range(steps):
            prev = grown
            grown = dilate(grown, diagonal=i % 2 == 1)
        a = 1 - age / 7.0
        band = grown & ~prev
        c.put(band, KW['plate3'], a)
        g.add(band, LW['steel'], 0.9 * a)
        if age >= 2:
            inner = prev & ~erode(prev) & ~m
            c.put(inner, KW['plate1'], 0.6 * a)

    # floor: the point scrapes back, dust puffs at the base
    base_y = B_FLOOR
    if 0 <= age <= 7:
        k = 1 - age / 8.0
        sc = c.rect(int(cx0 - 3), base_y, int(cx0 + 1), base_y) & (c.x <= cx0 + 1)
        c.put(sc, KW['stone3'], k)
    dust_puffs(c, cx0 - 2, base_y - 1, f, hit, 4, 1.8, rise=0.35, life=7, seed=9, big=0.9)

    sh_ = c.disc(cx, base_y, 9) & (np.abs(c.y - base_y) <= 1.2)
    c.put(sh_, K['ink'], 0.55)
    c.put(c.disc(cx, base_y, 5.5) & (np.abs(c.y - base_y) <= 0.8), K['ink'], 0.9)
    paint_heater(c, g, lab, cx, cy, gem_gain=1.0 + 1.2 * warn + (1.5 if 0 <= age <= 2 else 0),
                 glint=(-26 + (f - 15) * 9 if 15 <= f <= 20 else (-26 + (f - 1) * 9 if 1 <= f <= 5 else None)),
                 flash={12: 0.55, 13: 0.2}.get(f, 0.0))

    # the axe
    th = axe_angle(f)
    if th is not None:
        hx = B_HANDS[0] + math.cos(th) * B_REACH
        hy = B_HANDS[1] + math.sin(th) * B_REACH
        a = 1.0 if 6 <= f <= 14 else {5: 0.5, 15: 0.75, 16: 0.45, 17: 0.2}[f]
        if f in (10, 11, 12):                                       # swing smear: fading after-images of the head
            prev = axe_angle(f - 1)
            for j, k_ in enumerate((0.3, 0.6)):
                ga = prev + (th - prev) * k_
                gl_ = axe_labels(c, B_HANDS[0] + math.cos(ga) * B_REACH, B_HANDS[1] + math.sin(ga) * B_REACH, ga)
                gm = gl_ >= 3
                al = (0.35 + 0.3 * j) * (0.6 if f == 12 else 1.0)
                c.put(gm & ~erode(gm), K['violet2'], al)
                c.put(erode(gm), K['violet0'], al * 0.6)
                g.add(gm & ~erode(gm), L['violet'], 0.35 * al)
        alab = axe_labels(c, hx, hy, th)
        paint_axe(c, g, alab, a=a, curse=1.0 if f <= 12 else a * 0.6)
        if f == 9:                                                  # menace gleam on the raised edge
            ex = hx - math.sin(th) * 11
            ey = hy + math.cos(th) * 11
            star4(c, g, ex, ey, 3, core=K['white'], col=K['violet3'], edge=K['violet2'], light=L['violet'], gain=1.0)

    # the clang: flash, star, impact lines, warm sparks that bounce on the floor, the curse shaken off
    hx_, hy_ = cx0 + 11.0, 30.0                                    # contact on the right rim
    if f == hit:
        c.put(c.disc(hx_, hy_, 6.5), KW['spark1'], 0.6)
        c.put(c.disc(hx_, hy_, 4.0), KW['spark2'])
        c.put(c.disc(hx_, hy_, 2.0), K['white'])
        g.add(c.disc(hx_, hy_, 7), L['warm'], 1.2)
    if hit <= f <= hit + 3:
        star4(c, g, hx_, hy_, {0: 12, 1: 8, 2: 5, 3: 2}[age], core=K['white'], col=KW['spark2'], edge=KW['spark1'],
              light=L['warm'], gain=1.4)
    if 0 <= age <= 3:                                               # clang lines radiating from the contact
        k = 1 - age / 4.0
        for j, a0 in enumerate((-0.9, -0.35, 0.25, 0.8, 1.4)):
            r0 = 5 + age * 3
            ln = 4 + j % 2 * 2
            x0, y0 = hx_ + math.cos(a0) * r0, hy_ - math.sin(a0) * r0
            m2 = c.line(x0, y0, x0 + math.cos(a0) * ln, y0 - math.sin(a0) * ln)
            c.put(m2, K['white'] if age == 0 else KW['spark2'], k)
            g.add(m2, L['warm'], 0.6 * k)
    falling_sparks(c, g, f, hit, hx_, hy_, 41, 16, (1.4, 3.2), 10, WARM_SPARKS, L['warm'], B_FLOOR - 1,
                   (-0.6, 1.9), gain=1.0)
    sparks(c, g, f, hit, hx_ + 2, hy_, 43, 7, (0.5, 1.3), 6, (K['violet4'], K['violet3'], K['violet2']), L['violet'],
           gravity=-0.05, gain=0.7)

    motes(c, g, t, 241, 9, (14, 4, 50, 50), K['teal3'], K['steel3'], L['blue'], gain=0.4)
    motes(c, g, t, 251, 5, (2, 6, 86, 56), K['cyan3'], K['steel3'], L['cyan'], gain=0.5)
    vignette(c, 0.75)
    if f == hit:
        shake(c, g, -1, 0)
    elif f == hit + 1:
        shake(c, g, 1, 0)
    return c, g


# ---------------------------------------------------------------------------------------- Guardia Férrea
G_C = (44.0, 36.0)            # centre of the bulwark of plates
G_SHIELD = (44.0, 41.0)
G_GUARD_Y = 21.0              # the planted sword's crossguard
G_FLOOR = 56
G_PLATES = 8
G_LOCK_R = 21.0
G_SPAN = math.radians(224)    # the plates cover an arch open at the floor


def chapel_scene(c, g, t):
    """Violet night over a ruined chapel on a hill, the moon high on the right, a round stone dais in front."""
    gradient_bg(c, K['vio1'], K['vio2'], K['vio0'], 0.6)
    c.put(c.disc(44, 32, 32), K['vio3'], 0.22)                    # dithered glow behind the bulwark
    c.put(c.disc(44, 32, 20), K['vio3'], 0.38)
    mx, my = 76.5, 11.5
    c.put(c.disc(mx, my, 10), K['vio3'], 0.45)
    moon = c.disc(mx, my, 5.0) & ~c.disc(mx + 2.6, my - 1.6, 4.3)
    c.put(moon, K['moon1'])
    c.put(moon & ~c.disc(mx - 0.8, my + 0.8, 4.3), K['moon2'])
    g.add(moon, L['blue'], 0.25)
    twinkling_stars(c, g, t, 271, 16, (2, 2, 86, 36), avoid=(44, 32, 25), light=L['violet'])
    hills = c.poly([(0, 47), (9, 43), (18, 45), (27, 41), (37, 44), (50, 42), (61, 45), (70, 40), (79, 43),
                    (88, 41), (88, 64), (0, 64)])
    c.put(hills, K['vio0'])
    c.put(hills & ~np.roll(hills, 1, axis=0), K['moon0'], 0.5)
    # the ruined chapel far away on the right hill: gable, broken tower, one warm window
    ch = c.poly([(66, 41), (66, 35), (70, 31), (74, 35), (74, 41)]) | c.rect(75, 29, 78, 41) | c.rect(76, 27, 77, 29)
    c.put(ch, K['roof'])
    c.put(outline_mask(ch) & ~ch & (c.y < 41), K['roof2'])
    lit = 0.5 + 0.5 * math.sin(t * 4 * math.pi + 1.1)
    win = c.rect(70, 36, 70, 37)
    c.put(win, K['window'] if lit > 0.3 else K['window2'])
    g.add(win, L['warm'], 0.3 * lit)
    # dais and floor
    floor = c.rect(0, 52, AW - 1, AH - 1)
    c.put(floor, K['vio0'])
    c.put(c.rect(0, 52, AW - 1, 52), K['vio3'], 0.6)
    dais = (np.hypot((c.x - 44) / 31.0, (c.y - G_FLOOR) / 5.0) <= 1)
    c.put(dais, KW['stone1'])
    c.put(dais & (c.y < G_FLOOR - 1.5), KW['stone2'])
    c.put(dais & ~shift(dais, 0, 1), KW['stone3'])
    lip = (np.hypot((c.x - 44) / 31.0, (c.y - G_FLOOR - 2) / 5.0) <= 1) & ~dais & (c.y > G_FLOOR)
    c.put(lip, KW['stone0'])
    c.put(outline_mask(dais | lip) & (c.y > G_FLOOR - 3), K['ink'])


def plate_state(f, t):
    """(radius, alpha, wobble) of the plates: orbit (0-6), pulled out (7-10), slam (11), locked (12-16),
    crumble (17-19), re-form (20-23)."""
    if f <= 6:
        return 26 + 0.8 * math.sin(t * 2 * math.pi * 2), 0.75, 1.0
    if f <= 10:
        return {7: 27.0, 8: 28.5, 9: 29.5, 10: 30.0}[f], {7: 0.85}.get(f, 1.0), 1.0 - (f - 6) / 4.0
    if f == 11:
        return 23.5, 1.0, 0.0
    if f <= 16:
        return G_LOCK_R, 1.0, 0.0
    if f <= 19:
        return G_LOCK_R + (f - 16) * 0.6, {17: 0.75, 18: 0.5, 19: 0.25}[f], 0.0
    return 26 + 0.8 * math.sin(t * 2 * math.pi * 2), {20: 0.15, 21: 0.35, 22: 0.55, 23: 0.7}[f], (f - 19) / 4.0


def plate_angles(f, t, wob):
    """Plate centre angles (y-up radians) from the left floor round the top to the right floor."""
    out = []
    for i in range(G_PLATES):
        base = math.pi / 2 + G_SPAN / 2 - i * G_SPAN / (G_PLATES - 1)
        out.append(base + wob * math.radians(6) * math.sin(t * 2 * math.pi + i * 0.8))
    return out


def plate_mask(c, cx, cy, r, th, half_len, thick=2.1):
    """One curved iron plate: an annulus sector centred at angle th (y-up) on radius r."""
    rr = np.hypot(c.x - cx, c.y - cy)
    ang = np.arctan2(-(c.y - cy), c.x - cx)
    d = np.abs((ang - th + math.pi) % (2 * math.pi) - math.pi)
    return (np.abs(rr - r) <= thick) & (d * r <= half_len)


def guardia_frame(f):
    t = f / FRAMES
    c, g = Canvas(AW, AH), Canvas(AW, AH)
    chapel_scene(c, g, t)
    lock = 12
    age = f - lock
    cx, cy = G_C
    r, alpha, wob = plate_state(f, t)
    angs = plate_angles(f, t, wob)
    held = 0 <= age <= 4                                          # the bulwark is up: Aguante
    fury = 1.0 if 15 <= f <= 19 else 0.0

    # rune ring on the dais (turns once per loop; brightens while charging, flares on the lock)
    flare = max(0.0, 1 - abs(age) / 4.0) if -4 <= age <= 4 else 0.0
    ring = ellipse_ring(c, cx, G_FLOOR, 22, 3.2, 0.5)
    c.put(ring, KW['plate1'], 0.5 + 0.5 * flare)
    g.add(ring, LW['steel'], 0.12 + 0.5 * flare)
    for i in range(14):
        th = 2 * math.pi * (i / 14 + t)
        m = c.pixel(cx + math.cos(th) * 22, G_FLOOR + math.sin(th) * 3.2)
        c.put(m, KW['plate3'] if i % 7 == 0 else KW['plate2'])
        g.add(m, LW['steel'], 0.3 + 0.7 * flare)

    # the planted sword behind the shield, then the shield
    sx_, sy_ = G_SHIELD
    lab, u, v = sword_labels(c, sx_, G_GUARD_Y, math.pi / 2)
    rune_t = t * 2 + (1.0 if 7 <= f <= 12 else 0)
    paint_sword(c, g, lab, u, rune_t, glint_u=(-13 + (f - 1) * 9 if 1 <= f <= 5 else None),
                rune_gain=1.0 + (1.2 if fury else 0) + (0.6 if 8 <= f <= 12 else 0), fury=fury)
    sh_ = c.disc(sx_, G_FLOOR - 0.5, 9) & (np.abs(c.y - G_FLOOR + 0.5) <= 1.2)
    c.put(sh_, K['ink'], 0.6)
    slab = heater_labels(c, sx_, sy_, W=10.0, H=26.0)
    gem_gain = 1.0 + (1.5 if 8 <= f <= 13 else 0)
    paint_heater(c, g, slab, sx_, sy_, gem_gain=gem_gain, flash={12: 0.25}.get(f, 0.0))

    # the plates: speed streaks on the slam, locked arch with seams that flash
    half = (G_SPAN / (G_PLATES - 1)) * r / 2 * (1.0 if age >= 0 and f <= 19 else 0.82 if f == 11 else 0.6)
    if f == 11:
        for th in angs:
            for k_ in (1.0, 2.0):
                ox, oy = math.cos(th) * (r + 3 + 2.5 * k_), -math.sin(th) * (r + 3 + 2.5 * k_)
                m = c.line(cx + ox, cy + oy, cx + ox * 1.12, cy + oy * 1.12)
                c.put(m, KW['plate3'], 0.7 / k_)
                g.add(m, LW['steel'], 0.4 / k_)
    allp = np.zeros((c.h, c.w), bool)
    for i, th in enumerate(angs):
        pm = plate_mask(c, cx, cy, r, th, half)
        allp |= pm
        outer = pm & (np.hypot(c.x - cx, c.y - cy) > r + 1.0)
        inner = pm & (np.hypot(c.x - cx, c.y - cy) < r - 1.0)
        c.put(outline_mask(pm), K['ink'], alpha)
        c.put(pm, KW['plate1'], alpha)
        c.put(outer, KW['plate3'] if th > 0.3 else KW['plate2'], alpha)
        c.put(inner, KW['plate0'], alpha)
        mid = c.pixel(cx + math.cos(th) * r, cy - math.sin(th) * r)
        c.put(mid, K['cyan3'] if held or 8 <= f <= 11 else KW['plate3'], alpha)
        g.add(mid, L['cyan'], 0.6 * alpha)
        g.add(pm, LW['steel'], (0.1 + (0.18 if held else 0) + (0.18 if 8 <= f <= 11 else 0)) * alpha)
    if 0 <= age <= 3:                                              # seams light up where the plates meet
        k = 1 - age / 4.0
        for i in range(G_PLATES - 1):
            th = (angs[i] + angs[i + 1]) / 2
            for dr in (-1, 0, 1):
                m = c.pixel(cx + math.cos(th) * (r + dr), cy - math.sin(th) * (r + dr))
                c.put(m, K['white'] if age < 2 else K['cyan3'], k)
                g.add(m, L['cyan'], 1.0 * k)
    if 0 <= age <= 5:                                              # the lock rings out
        rad = r + 2 + age * 3.0
        a = 1 - age / 6.0
        band = (np.abs(np.hypot(c.x - cx, c.y - cy) - rad) <= 0.6) & (c.y < G_FLOOR - 1)
        c.put(band, KW['plate3'], a * 0.8)
        g.add(band, LW['steel'], 0.6 * a)
    if f == lock:
        star4(c, g, cx, cy - r, 9, core=K['white'], col=KW['plate3'], edge=KW['plate2'], light=LW['steel'], gain=1.3)
    elif f == lock + 1:
        star4(c, g, cx, cy - r, 5, core=K['white'], col=KW['plate3'], edge=KW['plate2'], light=LW['steel'], gain=1.0)
    dust_puffs(c, cx - 16, G_FLOOR - 1, f, lock, 2, 1.6, rise=0.3, life=6, seed=17, big=0.8)
    dust_puffs(c, cx + 16, G_FLOOR - 1, f, lock, 2, 1.6, rise=0.3, life=6, seed=19, big=0.8)

    # charging: motes drawn into the plates
    if 7 <= f <= 10:
        rng = np.random.default_rng(281)
        k = (f - 7) / 3.0
        for i in range(10):
            th = rng.uniform(-0.3, math.pi + 0.3)
            rr = (r + 10) - 8 * ease_in_out(k) + rng.uniform(-2, 2)
            m = c.pixel(cx + math.cos(th) * rr, cy - math.sin(th) * rr)
            c.put(m, KW['plate3'] if i % 3 else K['cyan3'])
            g.add(m, LW['steel'], 0.8)

    # Aguante: with the guard up, a gold pulse climbs from the shield into the hilt and the runes turn crimson
    gx_, gy_ = sx_, sy_ - 3.5                                      # shield gem
    if f == 13:
        star4(c, g, gx_, gy_, 5, core=K['white'], col=KW['gold3'], edge=KW['gold2'], light=LW['gold'], gain=1.3)
    if 13 <= f <= 15:
        top = {13: gy_ - 2, 14: G_GUARD_Y + 1, 15: G_GUARD_Y - 6}[f]
        beam = c.rect(int(sx_), int(top), int(sx_), int(gy_)) & ~(slab == 6) & ~(slab == 7)
        c.put(beam, KW['gold3'], 0.9)
        g.add(beam, LW['gold'], 0.9)
        g.add(dilate(beam), LW['gold'], 0.3)
    if f == 15:                                                    # the hilt flares crimson: Fuerza
        star4(c, g, sx_, G_GUARD_Y - 1, 8, core=K['white'], col=KW['crim5'], edge=KW['crim4'],
              light=LW['crimson'], gain=1.5)
    elif f == 16:
        star4(c, g, sx_, G_GUARD_Y - 1, 4, core=KW['crim5'], col=KW['crim4'], edge=KW['crim3'],
              light=LW['crimson'], gain=1.0)
    if 15 <= f <= 19:                                              # the hilt and the visible blade glow red
        k = {15: 1.0, 16: 0.8, 17: 0.6, 18: 0.4, 19: 0.2}[f]
        hm = (lab > 0)
        g.add(dilate(hm) & ~hm & (c.y < sy_ - 10), LW['crimson'], 0.5 * k)
    for side, born in ((-1, 15), (1, 16)):                         # Fuerza: crimson chevrons rising by the hilt
        rising_chevrons(c, g, f, sx_ + side * 10, G_GUARD_Y + 2, born, 5, KW['crim5'], LW['crimson'], count=2,
                        gain=0.9)
    sparks(c, g, f, 15, sx_, G_GUARD_Y - 6, 283, 8, (0.5, 1.2), 7, (KW['crim5'], KW['crim4'], KW['crim2']),
           LW['crimson'], gravity=-0.06, gain=0.8)

    # crumble: the plates shed steel motes as they fade
    if 17 <= f <= 21:
        rng = np.random.default_rng(293)
        a_ = f - 16
        for i in range(16):
            th = angs[i % G_PLATES] + rng.uniform(-0.15, 0.15)
            rr = G_LOCK_R + rng.uniform(-1.5, 1.5)
            x = cx + math.cos(th) * rr + rng.uniform(-1, 1) * a_
            y = cy - math.sin(th) * rr - a_ * rng.uniform(0.6, 1.4)
            k = 1 - a_ / 6.0
            m = c.pixel(x, y)
            c.put(m, KW['plate3'] if i % 3 else K['cyan3'], k)
            g.add(m, LW['steel'], 0.7 * k)

    motes(c, g, t, 301, 8, (8, 6, 80, 52), KW['plate3'], K['cyan3'], LW['steel'], gain=0.5)
    vignette(c, 0.72)
    if f == lock:
        shake(c, g, 0, 1)
    return c, g


ILLUSTRATIONS = {
    'espadazo': espadazo_frame,
    'bloqueo': bloqueo_frame,
    'guardia_ferrea': guardia_frame,
}


def render(card_id):
    fn = ILLUSTRATIONS[card_id]
    base, glow = [], []
    for f in range(FRAMES):
        b, gl = fn(f)
        base.append(b)
        glow.append(gl)
    return base, glow
