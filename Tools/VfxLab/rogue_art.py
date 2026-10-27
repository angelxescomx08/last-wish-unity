"""Animated pixel-art illustrations for the rogue's basic cards (88×64, 24 frames at 12 fps, seamless loop).

Each frame has a base image (opaque pixel art, readable without bloom) and an emissive light map that
Unity adds on top with an HDR additive material, so only the magical parts bloom.
"""
import math

import numpy as np

from pixelkit import Canvas, hexc, mix, dilate, erode, shift

AW, AH = 88, 64
FRAMES = 24

K = {
    'ink': hexc('#07050c'),
    'night0': hexc('#0b0817'), 'night1': hexc('#120d24'), 'night2': hexc('#1b1436'), 'night3': hexc('#271d4a'),
    'teal0': hexc('#070d16'), 'teal1': hexc('#0c1726'), 'teal2': hexc('#11243a'), 'teal3': hexc('#1a3550'),
    'vio0': hexc('#0e0718'), 'vio1': hexc('#1a0c2c'), 'vio2': hexc('#281242'), 'vio3': hexc('#3a1a5c'),
    'moon0': hexc('#6f63b8'), 'moon1': hexc('#a69be6'), 'moon2': hexc('#e2dcff'),
    'roof': hexc('#06040b'), 'roof2': hexc('#0d0a17'), 'window': hexc('#f2b35e'), 'window2': hexc('#a8603a'),
    'steel0': hexc('#3d435c'), 'steel1': hexc('#646d8c'), 'steel2': hexc('#98a2c0'), 'steel3': hexc('#cdd5ec'), 'steel4': hexc('#f4f7ff'),
    'gold1': hexc('#8a6a3a'),
    'grip0': hexc('#2a1638'), 'grip1': hexc('#4a2a66'), 'grip2': hexc('#6e4493'),
    'guard0': hexc('#4b3f78'), 'guard1': hexc('#7a6cb0'), 'guard2': hexc('#b6abe0'),
    'cyan0': hexc('#0d3550'), 'cyan1': hexc('#137ea3'), 'cyan2': hexc('#2fc4e6'), 'cyan3': hexc('#8ff1ff'), 'cyan4': hexc('#e6feff'),
    'violet0': hexc('#2d1346'), 'violet1': hexc('#55248a'), 'violet2': hexc('#8a47cf'), 'violet3': hexc('#c79bff'), 'violet4': hexc('#f0e0ff'),
    'body0': hexc('#0c0814'), 'body1': hexc('#171025'), 'body2': hexc('#241838'),
    'rimc': hexc('#58dcff'), 'rimv': hexc('#3a2163'),
    'scarf0': hexc('#3a1a5e'), 'scarf1': hexc('#5d2a92'), 'scarf2': hexc('#8f50d0'),
    'dust0': hexc('#2b2540'), 'dust1': hexc('#4a4066'),
    'white': hexc('#ffffff'),
}
# emissive colours (light, not paint)
L = {
    'cyan': np.array([0.25, 0.85, 1.0, 1]), 'white': np.array([1.0, 1.0, 1.0, 1]),
    'violet': np.array([0.6, 0.3, 1.0, 1]), 'blue': np.array([0.2, 0.45, 1.0, 1]),
    'warm': np.array([1.0, 0.6, 0.25, 1]),
}


def ease_out(t):
    return 1 - (1 - t) ** 3


def ease_in_out(t):
    return t * t * (3 - 2 * t)


def frac(x):
    return x - math.floor(x)


# ---------------------------------------------------------------------------------------- shared bits
def gradient_bg(c, top, mid, bot, split=0.55):
    t = c.y / c.h
    c.put(np.ones((c.h, c.w), bool), top)
    c.put(t > split * 0.45, mid, np.clip((t - split * 0.45) * 4, 0, 1))
    c.put(t > split, bot, np.clip((t - split) * 3.2, 0, 1))


def vignette(c, strength=0.75):
    d = np.hypot((c.x - c.w / 2) / (c.w / 2), (c.y - c.h / 2) / (c.h / 2))
    c.put(np.ones((c.h, c.w), bool), K['ink'], np.clip((d - 0.78) * 1.6, 0, 1) * strength)


def star4(c, g, x, y, arm, core=K['white'], col=K['cyan3'], edge=K['cyan2'], light=L['cyan'], gain=1.0, diag=True):
    """4-point pixel star (centre white, arms fading), with matching emissive."""
    x, y = int(x), int(y)
    m_core = c.pixel(x + 0.5, y + 0.5)
    for i in range(1, arm + 1):
        k = i / (arm + 0.5)
        colr = col if k < 0.55 else edge
        for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            m = c.pixel(x + dx * i + 0.5, y + dy * i + 0.5)
            c.put(m, colr)
            g.add(m, light, gain * (1 - k) * 0.9)
    if diag and arm >= 3:
        for dx, dy in ((1, 1), (1, -1), (-1, 1), (-1, -1)):
            for i in range(1, max(1, arm // 3) + 1):
                m = c.pixel(x + dx * i + 0.5, y + dy * i + 0.5)
                c.put(m, edge)
                g.add(m, light, gain * 0.35)
    c.put(m_core, core)
    g.add(m_core, L['white'], gain * 1.2)


def motes(c, g, t, seed, count, area, col_a, col_b, light, rise=1.0, gain=0.8):
    """Looping rising motes: each completes an integer number of cycles per loop, so the loop is seamless."""
    rng = np.random.default_rng(seed)
    x0, y0, x1, y1 = area
    for i in range(count):
        px = rng.uniform(x0, x1)
        phase = rng.random()
        cycles = 1 if rng.random() < 0.7 else 2
        wob = rng.uniform(1.0, 3.0)
        u = frac(t * cycles * rise + phase)
        y = y1 - u * (y1 - y0)
        x = px + math.sin((u * 2 + phase) * math.pi * 2) * wob
        a = math.sin(u * math.pi)
        if a < 0.15:
            continue
        col = col_a if i % 3 else col_b
        m = c.pixel(x, y)
        c.put(m, col, min(1.0, a * 1.3))
        g.add(m, light, gain * a)
        if a > 0.85 and i % 4 == 0:            # brightest ones become tiny crosses
            for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                mm = c.pixel(x + dx, y + dy)
                c.put(mm, col, 0.6)
                g.add(mm, light, gain * 0.3)


def outline_mask(m):
    return dilate(m) & ~m


# ---------------------------------------------------------------------------------------- dagger
def dagger_labels(c, cx, cy, ang, s=1.0, tip=40.0, wb0=4.4):
    """Rasterise a dagger whose guard centre is at (cx, cy), pointing at angle `ang` (radians, screen space).
    Returns an int label map (0 = empty) and the local (u, v) coordinates."""
    ux, uy = math.cos(ang), math.sin(ang)
    vx, vy = -uy, ux
    dx, dy = c.x - cx, c.y - cy
    u = (dx * ux + dy * uy) / s
    v = (dx * vx + dy * vy) / s
    lab = np.zeros((c.h, c.w), int)
    brk = tip * 0.5
    wbk = wb0 - brk * 0.02
    wb = np.where(u < brk, wb0 - u * 0.02, wbk * np.clip((tip - u) / (tip - brk), 0, None) ** 0.85)
    blade = (u >= 0) & (u <= tip) & (np.abs(v) <= wb)
    lab[blade] = 2
    lab[blade & (v > 0)] = 3
    lab[blade & (v <= -wb + 1.1)] = 1
    lab[blade & (v >= wb - 1.0)] = 4
    lab[blade & (np.abs(v) <= 0.7) & (u > 2) & (u < tip * 0.72)] = 5     # fuller (runes)
    av = np.abs(v)
    sh = np.clip(av - 5.0, 0, None) * 0.9
    guard = (av <= 7.8) & (u >= -3.2 + sh) & (u < 0.2 + sh)
    lab[guard] = 6
    lab[guard & (v > 0.5)] = 7
    lab[(av <= 1.4) & (u >= -2.8) & (u < -0.2)] = 10                  # guard gem
    grip = (u >= -14.5) & (u < -3.2) & (av <= 2.15)
    lab[grip] = 8
    lab[grip & ((np.floor((u + 14.5) / 2.5) % 2) == 0)] = 9
    pd = np.hypot(u + 17.2, v)
    lab[pd <= 3.0] = 11
    lab[(pd <= 3.0) & (v < -0.6) & (u < -16.6)] = 12
    lab[pd <= 1.2] = 10
    return lab, u, v


def paint_dagger(c, g, lab, u, t_rune, glint_u=None, rune_gain=1.0, ghost=None):
    """Paint dagger labels. ghost=(colour, alpha) paints a translucent coloured copy instead of steel."""
    m = lab > 0
    if ghost is not None:
        col, a, light = ghost
        edge = outline_mask(m)
        c.put(edge, col, a)
        c.put(m, K['violet1'], a * 0.3)
        c.put(m & (lab == 1), K['violet4'], a * 0.8)
        g.add(edge, light, a * 0.5)
        return
    c.put(outline_mask(m), K['ink'])
    colors = {1: K['steel4'], 2: K['steel2'], 3: K['steel1'], 4: K['steel0'], 6: K['guard2'], 7: K['guard0'],
              8: K['grip0'], 9: K['grip1'], 11: K['guard1'], 12: K['guard2']}
    for k, col in colors.items():
        c.put(lab == k, col)
    # runes in the fuller: a light wave travels from guard to tip
    fl = lab == 5
    wave = 0.5 + 0.5 * np.sin((u / 9.0 - t_rune) * math.pi * 2)
    pat = (np.floor(u) % 3) != 2
    c.put(fl, K['cyan0'])
    c.put(fl & pat, K['cyan1'])
    c.put(fl & pat & (wave > 0.55), K['cyan2'])
    c.put(fl & pat & (wave > 0.85), K['cyan3'])
    g.add(fl & pat, L['cyan'], rune_gain * (0.25 + 0.75 * wave))
    gem_m = lab == 10
    c.put(gem_m, K['cyan3'])
    g.add(gem_m, L['cyan'], 0.9 * rune_gain)
    if glint_u is not None:
        gl = (lab == 1) & (np.abs(u - glint_u) <= 2.6)
        c.put(gl, K['white'])
        g.add(gl, L['white'], 0.8)
        g.add((lab == 2) & (np.abs(u - glint_u) <= 1.2), L['cyan'], 0.4)


def dagger_tip(cx, cy, ang, s=1.0, tip=40.0):
    return cx + math.cos(ang) * tip * s, cy + math.sin(ang) * tip * s


# ---------------------------------------------------------------------------------------- Puñalada
def blade_shimmer(c, g, f, cx, cy, ang, seed, count=6, tip=40.0, light=None, col=None):
    """Sparkles that peel off the blade edge and drift away (looping: each lives 5 frames)."""
    light = L['cyan'] if light is None else light
    col = K['cyan3'] if col is None else col
    rng = np.random.default_rng(seed)
    ux, uy = math.cos(ang), math.sin(ang)
    vx, vy = -uy, ux
    for i in range(count):
        born = int(rng.integers(0, FRAMES))
        uu = rng.uniform(6, tip - 6)
        side = -1 if rng.random() < 0.6 else 1
        age = (f - born) % FRAMES
        if age > 5:
            continue
        drift = 1.0 + age * 1.2
        px = cx + ux * uu + vx * side * (4 + drift)
        py = cy + uy * uu + vy * side * (4 + drift) - age * 0.6
        a = 1 - age / 6
        m = c.pixel(px, py)
        c.put(m, K['white'] if age < 2 else col)
        g.add(m, light, 1.0 * a)
        if age < 2:
            for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                mm = c.pixel(px + dx, py + dy)
                c.put(mm, col, 0.8)
                g.add(mm, light, 0.35)


def punalada_frame(f):
    t = f / FRAMES
    c, g = Canvas(AW, AH), Canvas(AW, AH)
    gradient_bg(c, K['night0'], K['night2'], K['night1'], 0.62)
    # moon (upper left) with a soft dithered halo
    mx, my = 17.5, 14.5
    c.put(c.disc(mx, my, 15), K['night3'], 0.35)
    c.put(c.disc(mx, my, 11.5), K['night3'], 0.75)
    moon = c.disc(mx, my, 8.5) & ~c.disc(mx + 4.2, my - 2.2, 7.6)
    c.put(moon, K['moon1'])
    c.put(moon & ~c.disc(mx - 1.2, my + 1.2, 7.6), K['moon2'])
    c.put(moon & c.disc(mx - 2.8, my + 3.4, 3.0), K['moon0'])
    rng = np.random.default_rng(11)
    for i in range(18):
        sx, sy = rng.uniform(2, 86), rng.uniform(2, 34)
        ph = rng.random()
        if math.hypot(sx - mx, sy - my) < 13:
            continue
        b = 0.5 + 0.5 * math.sin((t * (1 + i % 3) + ph) * math.pi * 2)
        m = c.pixel(sx, sy)
        if b > 0.25:
            c.put(m, K['moon1'] if b < 0.8 else K['white'])
            g.add(m, L['blue'], 0.3 * b)
        if b > 0.9 and i % 3 == 0:
            for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                mm = c.pixel(sx + dx, sy + dy)
                c.put(mm, K['moon0'])
                g.add(mm, L['blue'], 0.15)
    roof = c.poly([(0, 52), (8, 52), (8, 47), (14, 43), (20, 47), (20, 50), (27, 50), (27, 45), (31, 45), (31, 41),
                   (34, 41), (34, 45), (40, 45), (47, 39), (54, 45), (54, 49), (62, 49), (62, 44), (70, 44), (70, 48),
                   (77, 48), (81, 44), (88, 44), (88, 64), (0, 64)])
    c.put(roof, K['roof'])
    c.put(outline_mask(roof) & (c.y < 60), K['roof2'])
    for i, (wx, wy) in enumerate([(11, 50), (16, 52), (36, 48), (44, 47), (49, 51), (65, 47), (73, 52), (84, 49)]):
        lit = 0.5 + 0.5 * math.sin((t * (1 + i % 2) + i * 0.37) * math.pi * 2)
        m = c.rect(wx, wy, wx, wy + 1)
        c.put(m, K['window'] if lit > 0.3 else K['window2'])
        g.add(m, L['warm'], 0.25 * lit)

    # dagger motion: hover bob, wind-up (anticipation), thrust (impact), recover (dissipation)
    ang = -math.atan2(1, 2)
    gx, gy = 34.0, 43.0
    bob = math.sin(t * 2 * math.pi * 2) * 0.8
    if f <= 11:
        along = 0.0
    elif f <= 14:
        along = [-3, -6, -7][f - 12]
    elif f <= 17:
        along = [10, 9, 7][f - 15]
    else:
        along = 7 * (1 - ease_in_out((f - 17) / 6.0))
    ux, uy = math.cos(ang), math.sin(ang)
    vx, vy = -uy, ux
    cx = round(gx + ux * along)
    cy = round(gy + uy * along + (bob if f <= 11 or f >= 20 else 0))

    # speed lines hugging the blade during the thrust
    if 15 <= f <= 18:
        k = {15: 1.0, 16: 0.75, 17: 0.45, 18: 0.2}[f]
        for i, off in enumerate((-10, -7, 7, 10, -13, 13)):
            start = 18 - (i * 7) % 15
            length = (9 + (i * 5) % 8) * (0.6 + 0.4 * k)
            sx, sy = cx + ux * start + vx * off, cy + uy * start + vy * off
            m = c.line(sx, sy, sx - ux * length, sy - uy * length)
            c.put(m, K['cyan3'] if i % 2 == 0 else K['steel3'], k)
            g.add(m, L['cyan'], 0.6 * k)

    lab, u, v = dagger_labels(c, cx, cy, ang)
    glint = -2 + (f - 18) * 10.5 if 18 <= f <= 22 else None
    charge = 13 <= f <= 17
    paint_dagger(c, g, lab, u, t * 2 + (1.5 if charge else 0), glint, rune_gain=1.8 if charge else 1.0)
    blade_shimmer(c, g, f, cx, cy, ang, 77, count=7)

    tx, ty = dagger_tip(cx, cy, ang)
    if f == 14:
        star4(c, g, tx + 1, ty, 2, gain=0.7)
    elif 15 <= f <= 18:
        star4(c, g, tx + 1, ty, {15: 8, 16: 7, 17: 4, 18: 2}[f], gain=1.4)
    rng = np.random.default_rng(5)
    for i in range(12):
        a0 = ang + rng.uniform(-1.4, 1.4)
        spd = rng.uniform(1.6, 3.4)
        age = f - 15
        if 0 < age <= 7:
            px = tx + math.cos(a0) * spd * age
            py = ty + math.sin(a0) * spd * age + 0.12 * age * age
            a = 1 - age / 8
            m = c.pixel(px, py)
            c.put(m, K['cyan4'] if age < 3 else K['cyan2'])
            g.add(m, L['cyan'], a)
            if age < 4:
                m2 = c.line(px, py, px - math.cos(a0) * spd * 0.9, py - math.sin(a0) * spd * 0.9)
                c.put(m2 & ~m, K['cyan1'], a)
                g.add(m2, L['cyan'], 0.3 * a)

    motes(c, g, t, 21, 10, (2, 8, 86, 62), K['cyan3'], K['violet3'], L['cyan'], gain=0.7)
    vignette(c, 0.7)
    return c, g


# ---------------------------------------------------------------------------------------- Esquiva
def figure_parts(c, ax, ay, t, flip=False, lean=0.0):
    """Hooded rogue (chibi proportions) in a dodging lunge, facing right (or left when flipped).
    Returns (body mask, scarf mask, face mask, eye pixels, blade segments, belt mask)."""
    s = -1 if flip else 1

    def P(pts):
        return [(ax + s * (x - lean * y * 0.06), ay + y) for x, y in pts]

    def w(k, a=1.0):
        return math.sin((t * 2 + k * 0.16) * math.pi * 2) * a

    hood_c = (ax + s * (3 + lean * 32 * 0.06), ay - 32)
    body = c.disc(hood_c[0], hood_c[1], 8.2)
    parts = [
        [(-3, -38), (-11, -35), (-4, -29)],                                   # hood tip
        [(-4, -28), (9, -28), (9, -23), (-4, -23)],                           # neck drape
        [(-3, -25), (8, -25), (9, -20), (7, -13), (-4, -13), (-5, -19)],      # torso
        [(-2, -27), (-7, -25), (-13 + w(1), -20), (-17 + w(2, 1.3), -13), (-21 + w(3, 1.8), -5 + w(4, 0.8)),
         (-15 + w(5, 1.4), -7), (-11 + w(6), -4), (-8, -9), (-4, -13)],      # cloak
        [(5, -25), (9, -25), (17, -21), (18, -17), (14, -16), (6, -20)],     # front arm
        [(-1, -24), (2, -22), (-4, -16), (-7, -13), (-10, -15), (-5, -20)],  # back arm
        [(1, -15), (7, -15), (12, -9), (8, -6)],                              # front thigh
        [(8, -9), (12, -9), (13, -2), (9, -2)],                               # front shin
        [(8, -4), (14, -4), (16, 0), (8, 0)],                                 # front boot
        [(-4, -15), (2, -14), (-4, -7), (-8, -9)],                            # back thigh
        [(-8, -9), (-4, -7), (-11, -2), (-14, -4)],                           # back shin
        [(-17, -4), (-11, -3), (-11, 0), (-18, 0)],                           # back boot
    ]
    for pts in parts:
        body |= c.poly(P(pts))
    body |= c.disc(ax + s * 16.5, ay - 18, 2.0) | c.disc(ax + s * -8.5, ay - 14.5, 1.8)
    scarf = c.poly(P([(3, -28), (-3, -28 + w(1, 0.5)), (-9, -29 + w(2)), (-15, -28 + w(3, 1.5)), (-21, -30 + w(4, 2)),
                      (-26, -28 + w(5, 2.5)), (-21, -26 + w(5, 2)), (-15, -25 + w(4, 1.5)), (-9, -25 + w(3)),
                      (-3, -24), (3, -24)]))
    face = c.disc(ax + s * (6.5 + lean * 31.5 * 0.06), ay - 31.5, 4.3) & c.disc(hood_c[0], hood_c[1], 7.6)
    ex = lean * 32 * 0.06
    eyes = [(ax + s * (5 + ex) + 0.5 * (s < 0) - 0.5 * (s < 0), ay - 32), (ax + s * (5 + ex), ay - 31),
            (ax + s * (8 + ex), ay - 32), (ax + s * (8 + ex), ay - 31)]
    blades = [((ax + s * 18, ay - 19), (ax + s * 26, ay - 23)), ((ax + s * 18, ay - 18), (ax + s * 26, ay - 22)),
              ((ax + s * -9, ay - 13), (ax + s * -12, ay - 7))]
    belt = c.poly(P([(-4, -15), (7, -15), (7, -13), (-4, -13)]))
    return body, scarf, face, eyes, blades, belt


def paint_figure(c, g, parts, flip=False):
    body, sc, face, eyes, blades, belt = parts
    allm = body | sc
    c.put(outline_mask(allm), K['ink'])
    rows = np.where(body.any(axis=1))[0]
    y0, y1 = (rows.min(), rows.max()) if len(rows) else (0, c.h)
    k = (c.y - y0) / max(1, y1 - y0)
    c.put(body, K['body2'])
    c.put(body, K['body1'], np.clip((k - 0.25) * 2.2, 0, 1))
    c.put(belt & body, hexc('#3a2a55'))
    # rim light: cyan on the right/top silhouette edges, a dithered violet bounce on the left edges
    light = 1
    rim_c = body & (~shift(body, -light, 0) | ~shift(body, 0, 1))
    rim_v = body & ~shift(body, light, 0) & ~rim_c
    c.put(rim_v, K['violet1'], 0.7)
    c.put(rim_c, K['rimc'])
    g.add(rim_c, L['cyan'], 0.5)
    c.put(sc, K['scarf1'])
    c.put(sc & ~shift(sc, 0, 1), K['scarf2'])
    c.put(sc & ~shift(sc, 0, -1), K['scarf0'])
    c.put(face & body, K['body0'])
    for (x, y) in eyes:
        m = c.pixel(x, y)
        c.put(m, K['cyan4'])
        g.add(m, L['cyan'], 1.4)
    for (p0, p1) in blades:
        m = c.line(p0[0], p0[1], p1[0], p1[1])
        c.put(m, K['steel3'])
        g.add(m, L['cyan'], 0.15)
    for (p0, p1) in blades[1:2] + blades[2:]:
        tip = c.pixel(p1[0] + 0.5, p1[1] + 0.5)
        c.put(tip, K['cyan4'])
        g.add(tip, L['cyan'], 0.8)
    return allm


def ghost(c, g, mask, a):
    edge = mask & ~erode(mask)
    c.put(mask, K['cyan0'], a * 0.35)
    c.put(edge, K['cyan2'], a * 0.8)
    g.add(edge, L['cyan'], 0.5 * a)


def esquiva_frame(f):
    t = f / FRAMES
    c, g = Canvas(AW, AH), Canvas(AW, AH)
    gradient_bg(c, K['teal0'], K['teal2'], K['teal1'], 0.6)
    c.put(c.disc(44, 28, 26), K['teal3'], 0.3)
    c.put(c.disc(44, 28, 16), K['teal3'], 0.55)
    c.put(c.disc(44, 28, 9), hexc('#24476a'), 0.6)
    floor = c.rect(0, 58, AW - 1, AH - 1)
    c.put(floor, K['teal0'])
    c.put(c.rect(0, 58, AW - 1, 58), K['teal3'])
    for x in range(3, AW, 11):
        c.put(c.rect(x, 60, x + 5, 60), K['teal1'])
        c.put(c.rect(x + 6, 62, x + 8, 62), K['teal1'])
    xl, xr = 32, 56

    def pos(fr):
        fr %= FRAMES
        if fr <= 3:
            return xl + (xr - xl) * ease_out(fr / 3.0), False
        if fr < 12:
            return float(xr), False
        if fr <= 15:
            return xr + (xl - xr) * ease_out((fr - 12) / 3.0), True
        return float(xl), True

    x, flip = pos(f)
    moving = f <= 3 or 12 <= f <= 15
    dash_start = 0 if f < 12 else 12
    age = f - dash_start
    src, dst = (xl, xr) if dash_start == 0 else (xr, xl)
    # afterimages left where the dash started (two ghosts, fading over 7 frames)
    for frac_pos, born in ((0.0, 0), (0.42, 1)):
        life = 1 - (age - born) / 7.0
        if age >= born and life > 0.05:
            gx_ = src + (dst - src) * frac_pos
            body, sc, face, eyes, blades, belt = figure_parts(c, round(gx_), 58, t, flip=(dash_start == 12), lean=1.0)
            ghost(c, g, body | sc, life * 0.8)
    d = 1 if dash_start == 12 else -1
    if age <= 7:
        k = 1 - age / 8
        rng = np.random.default_rng(3 + dash_start)
        for i in range(6):
            yy = rng.uniform(20, 55)
            x0 = x + d * (8 + age * 3 + rng.uniform(0, 8))
            ln = rng.uniform(5, 11) * k
            m = c.line(x0, yy, x0 + d * ln, yy)
            c.put(m, K['cyan3'] if i % 2 else K['steel3'], k)
            g.add(m, L['cyan'], 0.35 * k)
    if age <= 5:
        k = 1 - age / 6
        arc = (np.abs(np.hypot(c.x - (xl + xr) / 2, (c.y - 34) * 2.2) - 26) < 1.2) & (c.y > 52) & (c.y < 58)
        side = (c.x < x - 2) if d < 0 else (c.x > x + 2)
        m = arc & side
        c.put(m, K['cyan3'], k)
        g.add(m, L['cyan'], 0.8 * k)
    land = 3 if f < 12 else 15
    pa = f - land
    if 0 <= pa <= 6:
        k = 1 - pa / 7
        for sd in (-1, 1):
            px = x + sd * (8 + pa * 1.6)
            m = c.disc(px, 57 - pa * 0.4, 1.0 + pa * 0.45)
            c.put(m, K['dust1'], 0.7 * k)
            c.put(m & c.disc(px - sd * 0.6, 56.5 - pa * 0.4, 0.8 + pa * 0.3), K['dust0'], k)
    bob = 0 if moving else round(math.sin(t * math.pi * 4) * 0.6)
    parts = figure_parts(c, round(x), 58 + bob, t, flip=flip, lean=1.0 if moving else 0.0)
    paint_figure(c, g, parts, flip)
    motes(c, g, t, 33, 9, (2, 6, 86, 56), K['cyan3'], K['steel3'], L['cyan'], gain=0.6)
    vignette(c, 0.75)
    return c, g


# ---------------------------------------------------------------------------------------- Finta
def crescent(c, g, cx, cy, R, head, length, direction, thick, palette, light, a=1.0, dash=False, gain=1.0):
    """Slash crescent: thick bright head, tapering tail behind it. Angles in radians (y-up).
    direction = -1 when the head angle decreases over time (clockwise on screen), +1 otherwise."""
    ang = np.arctan2(-(c.y - cy), c.x - cx)
    d = ((ang - head) * (-direction)) % (2 * math.pi)     # angular distance behind the head
    inside = d <= length
    s = np.clip(d / max(length, 1e-6), 0, 1)
    r = np.hypot(c.x - cx, c.y - cy)
    th = np.maximum(thick * (1 - s) ** 0.7 * np.clip(d / 0.15 + 0.4, 0, 1), np.where(s < 0.92, 1.0, 0.0))
    band = inside & (r <= R + 0.5) & (r >= R + 0.5 - th - 0.5)
    if dash:
        band &= (np.floor(d * R / 3.0) % 3) != 2
    depth = np.clip((R + 0.5 - r) / np.maximum(th, 0.8), 0, 1)
    fade = np.clip(1.3 - s * 1.0, 0, 1) * a
    c0, c1, c2, c3 = palette
    c.put(band, c0, fade)
    c.put(band & (depth < 0.7), c1, fade)
    c.put(band & (depth < 0.42), c2, fade)
    c.put(band & (depth < 0.2) & (s < 0.6), c3, fade)
    g.add(band, light, gain * fade * (1.1 - depth))
    return cx + math.cos(head) * R, cy - math.sin(head) * R


def arc_dagger(c, g, cx, cy, R, theta, s, t, ghost=None, rune_gain=1.3, tip=40.0):
    """Dagger flying along the arc (head angle decreasing): tip on the arc head, blade along the motion."""
    da = math.pi / 2 - theta
    hx, hy = cx + math.cos(theta) * R, cy - math.sin(theta) * R
    gx_, gy_ = hx - math.cos(da) * tip * s, hy - math.sin(da) * tip * s
    lab, u, v = dagger_labels(c, gx_, gy_, da, s=s, tip=tip)
    paint_dagger(c, g, lab, u, t * 2, None, rune_gain=rune_gain, ghost=ghost)
    return gx_, gy_, da


def finta_frame(f):
    t = f / FRAMES
    c, g = Canvas(AW, AH), Canvas(AW, AH)
    gradient_bg(c, K['vio1'], K['vio2'], K['vio0'], 0.6)
    c.put(c.disc(44, 34, 27), K['vio3'], 0.25)
    c.put(c.disc(44, 34, 17), K['vio3'], 0.45)
    # slowly turning dotted magic circle
    for i in range(36):
        a = i / 36 * 2 * math.pi + t * 2 * math.pi / 6
        r = 23 if i % 2 == 0 else 21
        m = c.pixel(44 + math.cos(a) * r, 34 + math.sin(a) * r * 0.92)
        c.put(m, K['violet1'] if i % 6 else K['violet2'])
        if i % 6 == 0:
            g.add(m, L['violet'], 0.25)
    rng = np.random.default_rng(23)
    for i in range(12):
        sx, sy = rng.uniform(2, 86), rng.uniform(2, 62)
        b = 0.5 + 0.5 * math.sin((t * (1 + i % 2) + rng.random()) * math.pi * 2)
        if b > 0.4:
            m = c.pixel(sx, sy)
            c.put(m, K['violet3'] if b < 0.85 else K['violet4'])
            g.add(m, L['violet'], 0.3 * b)

    home_ang = -math.atan2(1, 2)
    home = (36, 40)
    s = 0.62
    C1, R1 = (46, 47), 33            # feint: over the top, left -> right
    C2, R2 = (42, 15), 37            # strike: underneath, right -> left
    a_f0, a_f1 = math.radians(158), math.radians(22)
    a_r0, a_r1 = math.radians(326), math.radians(208)
    vpal = (K['violet0'], K['violet1'], K['violet2'], K['violet3'])
    cpal = (K['cyan0'], K['cyan2'], K['cyan3'], K['cyan4'])

    # 1) feint: a violet ghost dagger swings over the top, then dissolves
    if 2 <= f <= 11:
        k = np.clip((f - 2) / 5.0, 0, 1)
        head = a_f0 + (a_f1 - a_f0) * ease_out(k)
        fade = 1.0 if f <= 7 else max(0.0, 1 - (f - 7) / 4.0)
        if fade > 0:
            crescent(c, g, C1[0], C1[1], R1, head, 1.5, -1, 4.0, vpal, L['violet'], a=0.95 * fade, dash=True, gain=0.9)
            if 3 <= f <= 7:
                arc_dagger(c, g, C1[0], C1[1], R1, head, 0.5, t, ghost=(K['violet3'], 0.9 * fade, L['violet']))
        if f >= 7:
            rng = np.random.default_rng(9)
            for i in range(16):
                aa = a_f1 + (a_f0 - a_f1) * rng.random()
                age = f - 7
                px = C1[0] + math.cos(aa) * R1 + rng.uniform(-1, 1) * age
                py = C1[1] - math.sin(aa) * R1 - age * rng.uniform(0.4, 1.0)
                a = 1 - age / 5.0
                if a > 0:
                    m = c.pixel(px, py)
                    c.put(m, K['violet3'], a)
                    g.add(m, L['violet'], 0.7 * a)

    # 2) the real dagger: home, wind-up, strike underneath, impact, twirl back home
    if f <= 9:
        lab, u, v = dagger_labels(c, home[0], round(home[1] + math.sin(t * math.pi * 4) * 0.7), home_ang, s=s)
        paint_dagger(c, g, lab, u, t * 2, (-4 + ((f * 7) % 60)) if f < 8 else None, rune_gain=1.2)
    elif f <= 11:
        k = (f - 9) / 2.0
        lab, u, v = dagger_labels(c, home[0] + 4 * k, home[1] + 2 * k, home_ang + 0.5 * k, s=s)
        paint_dagger(c, g, lab, u, t * 2 + 1, None, rune_gain=1.8)
    elif f <= 14:
        k = (f - 11) / 3.0
        head = a_r0 + (a_r1 - a_r0) * ease_out(k)
        crescent(c, g, C2[0], C2[1], R2, head, 1.8, -1, 5.5, cpal, L['cyan'], a=1.0, gain=1.6)
        arc_dagger(c, g, C2[0], C2[1], R2, head, 0.5, t, rune_gain=2.0)
    else:
        if f <= 19:
            q = (f - 14) / 6.0
            crescent(c, g, C2[0], C2[1], R2, a_r1, 1.8 * (1 - q * 0.6), -1, 5.5 * (1 - q), cpal, L['cyan'], a=1 - q, gain=1.3)
        k = min(1.0, (f - 14) / 8.0)
        e = ease_in_out(k)
        da0 = math.pi / 2 - a_r1
        hx, hy = C2[0] + math.cos(a_r1) * R2, C2[1] - math.sin(a_r1) * R2
        ex, ey = hx - math.cos(da0) * 40 * 0.5, hy - math.sin(da0) * 40 * 0.5
        da = da0 + ((home_ang - da0 + math.pi) % (2 * math.pi) - math.pi) * e
        lab, u, v = dagger_labels(c, round(ex + (home[0] - ex) * e), round(ey + (home[1] - ey) * e), da, s=s)
        paint_dagger(c, g, lab, u, t * 2, None, rune_gain=1.3)

    endx, endy = C2[0] + math.cos(a_r1) * R2, C2[1] - math.sin(a_r1) * R2
    if 14 <= f <= 17:
        star4(c, g, endx, endy, {14: 6, 15: 8, 16: 5, 17: 2}[f], gain=1.4)
    rng = np.random.default_rng(17)
    for i in range(14):
        a0 = rng.uniform(0, 2 * math.pi)
        spd = rng.uniform(1.2, 3.0)
        age = f - 14
        if 0 < age <= 8:
            px = endx + math.cos(a0) * spd * age
            py = endy - math.sin(a0) * spd * age + 0.1 * age * age
            a = 1 - age / 9
            m = c.pixel(px, py)
            c.put(m, K['cyan3'] if i % 3 else K['violet3'])
            g.add(m, L['cyan'] if i % 3 else L['violet'], a)
    motes(c, g, t, 41, 8, (2, 6, 86, 60), K['violet3'], K['cyan3'], L['violet'], gain=0.55)
    vignette(c, 0.7)
    return c, g


ILLUSTRATIONS = {
    'punalada': punalada_frame,
    'esquiva': esquiva_frame,
    'finta': finta_frame,
}


def render(card_id):
    fn = ILLUSTRATIONS[card_id]
    base, glow = [], []
    for f in range(FRAMES):
        b, gl = fn(f)
        base.append(b)
        glow.append(gl)
    return base, glow
