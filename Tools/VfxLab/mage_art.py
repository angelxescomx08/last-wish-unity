"""Animated pixel-art illustrations for the mage's basic cards (88x64, 24 frames at 12 fps, seamless loop).

Same technique, palette and helpers as rogue_art.py (docs/art-style.md): every frame has a base image (opaque pixel
art, readable without bloom) and an emissive light map, baked for the game by export_game_card_art.py.
No characters: each picture is the spell itself (an arcane bolt, a ward, a fire spark), like the dagger pictures of
the rogue, so nothing is assembled from body parts and a future redesign of the mage sprite does not touch them.
"""
import math

import numpy as np

from pixelkit import Canvas, hexc, dilate, erode
from rogue_art import (AW, AH, FRAMES, K, L, ease_out, ease_in_out, frac, gradient_bg, vignette, star4, motes,
                       outline_mask)

# the rogue palette plus a fire ramp (same contrast steps as the cyan/violet magic) for Chispa Ígnea
KM = dict(K)
KM.update({
    'fire0': hexc('#3d0f14'), 'fire1': hexc('#8f2419'), 'fire2': hexc('#d9531e'), 'fire3': hexc('#f5973a'),
    'fire4': hexc('#ffd47a'), 'fire5': hexc('#fff6dc'),
    'smoke1': hexc('#2a2233'), 'smoke2': hexc('#3d3348'),
    'earth0': hexc('#0a0710'), 'earth1': hexc('#130d1b'), 'earth2': hexc('#1e1528'),
    'shaft1': hexc('#4a3024'),
})
LM = dict(L)
LM.update({'fire': np.array([1.0, 0.42, 0.1, 1]), 'ember': np.array([1.0, 0.68, 0.3, 1])})


# ---------------------------------------------------------------------------------------- shared bits
def twinkling_stars(c, g, t, seed, count, area, avoid=None, light=None):
    """Background stars that twinkle with an integer number of cycles per loop (seamless)."""
    light = L['blue'] if light is None else light
    rng = np.random.default_rng(seed)
    x0, y0, x1, y1 = area
    for i in range(count):
        sx, sy = rng.uniform(x0, x1), rng.uniform(y0, y1)
        ph = rng.random()
        if avoid is not None and math.hypot(sx - avoid[0], sy - avoid[1]) < avoid[2]:
            continue
        b = 0.5 + 0.5 * math.sin((t * (1 + i % 3) + ph) * math.pi * 2)
        m = c.pixel(sx, sy)
        if b > 0.25:
            c.put(m, K['moon1'] if b < 0.8 else K['white'])
            g.add(m, light, 0.3 * b)
        if b > 0.9 and i % 3 == 0:
            for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                mm = c.pixel(sx + dx, sy + dy)
                c.put(mm, K['moon0'])
                g.add(mm, light, 0.15)


def sparks(c, g, f, born, x, y, seed, count, speed, life, cols, light, gravity=0.12, spread=(0.0, 2 * math.pi),
           gain=1.0):
    """A burst of 1-px sparks with short streaks, born on frame `born` (y-up angles; ages wrap with the loop)."""
    rng = np.random.default_rng(seed)
    age = (f - born) % FRAMES
    for i in range(count):
        a0 = rng.uniform(*spread)
        spd = rng.uniform(*speed)
        if not 0 < age <= life:
            continue
        px = x + math.cos(a0) * spd * age
        py = y - math.sin(a0) * spd * age + gravity * age * age
        a = 1 - age / (life + 1)
        m = c.pixel(px, py)
        c.put(m, cols[0] if age < 3 else cols[1])
        g.add(m, light, gain * a)
        if age < 4:
            vx, vy = math.cos(a0) * spd, -math.sin(a0) * spd + 2 * gravity * age
            m2 = c.line(px, py, px - vx * 0.9, py - vy * 0.9)
            c.put(m2 & ~m, cols[2], a)
            g.add(m2, light, 0.3 * a * gain)


def ellipse_ring(c, cx, cy, rx, ry, width=0.6):
    d = np.hypot((c.x - cx) / max(rx, 0.1), (c.y - cy) / max(ry, 0.1))
    return np.abs(d - 1) * min(rx, ry) <= width


# ---------------------------------------------------------------------------------------- Proyectil Arcano
P_HOME = (29.0, 32.0)
P_HIT = (70.0, 22.0)


def paint_orb(c, g, x, y, r, sx=1.0, sy=1.0, gain=1.0, sigil=0):
    """Arcane orb: white core, cyan body, dithered cyan halo, violet bounce lower right. sx/sy squash and stretch it.
    sigil 1/2 draws a small four-point star (+ or x) in the core."""
    if r <= 0.2:
        return
    d = np.hypot((c.x - x) / sx, (c.y - y) / sy) / r
    lx, ly = (c.x - x) / sx, (c.y - y) / sy
    c.put(d <= 1.6, K['cyan0'], np.clip(1.6 - d, 0, 1) * 1.4)
    c.put(d <= 1.28, K['cyan1'], np.clip((1.28 - d) * 3, 0, 1))
    body = d <= 1.0
    c.put(outline_mask(body) & (d > 1.0), K['ink'], 0.5)
    c.put(body, K['cyan1'])
    c.put(body & (d <= 0.92) & (lx + ly < r * 0.5), K['cyan2'])           # lit from the upper left
    c.put(body & (d <= 0.6), K['cyan3'])
    c.put(body & (d <= 0.32), K['white'])
    c.put(body & (d > 0.66) & (lx + ly > r * 0.75), K['violet2'])          # violet bounce lower right
    c.put(body & (d > 0.55) & (d <= 0.85) & (lx + ly < -r * 0.95), K['cyan4'])   # rim highlight
    g.add(d <= 1.3, L['cyan'], gain * 0.5 * np.clip(1.2 - d, 0, 1.2))
    g.add(d <= 0.36, L['white'], gain * 0.45)
    if sigil and r >= 4:
        arm = int(round(r * 0.55))
        dirs = ((1, 0), (-1, 0), (0, 1), (0, -1)) if sigil == 1 else ((1, 1), (-1, 1), (1, -1), (-1, -1))
        for i in range(1, arm + 1):
            for dx, dy in dirs:
                m = c.pixel(x + dx * i, y + dy * i)
                c.put(m, K['white'] if i < arm else K['cyan4'])
                g.add(m, L['cyan'], gain * 0.6 * (1 - i / (arm + 1)))


def paint_bolt(c, g, hx, hy, ux, uy, length, r):
    """The orb in flight: bright head plus a tapering tail behind it (cyan into violet)."""
    vx, vy = -uy, ux
    dx, dy = c.x - hx, c.y - hy
    s = -(dx * ux + dy * uy)                 # distance behind the head
    v = np.abs(dx * vx + dy * vy)
    w = r * np.clip(1 - s / length, 0, 1) ** 0.75
    tail = (s >= 0) & (s <= length) & (v <= w + 0.35)
    k = np.clip(s / length, 0, 1)
    depth = v / np.maximum(w, 0.5)
    c.put(outline_mask(tail) & (k < 0.8), K['violet0'], 0.8)
    c.put(tail, K['violet1'], np.clip(1.25 - k, 0, 1))
    c.put(tail & (k < 0.85), K['violet2'])
    c.put(tail & (k < 0.62) & (depth < 0.78), K['cyan2'])
    c.put(tail & (k < 0.42) & (depth < 0.5), K['cyan3'])
    c.put(tail & (k < 0.22) & (depth < 0.32), K['cyan4'])
    g.add(tail, L['cyan'], np.clip(1 - k, 0, 1) * (1.1 - depth * 0.6))
    g.add(tail & (k > 0.45), L['violet'], 0.6 * np.clip(1 - k, 0, 1))
    paint_orb(c, g, hx, hy, r, sx=1.3, sy=0.82, gain=1.5)


def rune_ring(c, g, cx, cy, rx, ry, t, alpha, gain, front):
    """Vertical magic circle seen almost edge-on: two dotted ellipses with runes between them, turning once per loop.
    Draw it twice: front=False before the orb (far half, dim) and front=True after it (near half)."""
    if alpha <= 0.02:
        return
    half = (c.x < cx) if front else (c.x >= cx)
    outer = ellipse_ring(c, cx, cy, rx, ry, 0.55) & half
    inner = ellipse_ring(c, cx, cy, rx * 0.72, ry * 0.8, 0.5) & half
    c.put(outer, K['cyan1'] if front else K['cyan0'], alpha * (0.9 if front else 0.6))
    c.put(inner, K['cyan0'] if front else K['cyan0'], alpha * (0.7 if front else 0.4))
    g.add(outer, L['cyan'], gain * alpha * (0.22 if front else 0.08))
    n = 18
    for i in range(n):
        th = 2 * math.pi * (i / n + t)
        near = math.cos(th) > 0
        if near != front:
            continue
        rune = i % 3 == 0
        px, py = cx - math.cos(th) * rx, cy + math.sin(th) * ry
        m = c.pixel(px, py)
        if rune:                                  # a rune: a short tick between the two rings
            qx, qy = cx - math.cos(th) * rx * 0.86, cy + math.sin(th) * ry * 0.9
            m = m | c.pixel(qx, qy)
        col = (K['cyan4'] if rune else K['cyan3']) if front else (K['cyan2'] if rune else K['cyan1'])
        c.put(m, col, alpha)
        g.add(m, L['cyan'], gain * alpha * (0.9 if front else 0.35) * (1.3 if rune else 0.7))


def proyectil_scene(c, g, t):
    gradient_bg(c, K['night0'], K['night2'], K['night1'], 0.6)
    c.put(c.disc(50, 26, 31), K['night3'], 0.25)              # dithered nebula behind the path
    c.put(c.disc(60, 22, 17), K['vio3'], 0.3)
    c.put(c.disc(62, 21, 8), K['vio3'], 0.4)
    twinkling_stars(c, g, t, 13, 22, (2, 2, 86, 40), avoid=(P_HIT[0], P_HIT[1], 6))
    # far ridge with a lit edge, and a near ridge with a ruined watchtower whose window flickers
    far = c.poly([(0, 46), (9, 41), (16, 44), (25, 38), (34, 43), (44, 39), (53, 44), (63, 40), (72, 43), (80, 39),
                  (88, 42), (88, 64), (0, 64)])
    c.put(far, K['vio0'])
    c.put(far & ~np.roll(far, 1, axis=0), K['moon0'], 0.55)
    c.put(far & ~np.roll(far, 1, axis=0) & ~np.roll(far, 1, axis=1), K['moon0'])
    near = c.poly([(0, 54), (10, 51), (21, 53), (31, 50), (42, 53), (55, 51), (63, 52), (70, 49), (88, 51),
                   (88, 64), (0, 64)])
    tower = c.poly([(72, 50), (72, 37), (74, 35), (75, 37), (77, 34), (79, 36), (80, 36), (80, 50)])
    c.put(near | tower, K['roof'])
    c.put(outline_mask(near | tower) & ~(near | tower), K['roof2'])
    lit = 0.5 + 0.5 * math.sin(t * 4 * math.pi + 0.7)
    win = c.rect(76, 40, 76, 41)
    c.put(win, K['window'] if lit > 0.3 else K['window2'])
    g.add(win, L['warm'], 0.3 * lit)


def proyectil_frame(f):
    t = f / FRAMES
    c, g = Canvas(AW, AH), Canvas(AW, AH)
    proyectil_scene(c, g, t)
    hx, hy = P_HOME
    ix, iy = P_HIT
    bob = math.sin(t * 2 * math.pi * 2) * 0.8
    sigil = 1 + (f // 3) % 2

    # --- the orb: hovers (0-9), gathers and pulls back (10-13), flies (14-15), hits (16), re-forms at home (19-23)
    pull = 2.5 * ease_in_out((f - 9) / 4.0) if 10 <= f <= 13 else 0.0
    orb = None                     # (x, y, r, sx, sy, gain)
    ring = (8.5, 19.0, 1.0, 1.0)   # rx, ry, alpha, gain
    if f <= 9:
        orb = (hx, hy + bob, 7.0, 1, 1, 1.0)
    elif f <= 13:
        k = ease_in_out((f - 9) / 4.0)
        orb = (hx - pull, hy + bob * (1 - k), 7.0 - 1.3 * k, 1 - 0.3 * k, 1 + 0.25 * k, 1.0 + 0.9 * k)
        ring = (8.5 - 1.5 * k, 19.0 - 3.5 * k, 1.0, 1.0 + 1.2 * k)
    elif f <= 16:
        ring = {14: (9.5, 21.0, 0.9, 1.8), 15: (10.5, 22.0, 0.5, 1.0), 16: (11.0, 22.5, 0.2, 0.5)}[f]
    elif f >= 19:
        r = {19: 1.2, 20: 2.8, 21: 4.3, 22: 5.6, 23: 6.5}[f]
        k = (f - 18) / 6.0
        orb = (hx, hy + bob, r, 1, 1, 0.8 + 0.5 * (1 - k))
        ring = (8.5, 19.0, {19: 0.15, 20: 0.35, 21: 0.6, 22: 0.8, 23: 0.92}[f], 1.0)
    else:
        ring = (8.5, 19.0, 0.0, 0.0)
    ry_ = hy + (bob if f <= 9 or f >= 19 else 0)
    rune_ring(c, g, hx - pull, ry_, ring[0], ring[1], t, ring[2], ring[3], front=False)

    # launch: one pulse ring pushed forward along the path
    def path(u):
        return hx - 2.5 + (ix - hx + 2.5) * u, hy + (iy - hy) * u - 5 * math.sin(math.pi * u)

    if 14 <= f <= 15:
        k = (f - 14) / 2.0
        px_, py_ = path(0.22 + 0.1 * k)
        a = 0.9 * (1 - k)
        m = ellipse_ring(c, px_, py_, 4.0 + 2 * k, 13 + 3 * k, 0.55)
        c.put(m, K['cyan3'], a)
        g.add(m, L['cyan'], 0.7 * a)

    # gather: motes pulled into the orb
    if 9 <= f <= 13:
        rng = np.random.default_rng(31)
        k = (f - 9) / 4.0
        for i in range(10):
            a0 = rng.uniform(0, 2 * math.pi) + k * 1.4
            r0 = rng.uniform(13, 21) * (1 - ease_in_out(k)) + 4
            m = c.pixel(hx - pull + math.cos(a0) * r0, hy + math.sin(a0) * r0)
            c.put(m, K['cyan3'] if i % 3 else K['violet3'])
            g.add(m, L['cyan'] if i % 3 else L['violet'], 0.9)

    # flight: the bolt, speed lines streaming from its tail
    if 14 <= f <= 15:
        u = {14: 0.4, 15: 0.76}[f]
        bx, by = path(u)
        qx, qy = path(u - 0.04)
        dl = math.hypot(bx - qx, by - qy)
        ux, uy = (bx - qx) / dl, (by - qy) / dl
        vx, vy = -uy, ux
        tail = {14: 20, 15: 28}[f]
        k = 1.0 if f == 15 else 0.0
        for i, off in enumerate((-6, 6, -8, 8, -4, 4) if f == 15 else ()):
            start = tail * 0.45 + (i * 5) % 6
            ln = (7 + (i * 3) % 6) * k
            sx, sy = bx - ux * start + vx * off, by - uy * start + vy * off
            m = c.line(sx, sy, sx - ux * ln, sy - uy * ln)
            c.put(m, K['cyan3'] if i % 2 == 0 else K['violet3'], k)
            g.add(m, L['cyan'], 0.45 * k)
        paint_bolt(c, g, bx, by, ux, uy, tail, 5.6)
    # sparkles left along the path, fading
    if 14 <= f <= 21:
        rng = np.random.default_rng(57)
        for i in range(18):
            u = rng.uniform(0.05, 0.95)
            born = 14 if u < 0.4 else 15 if u < 0.76 else 16
            age = f - born
            if not 0 <= age <= 5:
                continue
            x, y = path(u)
            off = rng.uniform(-3.5, 3.5)
            m = c.pixel(x + off * 0.3, y + off + age * 0.5)
            a = 1 - age / 6
            c.put(m, K['cyan4'] if age < 2 else (K['cyan2'] if i % 3 else K['violet3']), a + 0.2)
            g.add(m, L['cyan'] if i % 3 else L['violet'], 0.8 * a)

    if orb is not None:
        paint_orb(c, g, orb[0], orb[1], orb[2], orb[3], orb[4], orb[5], sigil=sigil if f <= 13 or f >= 22 else 0)
        if f <= 13 or f >= 21:                    # three sparks orbiting the hovering orb
            for j in range(3):
                th = 2 * math.pi * (2 * t + j / 3)
                for back, col, a, gl in ((0.0, K['cyan4'], 1.0, 0.9), (0.3, K['cyan2'], 0.8, 0.35)):
                    m = c.pixel(orb[0] + math.cos(th - back) * 12, orb[1] + math.sin(th - back) * 4.5)
                    c.put(m, col, a)
                    g.add(m, L['cyan'], gl)
    rune_ring(c, g, hx - pull, ry_, ring[0], ring[1], t, ring[2], ring[3], front=True)

    # impact: flash, star, shock ring, sparks and violet shards
    if f == 16:
        c.put(c.disc(ix, iy, 7.5), K['cyan2'], 0.8)
        c.put(c.disc(ix, iy, 5.0), K['cyan4'])
        c.put(c.disc(ix, iy, 2.8), K['white'])
        g.add(c.disc(ix, iy, 7.5), L['cyan'], 1.3)
    if 16 <= f <= 19:
        star4(c, g, ix, iy, {16: 12, 17: 9, 18: 5, 19: 2}[f], gain=1.5)
    if 16 <= f <= 21:
        rad = {16: 6, 17: 10, 18: 14, 19: 16.5, 20: 18, 21: 19}[f]
        a = {16: 1.0, 17: 0.85, 18: 0.6, 19: 0.4, 20: 0.22, 21: 0.1}[f]
        rr = np.hypot(c.x - ix, (c.y - iy) * 1.15)
        band = np.abs(rr - rad) <= 0.7
        c.put(band, K['cyan3'], a)
        c.put(np.abs(rr - (rad - 1.6)) <= 0.5, K['violet2'], a * 0.7)
        g.add(band, L['cyan'], 0.9 * a)
    sparks(c, g, f, 16, ix, iy, 19, 16, (1.3, 3.4), 8, (K['cyan4'], K['cyan2'], K['cyan1']), L['cyan'])
    rng = np.random.default_rng(23)
    for i in range(6):                                       # violet shards that spin outwards
        a0 = rng.uniform(0, 2 * math.pi)
        spd = rng.uniform(1.4, 2.4)
        age = f - 16
        if 0 < age <= 6:
            px = ix + math.cos(a0) * spd * age
            py = iy - math.sin(a0) * spd * age + 0.15 * age * age
            sp = a0 + age * 1.1
            m = c.line(px - math.cos(sp), py - math.sin(sp), px + math.cos(sp), py + math.sin(sp))
            a = 1 - age / 7
            c.put(m, K['violet3'], a + 0.2)
            g.add(m, L['violet'], 0.8 * a)

    # re-forming: motes converge on the home spot
    if 18 <= f <= 23:
        rng = np.random.default_rng(37)
        k = (f - 17) / 6.0
        for i in range(10):
            a0 = rng.uniform(0, 2 * math.pi) - k * 1.2
            r0 = rng.uniform(12, 20) * (1 - ease_out(k)) + 4
            m = c.pixel(hx + math.cos(a0) * r0, hy + bob + math.sin(a0) * r0)
            c.put(m, K['cyan3'] if i % 2 else K['violet3'], 0.9)
            g.add(m, L['cyan'] if i % 2 else L['violet'], 0.7)

    motes(c, g, t, 61, 8, (2, 6, 86, 50), K['cyan3'], K['violet3'], L['cyan'], gain=0.55)
    vignette(c, 0.7)
    return c, g


# ---------------------------------------------------------------------------------------- Égida
E_C = (42.0, 30.0)          # shield centre
E_R = 22.0                  # barrier radius
E_HIT_ANG = math.radians(24)


def shield_labels(c, cx, cy):
    """Heater shield facing the viewer, with an arcane sigil (crystal, engraved ring and four rays).
    Labels: 1 rim, 2 face, 3 engraved channels, 4 crystal, 5 crystal light, 6 crystal shade, 7 studs."""
    W, top, bot, waist = 11.0, cy - 13.0, cy + 15.0, cy - 2.0
    dx, y = c.x - cx, c.y
    upper = (y >= top + 1.2 * (dx / W) ** 2) & (y <= waist) & (np.abs(dx) <= W)
    k = np.clip((y - waist) / (bot - waist), 0, 1)
    lower = (y > waist) & (y <= bot) & (np.abs(dx) <= W * (1 - k ** 1.7) + 0.3)
    m = upper | lower
    lab = np.zeros((c.h, c.w), int)
    lab[m] = 2
    rim = m & ~erode(erode(m))
    lab[rim] = 1
    inner = m & ~rim
    gy = cy - 1.5
    dist = np.hypot(dx, y - gy)
    lab[inner & (np.abs(dist - 6.0) <= 0.55)] = 3
    rays = ((np.abs(dx) <= 0.6) & (np.abs(y - gy) > 6.5)) | ((np.abs(y - gy) <= 0.6) & (np.abs(dx) > 6.5))
    lab[inner & rays & ~dilate(rim) & (y < bot - 5)] = 3
    gd = np.abs(dx) / 3.4 + np.abs(y - gy) / 4.6            # rhombus crystal in the middle
    lab[gd <= 1.0] = 4
    lab[(gd <= 1.0) & (dx + (y - gy) < -0.5)] = 5
    lab[(gd <= 1.0) & (dx - (y - gy) > 1.0) & (y > gy)] = 6
    for sx, sy in ((-7.5, top + 3.5), (7.5, top + 3.5), (-6.0, waist + 6), (6.0, waist + 6)):
        lab[(np.abs(dx - sx) < 0.6) & (np.abs(y - sy) < 0.6) & inner] = 7
    return lab


def paint_shield(c, g, lab, cx, cy, t_rune, rune_gain=1.0, glint=None):
    m = lab > 0
    c.put(outline_mask(m), K['ink'])
    dx, dy = c.x - cx, c.y - cy
    light = -(dx * 0.7 + dy * 0.7)              # upper-left is lit
    face = lab == 2
    c.put(face, K['vio2'])
    c.put(face & (light > -2), K['night3'], np.clip((light + 2) / 8, 0, 1))
    c.put(face & (light > 4), K['guard0'], np.clip((light - 4) / 6, 0, 1) * 0.8)
    c.put(face & (light < -6), K['vio1'], np.clip((-6 - light) / 6, 0, 1))
    rim = lab == 1
    c.put(rim, K['steel1'])
    c.put(rim & (light > -3), K['steel2'])
    c.put(rim & (light > 5), K['steel3'])
    c.put(rim & (light < -9), K['steel0'])
    c.put(rim & (light > 9.5), K['steel4'])
    c.put(lab == 7, K['gold1'])
    c.put((lab == 7) & (light > 0), K['window'])
    # the sigil: a light wave runs out from the crystal along the ring and the rays
    ch = lab == 3
    dist = np.hypot(dx, dy + 1.5)
    ang = np.arctan2(dy + 1.5, dx)
    wave = 0.5 + 0.5 * np.sin((dist / 6.0 + ang / (2 * math.pi) - t_rune) * math.pi * 2)
    pat = (np.floor(dist + ang * 2) % 3) != 2
    c.put(ch, K['cyan0'])
    c.put(ch & pat, K['cyan1'])
    c.put(ch & pat & (wave > 0.55), K['cyan2'])
    c.put(ch & pat & (wave > 0.85), K['cyan3'])
    g.add(ch & pat, L['cyan'], rune_gain * (0.2 + 0.7 * wave))
    gem = (lab >= 4) & (lab <= 6)
    c.put(lab == 4, K['cyan2'])
    c.put(lab == 5, K['cyan4'])
    c.put(lab == 6, K['cyan1'])
    c.put((lab == 5) & (np.abs(dx + 1.2) < 0.6) & (np.abs(dy + 3.7) < 0.6), K['white'])
    g.add(gem, L['cyan'], 0.9 * rune_gain)
    g.add(lab == 5, L['white'], 0.4 * rune_gain)
    if glint is not None:                         # a highlight sweeping down the rim
        gl = rim & (np.abs((dx + dy) - glint) <= 1.5)
        c.put(gl, K['white'])
        g.add(gl, L['white'], 0.7)


def hex_cells(c, cx, cy, size):
    """Pointy-top hex grid around (cx, cy): per pixel the centre of its cell and the hex distance to it (1 = edge)."""
    x, y = c.x - cx, c.y - cy
    q = (math.sqrt(3) / 3 * x - y / 3) / size
    r = (2 / 3 * y) / size
    s = -q - r
    rq, rr, rs = np.round(q), np.round(r), np.round(s)
    dq, dr, ds = np.abs(rq - q), np.abs(rr - r), np.abs(rs - s)
    fix_q = (dq > dr) & (dq > ds)
    fix_r = ~fix_q & (dr > ds)
    rq = np.where(fix_q, -rr - rs, rq)
    rr = np.where(fix_r, -rq - rs, rr)
    hx_ = size * math.sqrt(3) * (rq + rr / 2)
    hy_ = size * 1.5 * rr
    lx, ly = x - hx_, y - hy_
    ri = size * math.sqrt(3) / 2
    n = np.maximum(np.abs(lx), np.abs(lx * 0.5 + ly * math.sqrt(3) / 2))
    n = np.maximum(n, np.abs(lx * 0.5 - ly * math.sqrt(3) / 2))
    return hx_ + cx, hy_ + cy, n / ri


def paint_arrow(c, g, tx, ty, ang, a=1.0, curse=1.0):
    """An enemy arrow with a cursed violet glow, tip at (tx, ty), pointing along `ang` (screen radians)."""
    ux, uy = math.cos(ang), math.sin(ang)
    vx, vy = -uy, ux
    shaft = c.line(tx - ux * 4, ty - uy * 4, tx - ux * 17, ty - uy * 17)
    head = c.poly([(tx + ux * 0.6, ty + uy * 0.6), (tx - ux * 4.6 + vx * 2.8, ty - uy * 4.6 + vy * 2.8),
                   (tx - ux * 3.4, ty - uy * 3.4), (tx - ux * 4.6 - vx * 2.8, ty - uy * 4.6 - vy * 2.8)])
    fl = c.line(tx - ux * 14 + vx * 0.5, ty - uy * 14 + vy * 0.5, tx - ux * 18 + vx * 3, ty - uy * 18 + vy * 3)
    fl |= c.line(tx - ux * 14 - vx * 0.5, ty - uy * 14 - vy * 0.5, tx - ux * 18 - vx * 3, ty - uy * 18 - vy * 3)
    allm = shaft | head | fl
    c.put(outline_mask(allm), K['ink'], a)
    c.put(shaft, KM['shaft1'], a)
    c.put(fl, K['violet3'], a)
    c.put(head, K['violet2'], a)
    c.put(head & (c.pixel(tx, ty) | c.pixel(tx - ux * 1.5, ty - uy * 1.5) | c.pixel(tx - ux * 2.5, ty - uy * 2.5)),
          K['violet4'], a)
    g.add(head | fl, L['violet'], 0.7 * a * curse)


def egida_frame(f):
    t = f / FRAMES
    c, g = Canvas(AW, AH), Canvas(AW, AH)
    gradient_bg(c, K['teal0'], K['teal2'], K['teal1'], 0.6)
    cx0, cy0 = E_C
    c.put(c.disc(cx0, cy0 + 2, 31), K['teal3'], 0.25)
    c.put(c.disc(cx0, cy0 + 2, 20), K['teal3'], 0.45)
    c.put(c.disc(cx0, cy0 + 1, 11), hexc('#24476a'), 0.55)
    twinkling_stars(c, g, t, 47, 10, (2, 2, 86, 30), avoid=(cx0, cy0, E_R + 2), light=L['cyan'])
    floor = c.rect(0, 57, AW - 1, AH - 1)
    c.put(floor, K['teal0'])
    c.put(c.rect(0, 57, AW - 1, 57), K['teal3'])
    for x in range(2, AW, 11):
        c.put(c.rect(x, 59, x + 5, 59), K['teal1'])
        c.put(c.rect(x + 6, 61, x + 8, 61), K['teal1'])

    hit = 10
    age = f - hit
    bob = round(math.sin(t * 2 * math.pi * 2) * 0.7)
    recoil = {10: -2, 11: -2, 12: -1, 13: -1}.get(f, 0)
    cx, cy = cx0 + recoil, cy0 + bob
    hx, hy = cx0 + math.cos(E_HIT_ANG) * E_R, cy0 - math.sin(E_HIT_ANG) * E_R

    # ground rune circle (turns once per loop, flares on the hit)
    flare = max(0.0, 1 - abs(age - 0.5) / 4) if -1 <= age <= 5 else 0.0
    gr = ellipse_ring(c, cx0, 58, 17, 2.6, 0.5)
    c.put(gr, K['cyan1'], 0.45 + 0.4 * flare)
    g.add(gr, L['cyan'], 0.12 + 0.4 * flare)
    for i in range(12):
        th = 2 * math.pi * (i / 12 - t)
        m = c.pixel(cx0 + math.cos(th) * 17, 58 + math.sin(th) * 2.6)
        c.put(m, K['cyan3'] if i % 3 == 0 else K['cyan2'])
        g.add(m, L['cyan'], 0.35 + 0.6 * flare)
    c.put(c.disc(cx0, 57.5, 9) & (np.abs(c.y - 57.5) < 1.3), K['teal0'], 0.7)

    # barrier: a faint dome, brighter on the side the arrow comes from (anticipation) and on the hit
    rr = np.hypot(c.x - cx0, c.y - cy0)
    ang = np.arctan2(-(c.y - cy0), c.x - cx0)
    toward = np.clip(np.cos(ang - E_HIT_ANG), 0, 1) ** 2
    warn = np.clip((f - 4) / 5.0, 0, 1) if f <= 10 else max(0.0, 1 - (f - 10) / 5.0)
    shell = np.abs(rr - E_R) <= 0.6
    c.put(shell, K['cyan1'], 0.32 + 0.55 * warn * toward + 0.5 * flare)
    c.put(shell & (toward > 0.6), K['cyan2'], warn * 0.9 + flare * 0.5)
    g.add(shell, L['cyan'], 0.1 + 0.6 * warn * toward + 0.5 * flare)
    c.put((rr < E_R - 0.6) & (rr > E_R - 2.6), K['cyan0'], 0.25 + 0.3 * flare)
    for j, (spd, length, gain) in enumerate(((1, 0.55, 0.8), (-1, 0.35, 0.45))):
        head = 2 * math.pi * (spd * t + j * 0.5) + 1.0
        d = (head - ang) % (2 * math.pi)
        arc = shell & (d < length)
        c.put(arc, K['cyan3'], gain)
        g.add(arc, L['cyan'], gain * (1 - d / length) * 0.9)

    # the hit: the whole hex lattice flashes, then a ripple of lit cells runs across the dome
    if 0 <= age <= 8:
        hcx, hcy, hn = hex_cells(c, cx0, cy0, 3.3)
        inside = rr <= E_R - 0.5
        rad = 2 + age * 6.0
        cell_d = np.hypot(hcx - hx, hcy - hy)
        band = np.clip(1 - np.abs(cell_d - rad) / 5.0, 0, 1) * (1 - age / 9.0)
        edge = inside & (hn > 0.78)
        if age <= 2:
            c.put(edge, K['cyan1'], 0.55 * (1 - age / 3))
            g.add(edge, L['cyan'], 0.25 * (1 - age / 3))
        lit = edge & (band > 0.05)
        c.put(inside & (hn <= 0.78) & (band > 0.35), K['cyan0'], band * 0.7)
        c.put(lit, K['cyan2'], band * 1.2)
        c.put(lit & (band > 0.55), K['cyan3'])
        g.add(lit, L['cyan'], band * 1.2)

    # the arrow: incoming (6-9) with a violet trail, hits (10), tumbles away and fades (11-17)
    sx0, sy0 = 104.0, 8.0
    inc = math.atan2(hy - sy0, hx - sx0)
    ux, uy = math.cos(inc), math.sin(inc)
    if 6 <= f <= 10:
        k = {6: 0.02, 7: 0.3, 8: 0.58, 9: 0.84, 10: 1.0}[f]
        tx, ty = sx0 + (hx - sx0) * k, sy0 + (hy - sy0) * k
        if f < 10:
            vx, vy = -uy, ux
            trail = c.line(tx - ux * 18, ty - uy * 18, tx - ux * 30, ty - uy * 30)
            c.put(trail, K['violet2'], 0.8)
            g.add(trail, L['violet'], 0.4)
            for i, off in enumerate((-3, 3)):
                m = c.line(tx - ux * 8 + vx * off, ty - uy * 8 + vy * off, tx - ux * 20 + vx * off,
                           ty - uy * 20 + vy * off)
                c.put(m, K['steel3'], 0.7)
        paint_arrow(c, g, tx, ty, inc)
    elif 11 <= f <= 17:
        a = f - 10
        tx = hx + 2.2 * a
        ty = hy - 2.4 * a + 0.6 * a * a
        spin = inc + math.pi + 0.95 * a
        fade = 1.0 if a <= 3 else max(0.0, 1 - (a - 3) / 4.0)
        paint_arrow(c, g, tx, ty, spin, a=fade, curse=fade * 0.6)
    sparks(c, g, f, hit, hx, hy, 61, 6, (0.5, 1.2), 6, (K['violet4'], K['violet3'], K['violet2']), L['violet'],
           gravity=-0.05, spread=(0, 2 * math.pi), gain=0.7)            # the curse dissipating

    lab = shield_labels(c, cx, cy)
    glint = -20 + (f - 12) * 8 if 12 <= f <= 17 else None
    paint_shield(c, g, lab, cx, cy, t * 2 + (1.0 if 9 <= f <= 13 else 0),
                 rune_gain=1.0 + 1.3 * warn + 1.2 * flare, glint=glint)

    if 10 <= f <= 13:
        star4(c, g, hx, hy, {10: 9, 11: 6, 12: 4, 13: 2}[f], gain=1.4)
    # sparks glance off the barrier, away from its centre
    sparks(c, g, f, hit, hx, hy, 29, 13, (1.2, 3.0), 8, (K['cyan4'], K['cyan2'], K['cyan1']), L['cyan'],
           gravity=0.16, spread=(E_HIT_ANG - 1.1, E_HIT_ANG + 1.1))
    sparks(c, g, f, hit, hx, hy, 43, 5, (0.8, 1.8), 6, (K['window'], K['steel3'], K['steel2']), L['warm'],
           gravity=0.2, spread=(E_HIT_ANG - 0.6, E_HIT_ANG + 0.9), gain=0.6)

    motes(c, g, t, 53, 9, (2, 6, 86, 56), K['cyan3'], K['steel3'], L['cyan'], gain=0.55)
    vignette(c, 0.75)
    return c, g


# ---------------------------------------------------------------------------------------- Chispa Ígnea
C_HOME = (24.0, 41.0)        # base of the floating flame
C_LAND = (66.0, 55.0)


def flame_labels(c, x, y, h, w, sway, t, ph):
    """Pixel flame whose round base is centred at (x, y), tip `h` px above. Labels 2..5 = red, orange, yellow,
    white core (inner flames sit lower and sway less); 1 = the dark red rim just outside."""
    lab = np.zeros((c.h, c.w), int)
    if h <= 0.5 or w <= 0.3:
        return lab
    for k, (sw, sh, ss) in enumerate(((1.0, 1.0, 1.0), (0.72, 0.74, 0.75), (0.48, 0.5, 0.5), (0.26, 0.27, 0.3)),
                                     start=2):
        ww, hh = w * sw, h * sh
        yb = y + (1 - sw) * w * 0.35
        up = np.clip((yb - c.y) / hh, -1, 1)
        dx = c.x - (x + sway * ss * np.clip(up, 0, None) ** 1.5)
        base = (dx / ww) ** 2 + ((c.y - yb) / (ww * 0.85)) ** 2 <= 1.0
        wig = 1 + 0.16 * np.sin((up * 2.5 - t * 6 + ph) * math.pi * 2)
        tongue = (up >= 0) & (c.y <= yb) & (c.y >= yb - hh) & (np.abs(dx) <= ww * (1 - up) ** 0.75 * wig)
        lab[base | tongue] = k
    lab[(lab == 0) & dilate(lab > 0)] = 1
    return lab


def paint_flame(c, g, lab, gain=1.0):
    cols = {1: KM['fire0'], 2: KM['fire1'], 3: KM['fire2'], 4: KM['fire3'], 5: KM['fire5']}
    lights = {1: 0.12, 2: 0.4, 3: 0.75, 4: 1.0, 5: 1.3}
    for k in range(1, 6):
        m = lab == k
        c.put(m, cols[k], 0.7 if k == 1 else 1.0)
        g.add(m, LM['fire'] if k < 5 else LM['ember'], gain * lights[k])
    c.put((lab == 4) & (np.roll(lab, 1, axis=0) == 5), KM['fire4'])      # yellow ring just above the core


def flame(c, g, x, y, h, w, t, ph, sway_amp=1.4, gain=1.0, side=True, flicks=True):
    """A flickering flame plus two smaller side tongues and bits of flame that break off the tip and rise.
    Every period is an integer number of cycles per loop, so it loops without a seam."""
    hh = h * (1 + 0.1 * math.sin((t * 6 + ph) * math.pi * 2) + 0.05 * math.sin((t * 10 + ph * 2) * math.pi * 2))
    sway = sway_amp * math.sin((t * 4 + ph) * math.pi * 2)
    lab = flame_labels(c, x, y, hh, w, sway, t, ph)
    if side and h > 5:
        for s, k in ((-1, 0.31), (1, 0.57)):
            lh = h * (0.42 + 0.12 * math.sin((t * 8 + ph + k) * math.pi * 2))
            sl = flame_labels(c, x + s * w * 0.72, y + 0.5, lh, w * 0.42, sway * 0.6 + s * 0.8, t, ph + k)
            sl = np.where(sl > 3, 3, sl)
            lab = np.where(lab > 0, np.maximum(lab, np.where(sl > 1, sl, 0)), sl)
    if flicks and h > 6:
        for j in range(2):
            u = frac(t * 4 + ph + j * 0.5)
            fy = y - hh * (0.95 + 0.55 * u)
            fx = x + sway * (1.2 + 0.4 * u) + (j - 0.5) * 1.5
            r = max(0.0, (1 - u) * w * 0.32)
            if r > 0.45:
                m = c.disc(fx, fy, r)
                lab = np.where(m & (lab < 2), 3 if u < 0.4 else 2, lab)
                lab[(lab == 0) & dilate(m)] = 1
    paint_flame(c, g, lab, gain)
    return lab


def chispa_scene(c, g, t):
    gradient_bg(c, K['night0'], K['vio1'], K['night1'], 0.58)
    c.put(c.disc(44, 32, 30), K['night3'], 0.2)
    twinkling_stars(c, g, t, 71, 18, (2, 2, 86, 34))
    hills = c.poly([(0, 48), (12, 43), (24, 46), (36, 42), (50, 45), (62, 41), (76, 45), (88, 42), (88, 64), (0, 64)])
    c.put(hills, K['vio0'])
    c.put(hills & ~np.roll(hills, 1, axis=0), K['vio3'])
    # dead tree on the far right, ground with a few stones
    tree = c.line(80, 54, 80, 39) | c.line(81, 54, 81, 42) | c.line(80, 44, 84, 40) | c.line(81, 46, 77, 41)
    tree |= c.line(84, 40, 85, 37) | c.line(77, 41, 75, 40) | c.line(80, 39, 79, 36)
    ground = c.poly([(0, 55), (14, 54), (30, 56), (46, 55), (60, 56), (74, 55), (88, 56), (88, 64), (0, 64)])
    c.put(tree | ground, KM['earth0'])
    c.put(ground & ~np.roll(ground, 1, axis=0), KM['earth2'])
    for x in range(3, AW, 9):
        c.put(c.rect(x, 58 + (x % 3), x + 3, 58 + (x % 3)), KM['earth1'])


def ember_ring(c, g, x, y, t, alpha):
    """A small fire rune circle the flame floats over (ellipse of embers turning once per loop)."""
    if alpha <= 0.02:
        return
    ring = ellipse_ring(c, x, y, 9, 2.4, 0.5)
    c.put(ring, KM['fire1'], 0.6 * alpha)
    g.add(ring, LM['fire'], 0.15 * alpha)
    for i in range(10):
        th = 2 * math.pi * (i / 10 + t)
        m = c.pixel(x + math.cos(th) * 9, y + math.sin(th) * 2.4)
        c.put(m, KM['fire4'] if i % 5 == 0 else KM['fire3'], alpha)
        g.add(m, LM['fire'], 0.6 * alpha)


def chispa_frame(f):
    t = f / FRAMES
    c, g = Canvas(AW, AH), Canvas(AW, AH)
    chispa_scene(c, g, t)
    hx, hy = C_HOME
    lx, ly = C_LAND
    land = 16
    age = (f - land) % FRAMES                     # time since the spark landed (the fire it lit outlives the loop)

    # scorched ground under the fire
    burn = max(0.0, 1 - age / 17.0)
    if burn > 0:
        c.put(c.disc(lx, ly + 0.5, 12) & (np.abs(c.y - ly - 0.5) < 2.5), KM['fire0'], 0.45 * burn)
    c.put(c.disc(lx, ly + 0.5, 8) & (np.abs(c.y - ly - 0.5) < 1.5), KM['earth0'])

    # the home flame: hovers and flickers (0-8), gathers (9-11), flung (12-15), rekindles (18-23)
    bob = math.sin(t * 2 * math.pi * 2) * 0.8
    hl = 0.0
    H, Wd = 17.0, 5.0
    if f <= 8:
        h, w, y_ = H, Wd, hy + bob
    elif f <= 11:
        k = ease_in_out((f - 8) / 3.0)
        h, w, y_ = H - 7 * k, Wd + 1.0 * k, hy + bob * (1 - k) + 1.0 * k
        hl = k
    elif f >= 18:
        s = {18: 0.12, 19: 0.28, 20: 0.48, 21: 0.66, 22: 0.82, 23: 0.94}[f]
        h, w, y_ = H * s, Wd * (0.4 + 0.6 * s), hy + bob
    else:
        h, w, y_ = 0.0, 0.0, hy
    ember_ring(c, g, hx, hy + 4, t, 1.0 if h > 0 else 0.45)
    if 8 <= f <= 11:                              # gather: embers spiral into the flame
        rng = np.random.default_rng(83)
        k = (f - 8) / 3.0
        for i in range(9):
            a0 = rng.uniform(0, 2 * math.pi) + k * 1.6
            r0 = rng.uniform(9, 15) * (1 - ease_in_out(k)) + 2
            m = c.pixel(hx + math.cos(a0) * r0, hy - 4 + math.sin(a0) * r0)
            c.put(m, KM['fire4'] if i % 2 else KM['fire3'])
            g.add(m, LM['ember'], 0.9)
    if h > 0.6:
        flame(c, g, hx, y_, h, w, t, 0.0, gain=1.0 + 0.8 * hl, flicks=f <= 8 or f >= 21)
    if f == 18:
        star4(c, g, hx, hy - 1, 3, core=KM['fire5'], col=KM['fire4'], edge=KM['fire3'], light=LM['fire'], gain=1.0,
              diag=False)

    # flight: a fireball lobbed in an arc (quadratic curve), shedding embers and smoke
    p0, p1, p2 = (hx, hy - 4), (44.0, -4.0), (lx, ly - 2)

    def arc(u):
        a, b, cc = (1 - u) ** 2, 2 * u * (1 - u), u * u
        return a * p0[0] + b * p1[0] + cc * p2[0], a * p0[1] + b * p1[1] + cc * p2[1]

    if 12 <= f <= 15:
        u = {12: 0.16, 13: 0.42, 14: 0.68, 15: 0.9}[f]
        bx, by = arc(u)
        qx, qy = arc(u - 0.05)
        dl = math.hypot(bx - qx, by - qy)
        ux, uy = (bx - qx) / dl, (by - qy) / dl
        vx, vy = -uy, ux
        dx, dy = c.x - bx, c.y - by
        s = -(dx * ux + dy * uy)
        v = np.abs(dx * vx + dy * vy)
        length = 13.0
        wig = 1 + 0.25 * np.sin((s / 4.0 - t * 12) * math.pi * 2)
        wdt = 4.0 * np.clip(1 - s / length, 0, 1) ** 0.85 * wig
        tail = (s >= -4.0) & (s <= length) & (v <= wdt + 0.3)
        head_d = np.hypot(dx, dy)
        lab = np.zeros((c.h, c.w), int)
        lab[tail | (head_d <= 4.2)] = 2
        lab[(tail & (v <= wdt * 0.7) & (s < length * 0.7)) | (head_d <= 3.6)] = 3
        lab[(tail & (v <= wdt * 0.42) & (s < length * 0.4)) | (head_d <= 2.7)] = 4
        lab[head_d <= 1.6] = 5
        lab[(lab == 0) & dilate(lab > 0)] = 1
        paint_flame(c, g, lab, gain=1.4)
    rng = np.random.default_rng(91)
    for i in range(16):                                        # embers and smoke left along the arc
        u = rng.uniform(0.08, 0.95)
        born = 12 + int(u * 4)
        a = f - born
        if not 0 < a <= 6:
            continue
        x, y = arc(u)
        x += rng.uniform(-2, 2) + a * 0.2
        y += rng.uniform(-2, 2) + a * 0.6
        if i % 3 == 0:
            m = c.disc(x, y - a * 1.0, 0.8 + a * 0.35)
            c.put(m, KM['smoke1'], 0.7 * (1 - a / 7))
        else:
            m = c.pixel(x, y)
            k = 1 - a / 7
            c.put(m, KM['fire4'] if a < 2 else KM['fire2'] if a < 4 else KM['fire1'])
            g.add(m, LM['fire'], 0.9 * k)

    # landing: flash, star, a fan of flame tongues, sparks thrown up
    if f == land:
        c.put(c.disc(lx, ly - 1, 6.5), KM['fire3'], 0.8)
        c.put(c.disc(lx, ly - 1, 3.8), KM['fire5'])
        g.add(c.disc(lx, ly - 1, 7), LM['fire'], 1.3)
    if land <= f <= land + 3:
        star4(c, g, lx, ly - 2, {16: 10, 17: 7, 18: 4, 19: 2}[f], core=KM['fire5'], col=KM['fire4'],
              edge=KM['fire3'], light=LM['fire'], gain=1.4)
    if land <= f <= land + 4:
        k = (f - land) / 5.0
        for j, a0 in enumerate((-1.0, -0.5, 0.0, 0.5, 1.0)):
            dist = 3 + 10 * ease_out(min(1.0, (f - land + 1) / 3.0))
            fx_ = lx + math.sin(a0) * dist
            fy_ = ly - 1 - math.cos(a0) * dist * 0.5
            fh = (7 - abs(j - 2) * 1.6) * (1 - k)
            if fh > 0.8:
                flame(c, g, fx_, fy_, fh, 1.8 * (1 - k * 0.5), t, j * 0.37, sway_amp=0.6 + a0, gain=1.2, side=False,
                      flicks=False)
    sparks(c, g, f, land, lx, ly - 2, 97, 16, (1.4, 3.4), 9, (KM['fire5'], KM['fire3'], KM['fire2']), LM['fire'],
           gravity=0.2, spread=(0.35, math.pi - 0.35))

    # the fire it lit (2 stacks of Fuego: two flames) burns on and dies down; smoke and embers rise from it
    if 1 <= age <= 16:
        grow = min(1.0, age / 3.0)
        fade = 1.0 if age <= 8 else max(0.0, 1 - (age - 8) / 8.0)
        for j, (ox, ph) in enumerate(((-5.0, 0.21), (4.5, 0.63))):
            fh = (12.0 - j * 2.5) * grow * fade
            if fh > 0.8:
                flame(c, g, lx + ox, ly - 1.5, fh, 3.4 * (0.55 + 0.45 * fade), t, ph, sway_amp=1.0)
    rng = np.random.default_rng(101)
    for i in range(6):
        born = land + 2 + i * 2
        a = (f - born) % FRAMES
        if a > 10:
            continue
        x = lx + rng.uniform(-5, 5) + a * 0.4
        y = ly - 12 - a * 1.6
        k = 1 - a / 11
        m = c.disc(x, y, 1.0 + a * 0.3)
        c.put(m, KM['smoke2'] if a < 4 else KM['smoke1'], 0.55 * k)
    motes(c, g, t, 107, 7, (lx - 10, 20, lx + 10, ly - 4), KM['fire4'], KM['fire3'], LM['ember'], gain=0.8)
    motes(c, g, t, 109, 5, (hx - 8, 12, hx + 8, hy - 8), KM['fire4'], KM['fire2'], LM['ember'], gain=0.7)
    motes(c, g, t, 113, 5, (2, 6, 86, 50), K['cyan3'], K['violet3'], L['cyan'], gain=0.45)
    vignette(c, 0.72)
    return c, g


ILLUSTRATIONS = {
    'proyectil': proyectil_frame,
    'egida': egida_frame,
    'chispa_ignea': chispa_frame,
}


def render(card_id):
    fn = ILLUSTRATIONS[card_id]
    base, glow = [], []
    for f in range(FRAMES):
        b, gl = fn(f)
        base.append(b)
        glow.append(gl)
    return base, glow
