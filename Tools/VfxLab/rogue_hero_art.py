"""WARNING (user rule, 2026-10-30): the arm and the weapon drawn as separate pieces over the body (the sleeve tube and the
rigid weapon below) are the pieces technique the user rejected. Do not reuse this construction for any character;
see docs/art-style.md section 3.1. This sheet is pending a redraw with every pose as one complete figure.

La Picara (hero) in the approved art style (docs/art-style.md): the hooded rogue of the Esquiva card grown into the
combat character, with every combat animation built from the vocabulary of the rogue's basic cards.

Construction (docs/code-drawn-sprites.md section 10, the method the user approved; NEVER by pieces):
  * The whole figure is ONE silhouette scanned row by row (`scan`): every pixel row gets its local height `d` above the
    soles (crouch and squash undone), one centre line (hips + lean + nod) and the extents of that row: hood dome with its
    trailing tip, mantle, torso, tunic, and below the tunic two leg spans per row anchored to the floor. Behind the body,
    in the same scan, the cloak fills out to its waving back edge and the scarf tails stream back.
  * Hood, face, scarf, cloak, belt, tunic, leggings and boots are ZONES painted inside that one mass (labels), with one
    light model, Bayer dither and one outline around the whole mass. A test pins that the mass is one connected region.
  * Only the dagger arm and the daggers are drawn on top (the arm is a tapered sleeve that comes out from under the
    mantle, with an ink rim where it crosses the body).
  * Animation deforms the mass (offset, lean, crouch, squash/stretch, nod, cloth wind and sweep, stepping feet, kneel).

Look (the cards'): dark violet silhouette with a cyan rim on the front/top edges and a violet bounce behind, purple scarf,
glowing cyan eyes in a void, rune dagger with a light wave, 4-point stars, rising motes, sparks, speed lines, afterimages
and crescents. Two layers per frame: paint (readable alone) + emissive light (only the magic).

Native cells of 96x96 (soles on row 93, facing right), 12 fps like the card illustrations:
  idle 24 (2 s seamless loop), attack 10 (Punalada: strike on frame 4), guard 9 (Esquiva), hurt 7, cast 12 (Finta:
  release on frame 6), death 17 (held). Every action except death ends on idle frame 0, pixel for pixel.
"""
import math
from dataclasses import dataclass, fields

import numpy as np

from pixelkit import Canvas, hexc, mix, dilate, erode, shift
from rogue_art import K, L, star4, ease_out, ease_in_out, frac, outline_mask, dagger_labels, paint_dagger, crescent

CELL = 96
GROUND = 93           # row of the soles
ROOT_X = 40           # x of the hips at rest (room in front for the thrust)
FPS = 12

ANIMATIONS = [  # name, frames, loop
    ('idle', 24, True), ('attack', 10, False), ('guard', 9, False), ('hurt', 7, False), ('cast', 12, False),
    ('death', 17, False),
]
STRIKE_FRAME = 4
CAST_FRAME = 6
DAGGER_S = 0.46       # dagger scale (rogue_art daggers are 40 units long at s = 1)


def durations_ms(n):
    """12 fps in whole milliseconds (83/84/83...), so n frames last exactly n / 12 s (rounded)."""
    return [round((i + 1) * 1000 / FPS) - round(i * 1000 / FPS) for i in range(n)]


# ------------------------------------------------------------------------------------------------ palette (zones)
RAMP = {  # dark -> light
    'cloak': [K['ink'], K['body0'], K['body1'], K['body2'], hexc('#30204a')],
    'hood': [K['body0'], K['body1'], K['body2'], hexc('#33234f'), hexc('#45306a')],
    'tunic': [hexc('#0d0b1c'), K['night1'], K['night2'], K['night3']],
    'legs': [K['ink'], hexc('#100c1d'), hexc('#1a1530'), hexc('#262044')],
    'boot': [hexc('#140c16'), hexc('#24151f'), hexc('#3a2633'), hexc('#5c3f4c')],
    'scarf': [K['scarf0'], K['scarf1'], K['scarf2'], K['violet3']],
    'sleeve': [K['body0'], K['body1'], K['body2'], hexc('#33234f')],
    'glove': [K['grip0'], K['grip1'], K['grip2']],
}
BELT = [hexc('#241a38'), hexc('#3a2a55'), hexc('#54407a')]
GOLD = [K['gold1'], hexc('#d9b26a')]

# labels of the mass
EMPTY, CLOAK, TAIL, BLEG, FLEG, BOOT_B, BOOT_F, TUNIC, BELT_L, TORSO, MANTLE, SCARF, HOOD, FACE = range(14)
UPPER = (TORSO, MANTLE, SCARF, HOOD, FACE)

H_HIP = 26.0          # design heights (px above the soles, standing)
D_TOP = 66.0


# ------------------------------------------------------------------------------------------------ pose
@dataclass
class Pose:
    x: float = 0.0            # hips offset from ROOT_X
    y: float = 0.0            # whole figure offset (negative = up)
    sink: float = 2.0         # crouch: hips lowered (px); the legs fold
    stretch: float = 1.0      # vertical scale of the upper body (squash < 1 < stretch)
    lean: float = 3.0         # px the top of the hood is ahead of the hips
    nod: float = 0.0          # extra px of the hood forward (+) / back (-)
    ff: float = 10.0          # front foot x (rel. ROOT_X), back foot x
    bf: float = -9.0
    flift: float = 0.0        # foot lifted (px)
    blift: float = 0.0
    fknee: float = 2.5        # knee bend (px, forward)
    bknee: float = 1.5
    phase: float = 0.0        # cloth phase (rad, periodic use only)
    wind: float = 1.0         # cloth wave amplitude
    sweep: float = 0.0        # cloth streaming back (dash forward) 0..1
    tail: float = 1.0         # scarf tail length factor (they curl in when she dashes backwards)
    hx: float = 13.0          # front hand (rel. hips x, height above soles)
    hh: float = 32.0
    bow: float = 2.5          # elbow bow (px, perpendicular, + = down)
    fdag: float = -32.0       # front dagger angle (deg, 0 = right, -90 = up)
    bdag: float = 140.0       # back dagger angle (reverse grip, pointing back)
    eyes: float = 1.0         # eye light
    blink: float = 0.0        # 1 = eyes closed to a line
    runes: float = 1.0        # rune light on the daggers
    flash: float = 0.0        # white hit flash
    kneel: float = 0.0        # death: the mass sinks and the cloak pools
    fade: float = 0.0         # death: eyes and runes die out
    drop: float = 0.0         # the head bows: hood rows sink into the shoulders (px)


def lerp(a, b, u):
    return a + (b - a) * u


def blend(p, q, u):
    return Pose(**{f.name: lerp(getattr(p, f.name), getattr(q, f.name), u) for f in fields(Pose)})


def with_(p, **kw):
    d = {f.name: getattr(p, f.name) for f in fields(Pose)}
    d.update(kw)
    return Pose(**d)


def smooth(e0, e1, d):
    return np.clip((d - e0) / (e1 - e0), 0.0, 1.0)


# ------------------------------------------------------------------------------------------------ the one mass
class Figure:
    """Row scan of a pose: label map of the single mass plus the anchor points the painter needs."""

    def __init__(self, c, p):
        self.p = p
        gx = ROOT_X + p.x
        self.gx = gx
        base = GROUND + p.y                             # y of the soles' bottom edge (the outline sits on row GROUND)
        hl = base - c.y                                 # height above the soles of every pixel centre
        sink = p.sink + p.kneel * 13.0
        hip = H_HIP - sink
        # local design height: the upper body is squashed/stretched above the hips, the legs fold below them
        d = np.where(hl >= hip, H_HIP + (hl - hip) / p.stretch, hl * H_HIP / max(hip, 1.0))
        if p.drop:
            d = np.where(d > 41.0, d + p.drop * smooth(41.0, 41.0 + p.drop * 2.0, d), d)

        lean = p.lean * smooth(H_HIP, D_TOP, d) ** 1.15
        nod = p.nod * smooth(44.0, D_TOP, d)
        cx = gx + lean + nod                             # centre line of each row
        u = c.x - cx                                     # signed distance from the centre line (+ = front)
        self.cx_at = lambda dd: gx + p.lean * min(1, max(0, (dd - H_HIP) / (D_TOP - H_HIP))) ** 1.15 \
            + p.nod * min(1, max(0, (dd - 44) / (D_TOP - 44)))
        def y_at(dd):
            if p.drop and dd > 41.0:                      # undo the head drop (d grows by drop * smooth)
                lo, hi = 41.0, dd
                for _ in range(30):
                    mid = (lo + hi) / 2
                    if mid + p.drop * min(1, max(0, (mid - 41.0) / (p.drop * 2.0))) < dd:
                        lo = mid
                    else:
                        hi = mid
                dd = (lo + hi) / 2
            return base - (hip + (dd - H_HIP) * p.stretch if dd >= H_HIP else dd * hip / H_HIP)
        self.y_at = y_at

        lab = np.zeros((c.h, c.w), np.int8)
        ph, wind, sw = p.phase, p.wind, p.sweep

        # ---- cloak (behind everything): from the shoulders down to a ragged, waving hem
        k = smooth(45.0, 15.0, d)                        # 0 at the shoulders, 1 at the hem
        wave = wind * 1.8 * np.sin(ph + (45.0 - d) * 0.21) * k ** 1.2
        back = 8.0 + (45.0 - d) * 0.30 + wave + sw * 13.0 * k ** 1.1
        hem = 15.0 - p.kneel * 15.0 + 2.2 * np.abs(np.sin((c.x - gx) * 0.42 + ph * 0.5)) + sw * 9.0 * smooth(-6, -22, u)
        cloak = (d <= 45.0) & (d >= hem) & (u >= -back) & (u <= 2.0) & (hl > 0)
        if p.kneel > 0:                                   # the cloak pools on the floor around her
            pool = (hl > 0) & (hl < 2.0 + 7.0 * p.kneel) & (np.abs(c.x - (gx - 2)) < 6.0 + 12.0 * p.kneel - (hl * 1.1))
            cloak |= pool & (d <= 45.0)
        lab[cloak] = CLOAK

        # ---- scarf tails, streaming back from the knot at the nape
        knot_x = self.cx_at(46.5) - 6.0
        for length, dd0, ph0, th0 in ((22.0, 47.6, 0.0, 1.9), (15.0, 42.6, 1.9, 1.5)):
            Lt = (length + sw * 6.0) * p.tail
            s = knot_x - c.x                              # distance behind the knot
            q = np.clip(s / Lt, 0, 1)
            droop = (5.0 * (1 - sw) + 9.0 * (1 - wind)) * q ** 1.4
            dc = dd0 - droop + (wind * 2.2 + sw * 0.8) * np.sin(ph * 2 + ph0 - s * 0.36) * q ** 0.8
            th = th0 * (1 - 0.55 * q)
            # thicker where the tail is steep, so every column overlaps the next one (one connected mass)
            q1 = np.clip((s + 1) / Lt, 0, 1)
            dc1 = dd0 - (5.0 * (1 - sw) + 9.0 * (1 - wind)) * q1 ** 1.4 \
                + (wind * 2.2 + sw * 0.8) * np.sin(ph * 2 + ph0 - (s + 1) * 0.36) * q1 ** 0.8
            th = np.maximum(th, (np.abs(dc1 - dc) + 1.0) / 2 + 0.05)
            tail = (s >= -1) & (s <= Lt) & (np.abs(d - dc) <= th)
            lab[tail & (lab == EMPTY)] = TAIL
            lab[tail & (lab == CLOAK)] = TAIL

        # ---- legs: two spans per row, from the hips to the planted feet (not shown when kneeling)
        if p.kneel < 0.4:
            for foot, lift, bend, hip_off, lb, lbt in (
                    (p.bf, p.blift, p.bknee, -2.6, BLEG, BOOT_B), (p.ff, p.flift, p.fknee, 2.4, FLEG, BOOT_F)):
                fx = ROOT_X + foot
                hx = gx + hip_off
                dl = (hl - lift) * H_HIP / max(hip - lift, 1.0)      # leg-local height, 0 at the sole
                kk = np.clip(dl / H_HIP, 0, 1)
                bend_now = bend + sink * 0.35
                lc = fx + (hx - fx) * kk + bend_now * np.sin(math.pi * kk)
                v = c.x - lc
                half_b = np.select([dl < 3.2, dl < 8.2, dl < 10.0, dl < 14.5],
                                   [2.7, 2.6, 3.1, 2.6 + 0.5 * np.sin(np.clip((dl - 10) / 4.5, 0, 1) * math.pi)],
                                   2.6 + (dl - 14.5) * 0.09)
                toe = np.where(dl < 2.0, 3.6, np.where(dl < 3.2, 2.2, 0.0))
                half_f = np.select([dl < 3.2, dl < 8.2, dl < 10.0], [2.7 + toe, 2.5, 3.1], 2.4 + np.clip(dl - 14, 0, None) * 0.1)
                leg = (dl >= 0) & (dl <= H_HIP + 1) & (v >= -half_b) & (v <= half_f) & (hl > 0)
                boot = leg & (dl < 10.0)
                put = leg & ((lab == EMPTY) | (lab == CLOAK) | (lab == TAIL) | (lab == BLEG) | (lab == BOOT_B))
                lab[put & ~boot] = lb
                lab[put & boot] = lbt

        # ---- body: tunic, belt, torso, mantle, hood, one profile per row
        front = np.full(d.shape, -99.0)
        backx = np.full(d.shape, -99.0)
        zone = np.zeros(d.shape, np.int8)

        def band(d0, d1, f_ext, b_ext, z):
            m = (d >= d0) & (d < d1)
            front[...] = np.where(m, f_ext, front)
            backx[...] = np.where(m, b_ext, backx)
            zone[...] = np.where(m, z, zone)

        e = smooth(30.0, 42.0, d)
        tunic_lo = 19.5 - p.kneel * 6
        band(tunic_lo, 29.0, 5.6 + (29.0 - d) * 0.22, 6.4 + (29.0 - d) * 0.30, TUNIC)
        band(29.0, 31.5, 5.6, 6.3, BELT_L)
        band(31.5, 42.5, 5.4 + 1.3 * np.sin(e * math.pi / 2), 6.2 + 2.4 * e ** 1.2, TORSO)
        band(42.5, 47.6, 7.2 - smooth(42.5, 47.6, d) * 0.6, 8.4 - smooth(42.5, 47.6, d) * 1.6, MANTLE)
        hc = 56.0
        r = 10.2
        half = np.sqrt(np.clip(r * r - (d - hc) ** 2, 0, None))
        brim = 1.2 * np.exp(-((d - 58.5) / 2.0) ** 2)
        # the hood's trailing tip: a point that leans back and a little down, swaying with the cloth
        tip_d = 60.0 + 0.8 * math.sin(ph) * wind - sw * 1.2
        tip_len = 16.5 + 1.3 * math.sin(ph + 1.0) * wind + sw * 5.0
        wup, wdn = 4.6, 7.8                              # from the top-back of the dome to the point, then down
        tri = np.where(d >= tip_d, 1 - (d - tip_d) / wup, 1 - (tip_d - d) / wdn)
        tip = np.clip(tri, 0, 1) ** 1.05 * tip_len
        hood_f = half * 0.98 + 0.6 + brim
        hood_b = np.maximum(half - 0.6, tip)
        band(47.6, hc + r, hood_f, hood_b, HOOD)
        # the hood dome overlaps the mantle top (round shoulders of the hood)
        over = (d >= 45.5) & (d < 47.6)
        front[...] = np.where(over, np.maximum(front, hood_f), front)
        backx[...] = np.where(over, np.maximum(backx, hood_b), backx)

        # ragged tunic hem: two tails (front and back) and a notch at the side
        hem_t = tunic_lo + 1.6 * (1 + np.cos(u * 0.62)) * 0.5 * (np.abs(u) < 5.2)
        body = (u <= front) & (u >= -backx) & (zone > 0) & ~((zone == TUNIC) & (d < hem_t)) & (hl > 0)
        lab[body] = zone[body]

        # face: the void inside the hood opening; the scarf covers its lower half
        fcx = self.cx_at(54.5) + 4.6
        face = (((c.x - fcx) / 5.1) ** 2 + ((d - 54.4) / 5.6) ** 2 <= 1) & (u <= hood_f - 1.7) & (lab == HOOD)
        lab[face] = FACE
        scarf = (d >= 43.6) & (d < 50.2) & (u >= -6.6) & (u <= front + 1.0) & ((lab != EMPTY) | ((u > 0) & (d > 44)))
        scarf &= (u <= np.where(d < 47.6, 8.0, hood_f + 0.2))
        lab[scarf & (lab != EMPTY) | scarf & (u > 0)] = SCARF

        # one mass: specks of cloth that a thin, steep tip leaves behind are dropped (4-connected to the torso)
        m = lab != EMPTY
        reach = (lab == TORSO) | (lab == HOOD)
        while True:
            grown = dilate(reach) & m
            if (grown == reach).all():
                break
            reach = grown
        lab[m & ~reach] = EMPTY
        self.lab = lab
        self.d, self.u, self.hl = d, u, hl
        # anchors
        self.shoulder = (self.cx_at(41.5) + 2.5, self.y_at(41.5))
        self.eyes = [(fcx - 2.2, self.y_at(55.4)), (fcx + 1.8, self.y_at(55.4))]
        self.hand = (gx + p.hx, base - p.hh)
        self.back_hand = (self.cx_at(30) - 7.5, self.y_at(29.0))
        self.chest = (self.cx_at(37) + 4.0, self.y_at(37))
        self.brooch = (self.cx_at(45.5) + 5.4, self.y_at(45.6))

    def mask(self):
        return self.lab != EMPTY


# ------------------------------------------------------------------------------------------------ painting
def tone_index(c, light, n, amp=0.55):
    """Ramp index of every pixel from a 0..1 light value, ordered dither between steps."""
    return np.clip(np.floor(light * (n - 1) + 0.5 + (c.B - 0.5) * amp), 0, n - 1).astype(int)


def paint_zone(c, m, ramp, light, amp=0.55):
    idx = tone_index(c, light, len(ramp), amp)
    for i, col in enumerate(ramp):
        c.put(m & (idx == i), col)


def paint_mass(c, g, fig, t):
    p = fig.p
    lab, d, u = fig.lab, fig.d, fig.u
    m = lab != EMPTY
    c.put(outline_mask(m), K['ink'])
    # one light for the whole mass: key light from the upper left, darker low, soft vertical folds
    fold = 0.10 * np.cos(u * 1.15 + d * 0.18 + 0.4 * math.sin(p.phase))
    base_light = 0.70 - 0.032 * u - 0.010 * (D_TOP - d) + fold

    hood = lab == HOOD
    paint_zone(c, hood, RAMP['hood'], base_light + 0.10 * (d > 58))
    # brim edge around the face opening
    face = lab == FACE
    brim = hood & dilate(face, True)
    c.put(brim, RAMP['hood'][3])
    c.put(face, K['body0'])
    c.put(face & ~shift(face, 0, -1) & shift(face, 0, 1), hexc('#05030a'))   # darkest line where the scarf overlaps
    c.put(face & (d < 51.5), hexc('#1a1030'), 0.6)                           # bounce from the scarf

    mantle = lab == MANTLE
    paint_zone(c, mantle, RAMP['hood'], base_light + 0.05)
    c.put(mantle & ~shift(mantle, 0, -1) & ~face, RAMP['hood'][0])          # cast shadow under the mantle edge
    torso = lab == TORSO
    paint_zone(c, torso, RAMP['tunic'], base_light + 0.05)
    # leather strap across the chest
    strap = torso & (np.abs((u - 1.0) - (d - 36.5) * 0.85) < 0.75)
    c.put(strap, BELT[1])
    c.put(strap & (u < 0), BELT[2])
    belt = lab == BELT_L
    c.put(belt, BELT[1])
    c.put(belt & (d > 30.6), BELT[2])
    c.put(belt & (d < 29.6), BELT[0])
    buckle = belt & (u > 1.3) & (u < 3.4)
    c.put(buckle, GOLD[0])
    c.put(buckle & (d > 30.2) & (u < 2.4), GOLD[1])
    g.add(buckle & (d > 30.2) & (u < 2.4), L['warm'], 0.12)
    tunic = lab == TUNIC
    paint_zone(c, tunic, RAMP['tunic'], base_light - 0.05 + 0.08 * (np.abs(np.sin(u * 0.8)) < 0.3))
    c.put(tunic & ~shift(tunic, 0, -1) & (lab != EMPTY), RAMP['tunic'][0])

    cloak = lab == CLOAK
    paint_zone(c, cloak, RAMP['cloak'], base_light - 0.08 + 0.12 * (u < -9))
    # lining: the inner violet shows where the cloak's back edge turns
    lining = cloak & ~shift(cloak, 0, -1) & (d < 30)
    c.put(lining, K['vio2'])

    for lb, z in ((BLEG, 'legs'), (FLEG, 'legs')):
        mm = lab == lb
        paint_zone(c, mm, RAMP[z], base_light - (0.18 if lb == BLEG else 0.04) - 0.012 * u)
    for lb in (BOOT_B, BOOT_F):
        mm = lab == lb
        paint_zone(c, mm, RAMP['boot'], 0.55 - (0.2 if lb == BOOT_B else 0) + 0.25 * (d > 8.2) - 0.03 * u)
        c.put(mm & (d < 1.2), RAMP['boot'][0])
    # separate the back leg from the front one with a 1 px shadow line
    fl = (lab == FLEG) | (lab == BOOT_F)
    c.put(((lab == BLEG) | (lab == BOOT_B)) & dilate(fl), K['ink'])

    tails = lab == TAIL
    scarf = lab == SCARF
    for mm in (tails, scarf):
        paint_zone(c, mm, RAMP['scarf'], 0.62 - 0.02 * u + 0.06 * np.sin(d * 1.3 + c.x * 0.3), amp=0.4)
        c.put(mm & ~shift(mm, 0, 1), RAMP['scarf'][2])        # top highlight
        c.put(mm & ~shift(mm, 0, -1), RAMP['scarf'][0])       # bottom shadow
    c.put(scarf & ~shift(scarf, 0, 1) & (u > 2), RAMP['scarf'][3])
    # scarf/hood/body separations (1 px ink) so the zones read
    c.put(tails & dilate(cloak) & ~shift(tails, 0, 1), RAMP['scarf'][3])

    # rim light: cyan on the front and top edges, violet bounce on the back edges (the card figure)
    body = m
    # top edges only where the surface faces up (not the steps of an edge that recedes backwards)
    top = ~shift(body, 0, 1) & shift(body, 1, 0)
    rim_c = body & (~shift(body, -1, 0) | top) & (lab != TAIL) & (lab != SCARF)
    rim_v = body & ~shift(body, 1, 0) & ~rim_c
    c.put(rim_v, K['rimv'], 0.8)
    rim_gain = 0.5 * (1 - p.fade)
    low = (d < 24.0) & (lab != CLOAK)
    c.put(rim_c & ~low, mix(K['rimc'], K['body2'], p.fade))
    c.put(rim_c & low, mix(K['cyan1'], K['body2'], p.fade))
    g.add(rim_c & ~low, L['cyan'], rim_gain)
    g.add(rim_c & low, L['cyan'], rim_gain * 0.25)
    return m


def paint_eyes(c, g, fig, p):
    k = p.eyes * (1 - p.fade)
    if k <= 0.02:
        return
    for i, (ex, ey) in enumerate(fig.eyes):
        h = 1 if p.blink > 0.5 else 2
        for j in range(h):
            mm = c.pixel(ex, ey + j - (0 if h == 2 else -0.5))
            mm &= fig.lab == FACE
            c.put(mm, mix(K['cyan1'], K['cyan4'], min(1, k)))
            g.add(mm, L['cyan'], 1.4 * k)
        if p.eyes > 1.3 and i == 1:                      # flare: a short streak trailing back
            mm = c.pixel(ex - 1, ey) & (fig.lab == FACE)
            c.put(mm, K['cyan2'])
            g.add(mm, L['cyan'], 0.6 * k)


def brooch(c, g, fig, p):
    x, y = fig.brooch
    m = c.pixel(x, y) | c.pixel(x + 1, y)
    m &= (fig.lab == SCARF)
    c.put(dilate(m) & ~m & (fig.lab == SCARF), K['guard0'])
    c.put(m, K['cyan3'])
    g.add(m, L['cyan'], 0.8 * p.runes * (1 - p.fade))


# ------------------------------------------------------------------------------------------------ arm and daggers
def bezier(p0, p1, p2, n):
    out = []
    for i in range(n + 1):
        t = i / n
        a, b, cc = (1 - t) ** 2, 2 * (1 - t) * t, t * t
        out.append((a * p0[0] + b * p1[0] + cc * p2[0], a * p0[1] + b * p1[1] + cc * p2[1], t))
    return out


def sleeve(c, a, b, bow, r0, r1):
    """Tapered sleeve from a to b along a quadratic Bezier (each pixel keeps its nearest axis sample)."""
    mx, my = (a[0] + b[0]) / 2, (a[1] + b[1]) / 2
    dx, dy = b[0] - a[0], b[1] - a[1]
    ln = max(1e-6, math.hypot(dx, dy))
    nx, ny = -dy / ln, dx / ln
    if ny < 0:
        nx, ny = -nx, -ny                                 # the elbow bows downwards
    ctrl = (mx + nx * bow, my + ny * bow)
    pts = bezier(a, ctrl, b, max(8, int(ln * 2.5)))
    best = np.full((c.h, c.w), 1e9)
    tt = np.zeros((c.h, c.w))
    for (x, y, t) in pts:
        dd = np.hypot(c.x - x, c.y - y)
        sel = dd < best
        best = np.where(sel, dd, best)
        tt = np.where(sel, t, tt)
    rr = r0 + (r1 - r0) * tt
    return best <= rr, tt


def draw_arm(c, g, fig, p):
    """The dagger arm (drawn over the mass): a sleeve from under the mantle to the gloved fist."""
    a = fig.shoulder
    b = fig.hand
    arm, tt = sleeve(c, a, b, p.bow, 2.7, 2.0)
    c.put(outline_mask(arm), K['ink'])
    glove = arm & (tt > 0.72)
    sl = arm & ~glove
    light = 0.72 - 0.05 * (c.y - b[1]) - 0.25 * tt
    paint_zone(c, sl, RAMP['sleeve'], light)
    paint_zone(c, glove, RAMP['glove'], 0.55 - 0.06 * (c.y - b[1]))
    c.put(glove & ~shift(glove, 0, 1), RAMP['glove'][2])
    cuff = arm & (np.abs(tt - 0.72) < 0.06)
    c.put(cuff, BELT[2])
    # cyan rim on the top edge of the sleeve (same light as the mass)
    rim = arm & ~shift(arm, 0, 1) & ~glove
    c.put(rim, K['rimc'])
    g.add(rim, L['cyan'], 0.45 * (1 - p.fade))
    return arm


def mantle_over(c, g, fig, p, arm):
    """The mantle covers the root of the arm (no shoulder joint), with a 1 px cast shadow on the sleeve."""
    lab = fig.lab
    over = arm & ((lab == MANTLE) | (lab == SCARF) | (lab == HOOD)) & (fig.d > 42.0)
    if not over.any():
        return
    sub = Canvas(c.w, c.h)
    sub.B = c.B
    paint_zone(sub, over & (lab == MANTLE), RAMP['hood'], 0.62 - 0.03 * fig.u)
    paint_zone(sub, over & (lab == HOOD), RAMP['hood'], 0.62 - 0.03 * fig.u)
    paint_zone(sub, over & (lab == SCARF), RAMP['scarf'], 0.6, amp=0.4)
    c.paste(sub)
    edge = arm & ~over & shift(over, 0, 1)
    c.put(edge, K['ink'])


def dagger_at(c, g, hand, deg, p, t, s=DAGGER_S, glint=None, ghost=None, gain=None):
    """A rune dagger held at `hand` (grip centre), pointing at `deg`. Returns the tip."""
    a = math.radians(deg)
    off = 8.85 * s
    gx, gy = hand[0] + math.cos(a) * off, hand[1] + math.sin(a) * off
    lab, u, v = dagger_labels(c, gx, gy, a, s=s)
    paint_dagger(c, g, lab, u, t * 2, glint, rune_gain=(p.runes if gain is None else gain) * (1 - p.fade), ghost=ghost)
    return gx + math.cos(a) * 40 * s, gy + math.sin(a) * 40 * s, lab


def fist(c, g, fig):
    x, y = fig.hand
    m = c.disc(x, y, 2.05)
    paint_zone(c, m, RAMP['glove'], 0.75 - 0.18 * (c.y - y))
    c.put(m & ~shift(m, 0, 1), RAMP['glove'][2])
    return m


# ------------------------------------------------------------------------------------------------ whole figure
def draw_figure(c, g, p, t, glint=None, hold=True, back_dagger=True):
    """Back dagger, the mass, eyes, then the dagger arm and its dagger. Returns (figure, tip, mask of everything drawn)."""
    fig = Figure(c, p)
    drawn = np.zeros((c.h, c.w), bool)
    if back_dagger and p.kneel < 0.3:
        *_, lab = dagger_at(c, g, fig.back_hand, p.bdag, p, t, s=DAGGER_S * 0.92, gain=p.runes * 0.6)
        drawn |= dilate(lab > 0)
    m = paint_mass(c, g, fig, t)
    drawn |= dilate(m)
    paint_eyes(c, g, fig, p)
    brooch(c, g, fig, p)
    arm = draw_arm(c, g, fig, p)
    mantle_over(c, g, fig, p, arm)
    drawn |= dilate(arm)
    tip = None
    if hold:
        tx, ty, lab = dagger_at(c, g, fig.hand, p.fdag, p, t, glint=glint)
        drawn |= dilate(lab > 0)
        tip = (tx, ty)
    fist(c, g, fig)
    if p.flash > 0:
        flash(c, drawn & (c.px[..., 3] > 0.5), p.flash)
    return fig, tip, drawn


def flash(c, m, amount):
    """Hit flash: the figure's colours pushed towards a violet-white (solid, so it reads as one bright silhouette)."""
    w = np.where(m, amount * 0.85, 0.0)[..., None]
    c.px[..., :3] = c.px[..., :3] * (1 - w) + K['violet4'][:3] * w
    if amount > 0.7:
        c.put(m & (c.B < (amount - 0.7) * 3), K['white'])


def figure_mask(p):
    """Silhouette of a pose (mass + arm + front dagger) for afterimages."""
    c = Canvas(CELL, CELL)
    fig = Figure(c, p)
    arm, _ = sleeve(c, fig.shoulder, fig.hand, p.bow, 2.7, 2.0)
    a = math.radians(p.fdag)
    off = 8.85 * DAGGER_S
    lab, _, _ = dagger_labels(c, fig.hand[0] + math.cos(a) * off, fig.hand[1] + math.sin(a) * off, a, s=DAGGER_S)
    return fig.mask() | arm | (lab > 0)


def ghost(c, g, mask, a, violet=False, fill=True, cover=None):
    """Afterimage (the Esquiva card): translucent dithered fill and a bright edge. `cover` (mask) is left untouched."""
    edge = mask & ~erode(mask)
    if cover is not None:
        mask, edge = mask & ~cover, edge & ~cover
    if not fill:
        mask = np.zeros_like(mask)
    if violet:
        c.put(mask, K['violet0'], a * 0.45)
        c.put(edge, K['violet2'], a * 0.85)
        g.add(edge, L['violet'], 0.5 * a)
    else:
        c.put(mask, K['cyan0'], a * 0.4)
        c.put(edge, K['cyan2'], a * 0.85)
        g.add(edge, L['cyan'], 0.5 * a)


# ------------------------------------------------------------------------------------------------ effects (cards' vocabulary)
def ambient(c, g, t, seed=21, count=7, gain=0.6):
    """Rising cyan/violet motes around her (seamless over the 24-frame clock)."""
    motes(c, g, t, seed, count, (20, 26, 80, 90), K['cyan3'], K['violet3'], L['cyan'], gain=gain)


def motes(c, g, t, seed, count, area, col_a, col_b, light, rise=1.0, gain=0.8):
    rng = np.random.default_rng(seed)
    x0, y0, x1, y1 = area
    for i in range(count):
        px = rng.uniform(x0, x1)
        phase = rng.random()
        cycles = 1 if rng.random() < 0.7 else 2
        wob = rng.uniform(1.0, 2.5)
        uu = frac(t * cycles * rise + phase)
        y = y1 - uu * (y1 - y0)
        x = px + math.sin((uu * 2 + phase) * math.pi * 2) * wob
        a = math.sin(uu * math.pi)
        if a < 0.2:
            continue
        col = col_a if i % 3 else col_b
        lt = light if i % 3 else L['violet']
        m = c.pixel(x, y)
        c.put(m, col, min(1.0, a * 1.4))
        g.add(m, lt, gain * a)
        if a > 0.85 and i % 4 == 0:
            for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                mm = c.pixel(x + dx, y + dy)
                c.put(mm, col, 0.6)
                g.add(mm, lt, gain * 0.3)


def sparks(c, g, origin, age, seed, count, speed, ang, spread, grav=0.14, life=7.0, violet_every=0, trail=True):
    """Burst of 1 px sparks (Punalada's impact): white-hot first, cyan after, falling, with a short trail."""
    if age <= 0:
        return
    ox, oy = origin
    rng = np.random.default_rng(seed)
    for i in range(count):
        a0 = ang + rng.uniform(-spread, spread)
        spd = rng.uniform(speed * 0.45, speed)
        lf = life * rng.uniform(0.7, 1.0)
        if age > lf:
            continue
        x = ox + math.cos(a0) * spd * age
        y = oy + math.sin(a0) * spd * age + grav * age * age
        k = 1 - age / (lf + 1)
        vio = violet_every and i % violet_every == 0
        m = c.pixel(x, y)
        c.put(m, K['violet3'] if vio else (K['cyan4'] if age < 2.5 else K['cyan2']))
        g.add(m, L['violet'] if vio else L['cyan'], 1.0 * k)
        if trail and age < 4:
            m2 = c.line(x, y, x - math.cos(a0) * spd * 0.9, y - math.sin(a0) * spd * 0.9 - grav * age * 1.5)
            c.put(m2 & ~m, K['violet1'] if vio else K['cyan1'], k)
            g.add(m2, L['violet'] if vio else L['cyan'], 0.3 * k)


def speed_lines(c, g, x, y, ux, uy, k, seed, count=6, spread=8.0, length=(7, 12), start=4.0):
    """1 px lines trailing behind something fast (along -u)."""
    if k <= 0:
        return
    rng = np.random.default_rng(seed)
    vx, vy = -uy, ux
    for i in range(count):
        off = rng.uniform(-spread, spread)
        st = start + rng.uniform(0, 6)
        ln = rng.uniform(*length) * (0.6 + 0.4 * k)
        sx, sy = x - ux * st + vx * off, y - uy * st + vy * off
        m = c.line(sx, sy, sx - ux * ln, sy - uy * ln)
        c.put(m, K['cyan3'] if i % 2 == 0 else K['steel3'], min(1.0, k * 1.6))
        g.add(m, L['cyan'], 0.5 * k)


def dust(c, x, y, age, side=(-1, 1), life=6):
    """Landing puffs (Esquiva): no light."""
    if not (0 <= age <= life):
        return
    k = 1 - age / (life + 1)
    for sd in side:
        px = x + sd * (3 + age * 1.5)
        m = c.disc(px, y - age * 0.4, 1.0 + age * 0.45)
        c.put(m, K['dust1'], 0.75 * k)
        c.put(m & c.disc(px - sd * 0.6, y - 0.5 - age * 0.4, 0.8 + age * 0.3), K['dust0'], k)


def magic_circle(c, g, cx, cy, t, k, rx=19.0, ry=3.4, violet=True, front=None):
    """Dotted rune ellipse on the floor (Finta's circle, seen in perspective). front: None both halves, True/False."""
    if k <= 0.02:
        return
    n = 40
    for i in range(n):
        a = i / n * 2 * math.pi + t * 2 * math.pi / 4
        sy = math.sin(a)
        if front is True and sy < 0 or front is False and sy >= 0:
            continue
        r = 1.0 if i % 2 == 0 else 0.9
        if (i / n) > k:                                  # it draws itself in as k grows
            continue
        m = c.pixel(cx + math.cos(a) * rx * r, cy + sy * ry * r)
        hot = i % 5 == 0
        c.put(m, (K['violet3'] if hot else K['violet2']) if violet else (K['cyan3'] if hot else K['cyan1']))
        g.add(m, L['violet'] if violet else L['cyan'], (0.7 if hot else 0.3) * min(1, k * 1.3))


def ring(c, g, cx, cy, r, k, ry=None, col=None, light=None, gain=0.8):
    if k <= 0.02:
        return
    ry = r if ry is None else ry
    m = np.abs(np.hypot((c.x - cx) / max(r, 0.5), (c.y - cy) / max(ry, 0.5)) - 1) * max(r, ry) < 0.6
    c.put(m, K['cyan3'] if col is None else col, k)
    g.add(m, L['cyan'] if light is None else light, gain * k)


def shimmer(c, g, f, period, tip_base, tip, seed, count=5):
    """Sparkles peeling off the blade edge and drifting up (each lives 5 frames; seamless over `period`)."""
    (bx, by), (tx, ty) = tip_base, tip
    ln = max(1e-6, math.hypot(tx - bx, ty - by))
    ux, uy = (tx - bx) / ln, (ty - by) / ln
    vx, vy = -uy, ux
    rng = np.random.default_rng(seed)
    for i in range(count):
        born = int(rng.integers(0, period))
        uu = rng.uniform(0.25, 0.9) * ln
        side = -1 if rng.random() < 0.6 else 1
        age = (f - born) % period
        if age > 4:
            continue
        drift = 1.0 + age * 1.1
        px = bx + ux * uu + vx * side * (2 + drift)
        py = by + uy * uu + vy * side * (2 + drift) - age * 0.7
        a = 1 - age / 5
        m = c.pixel(px, py)
        c.put(m, K['white'] if age < 2 else K['cyan3'])
        g.add(m, L['cyan'], a)
        if age < 1:
            for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                mm = c.pixel(px + dx, py + dy)
                c.put(mm, K['cyan3'], 0.8)
                g.add(mm, L['cyan'], 0.3)


def guard_point(fig, p, s=DAGGER_S):
    a = math.radians(p.fdag)
    off = 8.85 * s
    return fig.hand[0] + math.cos(a) * off, fig.hand[1] + math.sin(a) * off


# ------------------------------------------------------------------------------------------------ animations
IDLE = Pose()
LOOP = 24                     # the ambient clock (cloth, motes) runs on the idle loop


def idle_pose(t):
    """Breathing (1 px), cloth on a 2 s cycle, the dagger hand rising and falling with the breath."""
    s = math.sin(t * 2 * math.pi)
    return with_(IDLE, sink=IDLE.sink + round(0.5 - 0.5 * math.cos(t * 2 * math.pi)), phase=t * 2 * math.pi,
                 hh=IDLE.hh + (1 if s > 0.35 else 0), fdag=IDLE.fdag + 3 * s)


def clock(f, n):
    """Ambient time of action frame f: runs on so that the last frame is idle time 0."""
    return frac((f - (n - 1)) / LOOP)


def idle_frame(f):
    t = f / LOOP
    p = idle_pose(t)
    p = with_(p, blink=1.0 if f in (17, 18) else 0.0)
    c, g = Canvas(CELL, CELL), Canvas(CELL, CELL)
    glint = -2 + (f - 6) * 10.0 if 6 <= f <= 10 else None
    fig, tip, _ = draw_figure(c, g, p, t, glint)
    shimmer(c, g, f, LOOP, guard_point(fig, p), tip, 77, count=4)
    ambient(c, g, t)
    return c, g


def keyed(keys, f):
    """Pose at frame f from (frame, pose, easing) keys."""
    for (f0, p0, _), (f1, p1, ease) in zip(keys, keys[1:]):
        if f0 <= f <= f1:
            u = 0.0 if f1 == f0 else (f - f0) / (f1 - f0)
            return blend(p0, p1, ease(u) if ease else u)
    return keys[-1][1]


def at_clock(p, t):
    return with_(p, phase=t * 2 * math.pi)


# ---- attack: Punalada (wind-up, thrust with speed lines and a star at the tip, sparks, glint, recovery)
def attack_keys():
    i0 = idle_pose(0)
    wind1 = with_(i0, x=-2, sink=4, lean=0.5, hx=7, hh=35, fdag=-14, bow=3.0, runes=1.6, eyes=1.2)
    wind2 = with_(i0, x=-3, sink=5, lean=-1.5, hx=4, hh=36, fdag=-8, bow=3.4, runes=2.1, eyes=1.6, bf=-11)
    launch = with_(i0, x=5, sink=3, lean=6, ff=15, flift=2.5, bf=-8, hx=17, hh=37, fdag=-2, bow=1.2, sweep=0.7,
                   runes=2.2, eyes=1.6)
    strike = with_(i0, x=8, sink=4, lean=8, nod=1, ff=19, bf=-7, fknee=3.5, hx=20, hh=37, fdag=4, bow=0.4, sweep=1.0,
                   runes=2.4, eyes=1.6, stretch=1.03)
    hold = with_(strike, sweep=0.75, hx=19, runes=2.0, eyes=1.3)
    rec = with_(strike, x=4, lean=5, ff=14, hx=15, hh=35, fdag=-12, bow=1.5, sweep=0.35, runes=1.4, eyes=1.1)
    return [(0, i0, None), (1, wind1, ease_out), (2, wind2, ease_out), (3, launch, None), (4, strike, ease_out),
            (5, hold, None), (7, rec, ease_in_out), (9, i0, ease_in_out)]


ATTACK_KEYS = None


def attack_pose(f):
    global ATTACK_KEYS
    if ATTACK_KEYS is None:
        ATTACK_KEYS = attack_keys()
    return keyed(ATTACK_KEYS, f)


def tip_of(p):
    c = Canvas(CELL, CELL)
    fig = Figure(c, p)
    a = math.radians(p.fdag)
    off = 8.85 * DAGGER_S
    gx, gy = fig.hand[0] + math.cos(a) * off, fig.hand[1] + math.sin(a) * off
    return gx + math.cos(a) * 40 * DAGGER_S, gy + math.sin(a) * 40 * DAGGER_S


def attack_strike_point():
    return tip_of(attack_pose(STRIKE_FRAME))


def attack_frame(f):
    n = 10
    if f == n - 1:
        return idle_frame(0)
    t = clock(f, n)
    p = at_clock(attack_pose(f), t)
    c, g = Canvas(CELL, CELL), Canvas(CELL, CELL)
    cover = figure_mask(p)
    # one afterimage of the wind-up pose left behind by the lunge (edge only where she no longer is)
    if f in (3, 4, 5):
        ghost(c, g, figure_mask(at_clock(attack_pose(2), t)), {3: 0.9, 4: 0.55, 5: 0.25}[f], fill=False, cover=cover)
    # speed lines behind her during the thrust
    if f in (3, 4, 5):
        k = {3: 0.7, 4: 1.0, 5: 0.45}[f]
        fig0 = Figure(Canvas(CELL, CELL), p)
        speed_lines(c, g, fig0.gx - 14, fig0.y_at(36), 1, 0, k, 31 + f, count=5, spread=12, length=(5, 10), start=0)
    glint = -2 + (f - 5) * 12.0 if 5 <= f <= 8 else None
    fig, tip, _ = draw_figure(c, g, p, t, glint)
    tx, ty = tip
    a = math.radians(p.fdag)
    ux, uy = math.cos(a), math.sin(a)
    if f in (3, 4, 5):                                   # lines hugging the blade (Punalada), outside it
        k = {3: 0.7, 4: 1.0, 5: 0.45}[f]
        vx, vy = -uy, ux
        for i, off in enumerate((-6, 6, -8, 8, -4.5, 4.5)):
            st = 3 + (i * 5) % 9
            ln = (7 + (i * 3) % 6) * (0.6 + 0.4 * k)
            x0, y0 = tx - ux * st + vx * off, ty - uy * st + vy * off
            m = c.line(x0, y0, x0 - ux * ln, y0 - uy * ln)
            c.put(m, K['cyan3'] if i % 2 == 0 else K['steel3'])
            g.add(m, L['cyan'], 0.6 * k)
    if f == 2:
        star4(c, g, tx + 1, ty, 2, gain=0.8)
    elif f in (4, 5, 6, 7):
        star4(c, g, tx, ty, {4: 5, 5: 4, 6: 2, 7: 1}[f], gain=1.4)
    sx, sy = attack_strike_point()
    # sparks spray up and back from the point (the game adds its own burst forward, outside the cell)
    sparks(c, g, (sx - 1, sy), f - STRIKE_FRAME, 5, 14, 2.6, -math.pi * 0.7, 0.9, grav=0.2)
    ambient(c, g, t)
    return c, g


# ---- guard: Esquiva (dash back leaving afterimages, guard with the blade up, dash home)
def guard_keys():
    i0 = idle_pose(0)
    crouch = with_(i0, sink=5, lean=1, hx=11, hh=33, fdag=-45, bow=3)
    back = with_(i0, x=-14, y=-2, sink=2, lean=-4, nod=-1, ff=-5, bf=-20, flift=2, blift=1, hx=9, hh=41, fdag=-82,
                 bow=2.0, wind=1.5, tail=0.55, runes=1.8, eyes=1.3)
    land = with_(back, y=0, sink=6, stretch=0.94, lean=-1.5, nod=0, ff=-4, bf=-22, flift=0, blift=0, hh=39, fdag=-86,
                 wind=1.3, tail=0.7, runes=2.2)
    hold = with_(land, sink=5, stretch=0.97, hh=40, fdag=-84, wind=1.1, tail=0.8, runes=1.8)
    home = with_(i0, x=-4, sink=3, lean=4, ff=5, bf=-13, flift=1.5, hx=12, hh=36, fdag=-55, sweep=0.6, runes=1.4)
    return [(0, crouch, None), (1, back, ease_out), (2, land, ease_out), (4, hold, None), (5, home, ease_out),
            (6, with_(i0, sink=3, lean=3.5, sweep=0.2), ease_out), (8, i0, ease_in_out)]


GUARD_KEYS = None


def guard_pose(f):
    global GUARD_KEYS
    if GUARD_KEYS is None:
        GUARD_KEYS = guard_keys()
    return keyed(GUARD_KEYS, f)


def guard_frame(f):
    n = 9
    if f == n - 1:
        return idle_frame(0)
    t = clock(f, n)
    p = at_clock(guard_pose(f), t)
    c, g = Canvas(CELL, CELL), Canvas(CELL, CELL)
    home_mask = figure_mask(at_clock(guard_pose(0), t))
    cover = figure_mask(p)
    # afterimages where the dash started (two ghosts, fading), like the Esquiva card
    if 1 <= f <= 4:
        ghost(c, g, home_mask, {1: 0.9, 2: 0.65, 3: 0.4, 4: 0.18}[f], cover=cover)
    if 1 <= f <= 3:
        mid = blend(guard_pose(0), guard_pose(1), 0.5)
        ghost(c, g, figure_mask(at_clock(mid, t)), {1: 0.7, 2: 0.4, 3: 0.15}[f], fill=False, cover=cover | home_mask)
    if f in (5, 6):
        ghost(c, g, figure_mask(at_clock(guard_pose(4), t)), {5: 0.75, 6: 0.3}[f], cover=cover)
    fig0 = Figure(Canvas(CELL, CELL), p)
    # speed lines on the side she comes from
    if f in (1, 2, 3):
        k = {1: 1.0, 2: 0.6, 3: 0.25}[f]
        speed_lines(c, g, fig0.cx_at(40) + 10, fig0.y_at(38), -1, 0, k, 61 + f, count=6, spread=16, length=(5, 10))
    if f in (5, 6):
        k = {5: 0.9, 6: 0.4}[f]
        speed_lines(c, g, fig0.cx_at(40) - 10, fig0.y_at(38), 1, 0, k, 71 + f, count=6, spread=16, length=(5, 10))
    # swoosh on the floor between the two spots
    if f in (1, 2, 3):
        k = {1: 1.0, 2: 0.6, 3: 0.25}[f]
        x0, x1 = ROOT_X - 16, ROOT_X + 8
        arc = (np.abs(np.hypot(c.x - (x0 + x1) / 2, (c.y - (GROUND - 22)) * 2.6) - 13) < 1.1) & (c.y > GROUND - 3.5) \
            & (c.y < GROUND) & (c.x > fig0.gx + 2)
        c.put(arc, K['cyan3'], k)
        g.add(arc, L['cyan'], 0.8 * k)
    fig, tip, _ = draw_figure(c, g, p, t, glint=-2 + (f - 3) * 13.0 if 3 <= f <= 5 else None)
    dust(c, fig.gx, GROUND - 1, f - 2)
    dust(c, fig.gx + 4, GROUND - 1, f - 6, side=(-1,))
    tx, ty = tip
    if f == 2:
        star4(c, g, tx, ty - 1, 4, gain=1.2)
    elif f == 3:
        star4(c, g, tx, ty - 1, 2, gain=0.9)
    ambient(c, g, t)
    return c, g


# ---- hurt: white flash, knocked back with violet shards, recovery
def hurt_keys():
    i0 = idle_pose(0)
    hit = with_(i0, x=-4, sink=4, stretch=0.95, lean=-5, nod=-2, ff=8, bf=-11, hx=8, hh=31, fdag=18, bow=3.5,
                blink=1.0, eyes=1.4, flash=0.85, runes=0.6, wind=1.2)
    back = with_(hit, x=-5, lean=-6, flash=0.4, hh=30)
    rec = with_(i0, x=-2, sink=3, lean=0, hx=11, fdag=-15, flash=0.0)
    return [(0, hit, None), (1, back, ease_out), (2, with_(back, flash=0.12, x=-4.5, lean=-4.5), None),
            (4, rec, ease_in_out), (6, i0, ease_in_out)]


HURT_KEYS = None


def hurt_pose(f):
    global HURT_KEYS
    if HURT_KEYS is None:
        HURT_KEYS = hurt_keys()
    return keyed(HURT_KEYS, f)


def hurt_frame(f):
    n = 7
    if f == n - 1:
        return idle_frame(0)
    t = clock(f, n)
    p = at_clock(hurt_pose(f), t)
    c, g = Canvas(CELL, CELL), Canvas(CELL, CELL)
    fig, tip, _ = draw_figure(c, g, p, t)
    chest = Figure(Canvas(CELL, CELL), hurt_pose(0)).chest
    sparks(c, g, (chest[0] + 3, chest[1]), f + 0.8, 13, 12, 2.6, math.pi, 1.0, grav=0.12, life=5.5, violet_every=2)
    if f == 0:
        star4(c, g, chest[0] + 4, chest[1] - 1, 3, core=K['white'], col=K['violet3'], edge=K['violet2'], light=L['violet'], gain=1.0)
    ambient(c, g, t)
    return c, g


# ---- cast: Finta (blade raised, rune circle, a violet ghost dagger feints over her head, release flare)
def cast_keys():
    i0 = idle_pose(0)
    up1 = with_(i0, sink=3, hx=9, hh=44, fdag=-70, bow=2.5, runes=1.6, eyes=1.2, stretch=1.01)
    up2 = with_(i0, sink=1, y=0, hx=7, hh=50, fdag=-86, bow=2.2, runes=2.0, eyes=1.4, stretch=1.03, lean=1.5, wind=1.4)
    rise = with_(up2, y=-1, stretch=1.05, hh=52, runes=2.4, eyes=1.6, wind=1.6)
    rel = with_(rise, y=-1, stretch=1.06, hh=53, fdag=-88, runes=2.6, eyes=1.8)
    after = with_(i0, sink=3, hx=10, hh=45, fdag=-60, runes=1.6, eyes=1.2, stretch=0.98)
    return [(0, with_(i0, sink=3), None), (1, up1, ease_out), (2, up2, ease_out), (5, rise, ease_in_out),
            (6, rel, None), (8, after, ease_out), (11, i0, ease_in_out)]


CAST_KEYS = None


def cast_pose(f):
    global CAST_KEYS
    if CAST_KEYS is None:
        CAST_KEYS = cast_keys()
    return keyed(CAST_KEYS, f)


def cast_point():
    return tip_of(cast_pose(CAST_FRAME))


def cast_frame(f):
    n = 12
    if f == n - 1:
        return idle_frame(0)
    t = clock(f, n)
    p = at_clock(cast_pose(f), t)
    c, g = Canvas(CELL, CELL), Canvas(CELL, CELL)
    circle_k = {0: 0, 1: 0.35, 2: 0.7, 3: 1, 4: 1, 5: 1, 6: 1, 7: 0.8, 8: 0.55, 9: 0.3, 10: 0.1}[f]
    fx = ROOT_X + p.x + 1
    magic_circle(c, g, fx, GROUND - 2, t, circle_k, front=False)
    px, py = cast_point()
    # the feint: a violet ghost dagger sweeps over her head (back -> front), dashed crescent behind it
    C1, R1 = (ROOT_X + 2, py + 26), 19
    a0, a1 = math.radians(168), math.radians(18)
    if 2 <= f <= 7:
        k = min(1.0, (f - 2) / 3.0)
        head = a0 + (a1 - a0) * ease_out(k)
        fade = 1.0 if f <= 5 else max(0.0, 1 - (f - 5) / 2.5)
        crescent(c, g, C1[0], C1[1], R1, head, 1.6, -1, 3.2, (K['violet0'], K['violet1'], K['violet2'], K['violet3']),
                 L['violet'], a=0.95 * fade, dash=True, gain=0.9)
        if f <= 5:
            hx, hy = C1[0] + math.cos(head) * R1, C1[1] - math.sin(head) * R1
            da = math.pi / 2 - head
            lab, u, v = dagger_labels(c, hx - math.cos(da) * 40 * 0.42, hy - math.sin(da) * 40 * 0.42, da, s=0.42)
            paint_dagger(c, g, lab, u, t * 2, None, ghost=(K['violet3'], 0.9, L['violet']))
    if f >= 6:                                           # the ghost dissolves into violet motes
        rng = np.random.default_rng(9)
        for i in range(14):
            aa = a1 + (a0 - a1) * rng.random() * 0.6
            age = f - 5
            x = C1[0] + math.cos(aa) * R1 + rng.uniform(-1, 1) * age
            y = C1[1] - math.sin(aa) * R1 - age * rng.uniform(0.4, 1.1)
            a = 1 - age / 5.0
            if a > 0:
                m = c.pixel(x, y)
                c.put(m, K['violet3'], a)
                g.add(m, L['violet'], 0.7 * a)
    fig, tip, _ = draw_figure(c, g, p, t, glint=-2 + (f - 1) * 11 if 1 <= f <= 4 else None)
    magic_circle(c, g, fx, GROUND - 2, t, circle_k, front=True)
    # charge: violet motes rise from the circle
    if 1 <= f <= 7:
        motes(c, g, t * 3, 17, 10, (fx - 18, GROUND - 30, fx + 18, GROUND - 1), K['violet3'], K['cyan3'], L['violet'],
              gain=0.8 * circle_k)
    tx, ty = tip
    if f in (4, 5):
        star4(c, g, tx, ty, {4: 1, 5: 3}[f], gain=1.0)
    if f in (6, 7, 8, 9):                                # the flare stays where the spell was released
        star4(c, g, px, py, {6: 7, 7: 5, 8: 3, 9: 1}[f], gain=1.5)
        ring(c, g, px, py, 3 + (f - 6) * 3.5, 1 - (f - 6) / 4, gain=0.9)
    sparks(c, g, (px, py), f - CAST_FRAME + 0.6, 17, 14, 2.6, -math.pi / 2, math.pi, grav=0.16, life=6, violet_every=3)
    ambient(c, g, t)
    return c, g


# ---- death: struck, staggers, sinks to her knees (the cloak pools), the dagger falls and sticks in the floor, the
#      light leaves her eyes and her runes; held on the last frame
def death_keys():
    i0 = idle_pose(0)
    hit = with_(i0, x=-4, sink=4, stretch=0.95, lean=-6, nod=-2, hx=8, hh=31, fdag=18, bow=3.5, blink=1.0, eyes=1.6,
                flash=0.85, runes=0.6, wind=1.6)
    back = with_(hit, x=-6, y=-1, lean=-7, flash=0.4)
    stag = with_(i0, x=-5, sink=7, lean=1, nod=1, hx=6, hh=26, fdag=50, bow=3, blink=0.0, eyes=0.9, runes=0.6, wind=1.2)
    sinking = with_(stag, sink=9, kneel=0.45, lean=4, nod=2.0, drop=1.5, hx=8, hh=20, fdag=70, eyes=0.8, wind=0.8)
    down = with_(stag, sink=4, kneel=1.0, lean=10, nod=2.0, drop=3.5, stretch=0.95, hx=13, hh=6, bow=3, fdag=80,
                 eyes=0.7, wind=0.35, runes=0.4)
    out = with_(down, eyes=0.0, fade=1.0, wind=0.15, nod=2.5, drop=5.0, lean=11, sink=5, runes=0.0)
    return [(0, hit, None), (1, back, ease_out), (3, stag, ease_in_out), (6, sinking, ease_in_out),
            (9, down, ease_out), (10, with_(down, stretch=0.94), None), (11, down, None), (16, out, ease_in_out)]


DEATH_KEYS = None


def death_pose(f):
    global DEATH_KEYS
    if DEATH_KEYS is None:
        DEATH_KEYS = death_keys()
    return keyed(DEATH_KEYS, f)


def fallen_dagger(c, g, f, p):
    """The dagger leaves her hand on frame 5, tumbles and sticks in the floor in front of her on frame 9."""
    x0, y0 = Figure(Canvas(CELL, CELL), death_pose(5)).hand
    land = (ROOT_X + 17, GROUND - 13.5)                     # guard centre once it is stuck (tip 3 px in the floor)
    if f < 9:
        k = (f - 5) / 4.0
        x = x0 + (land[0] - x0) * k
        y = y0 + (land[1] - y0) * k * k - 7 * math.sin(math.pi * k)
        deg = 70 + 380 * k                                  # one tumble, ends pointing down
    else:
        x, y = land
        deg = 87 if f == 9 else (91 if f == 10 else 89)     # it quivers once
    glow = max(0.0, 1.0 - max(0, f - 10) / 6.0)
    a = math.radians(deg)
    lab, u, v = dagger_labels(c, x, y, a, s=DAGGER_S)
    lab[c.y > GROUND] = 0                                   # buried tip
    paint_dagger(c, g, lab, u, f / LOOP * 2, None, rune_gain=1.2 * glow)
    if f == 9:
        star4(c, g, land[0], GROUND - 2, 3, gain=1.0)
    return land


def death_frame(f):
    t = frac(f / LOOP)
    p = at_clock(death_pose(f), t)
    c, g = Canvas(CELL, CELL), Canvas(CELL, CELL)
    hold = f < 5
    fig, tip, _ = draw_figure(c, g, p, t, hold=hold)
    if not hold:
        fallen_dagger(c, g, f, p)
    chest = Figure(Canvas(CELL, CELL), death_pose(0)).chest
    sparks(c, g, (chest[0] + 3, chest[1]), f + 0.8, 13, 12, 2.6, math.pi, 1.0, grav=0.12, life=5.5, violet_every=2)
    if f == 0:
        star4(c, g, chest[0] + 4, chest[1] - 1, 3, core=K['white'], col=K['violet3'], edge=K['violet2'], light=L['violet'])
    dust(c, fig.gx - 2, GROUND - 1, f - 9, life=5)
    # her light leaves as motes rising from the pooled cloak (fewer and fainter until the held frame)
    if f >= 10:
        k = 1 - (f - 10) / 9.0
        motes(c, g, (f - 10) / 12.0, 29, 8, (fig.gx - 14, GROUND - 40, fig.gx + 12, GROUND - 2), K['cyan3'], K['violet3'],
              L['cyan'], gain=0.7 * k)
    return c, g


FRAME_FUNCS = {'idle': idle_frame, 'attack': attack_frame, 'guard': guard_frame, 'hurt': hurt_frame,
               'cast': cast_frame, 'death': death_frame}


def render(name):
    n = dict((a, k) for a, k, _ in ANIMATIONS)[name]
    base, glow = [], []
    for f in range(n):
        c, g = FRAME_FUNCS[name](f)
        base.append(c)
        glow.append(g)
    return base, glow


POSE_FUNCS = {'idle': lambda f: idle_pose(f / LOOP), 'attack': attack_pose, 'guard': guard_pose, 'hurt': hurt_pose,
              'cast': cast_pose, 'death': death_pose}


def mass(name, f):
    """The single silhouette of frame f of an animation (a test pins that it is one 4-connected region)."""
    return Figure(Canvas(CELL, CELL), POSE_FUNCS[name](f)).mask()
