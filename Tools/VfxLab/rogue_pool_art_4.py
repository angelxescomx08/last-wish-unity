"""Animated pixel-art illustrations for a fourth batch of the rogue's loot cards (88x64, 24 frames at 12 fps, seamless).

Same technique and helpers as rogue_pool_art.py, rogue_pool_art_2.py and rogue_pool_art_3.py (docs/art-style.md): every
frame has a base image (opaque pixel art, readable without bloom) and an emissive light map, baked for the game by
export_game_card_art.py. No characters: her daggers, boots, cloak and dice, a straw decoy, and what they touch.

  agilidad          Agilidad (7 de escudo; Combo: +5 de escudo): her boots balance on a tightrope between two towers;
                    an arrow comes, they hop over it and a ward rises; a second arrow, a second hop (Combo), a bigger ward.
  senuelo           Señuelo (5 de escudo; el siguiente ataque enemigo golpea a otro enemigo): a straw decoy wearing her
                    hood takes an enemy's bolt, which turns violet and curves into the other enemy's sigil.
  sombra_esquiva    Sombra Esquiva (7 de escudo; Combo: mete 1 Daga Oculta): a lantern throws the dagger's shadow on a
                    wall; the dagger slips into it, the arrow hits only stone; with Combo the shadow peels off as a
                    card into the draw pile.
  capa_de_sombras   Capa de Sombras (8 de escudo; no se pierde al empezar tu turno): her starry cloak opens round a
                    ward and closes; the hourglass turns (a new turn), its cold wave breaks on the cloak, and the ward
                    is still there when it opens again.
  corte_afortunado  Corte Afortunado (6 de daño; crítico según tu Suerte: +6): a coin leaps from a wishing well, lands
                    clover up on the blade, the runes turn gold and the cut is a critical hit.
  hoja_unica        Hoja Única (8 de daño; Singular: +8): three mirrors show copies of the dagger; they crack and
                    burst, and the one blade left glows violet (Singular) and dives into the enemy's sigil.
  rafaga_de_cortes  Ráfaga de Cortes (2 de daño por carta jugada este turno): the cards played this turn light one by
                    one and each sends a cut across a bronze gong; this card is the fourth and last.
  tirar_los_dados   Tirar los Dados (gana de 0 a 3 de maná; la Suerte da tiradas extra): a dice cup throws a die that
                    lands on 1; a lucky clover makes it roll again, it lands on 3 and three mana drops rise.
  torbellino        Torbellino (8 de daño, 5 de escudo; Combo: +4 y +4): the dagger whips up a whirlwind among standing
                    stones, it hits the enemy's sigil and its wind turns into a ward; with Combo it grows.
  dagas_ocultas     Dagas Ocultas (mete 2 Dagas Ocultas en tu pila de robo; Combo: 3): shadow copies peel off the
                    dagger, fold into cards and slip into the draw pile, and a third one with Combo.
"""
import math

import numpy as np

from pixelkit import Canvas, hexc, dilate, erode, shift
from rogue_art import (AW, AH, FRAMES, K, L, ease_out, ease_in_out, gradient_bg, vignette, star4, motes, outline_mask,
                       dagger_labels)
from mage_art import twinkling_stars, sparks, ellipse_ring
from warrior_art import shake, dust_puffs, WARM_SPARKS, CYAN_SPARKS
from rogue_pool_art import (KR, LR, KB, LB, VIOLET_SPARKS, GOLD_SPARKS, dir_of, dagger, tip_of, speed_lines, converge,
                            clipped, mana_drop, cobbles, sigil, diamond_ring, paint_leaf)
from rogue_pool_art_2 import K2, POISON_SPARKS, paint_card, arrow
from rogue_pool_art_3 import K3, CRIM_SPARKS, plus_marks, ward_rings, slash_line, card_rot, CPAL, RPAL

K4 = dict(K3)
K4.update({
    'boot0': hexc('#1e1016'), 'boot1': hexc('#3d2124'), 'boot2': hexc('#5c3628'), 'boot3': hexc('#8a5a3c'),
    'sack0': hexc('#2a1f16'), 'sack1': hexc('#46352a'), 'sack2': hexc('#64503c'), 'sack3': hexc('#8a7356'),
    'bone0': hexc('#3a3448'), 'bone1': hexc('#7a7290'), 'bone2': hexc('#c4bcd6'), 'bone3': hexc('#f1ecfa'),
    'glass0': hexc('#0b1626'), 'glass1': hexc('#16304a'), 'glass2': hexc('#2f5f84'), 'glass3': hexc('#8cc4e6'),
    'bronze0': hexc('#1f150f'), 'bronze1': hexc('#3e2a1c'), 'bronze2': hexc('#614028'), 'bronze3': hexc('#93683c'),
    'bronze4': hexc('#d6a868'),
    'lining0': hexc('#070a1c'), 'lining1': hexc('#0e1634'), 'lining2': hexc('#18264e'),
    'heath0': hexc('#0a0d12'), 'heath1': hexc('#121a1c'), 'heath2': hexc('#1c2a26'),
})
LUCK = np.array([0.4, 1.0, 0.35, 1])          # the clover's green light (luck)
GOLD = LR['gold']
BLUE_SPARKS = (KB['b3'], KB['b2'], KB['b1'])
STRAW_SPARKS = (K4['straw3'], K4['straw2'], K4['straw1'])


# ---------------------------------------------------------------------------------------- shared bits
def comet(c, g, x, y, px, py, pal, light, r=1.6, gain=1.0):
    """A bolt of light at (x, y) with a tapering trail back to (px, py): pal = (dark, mid, bright)."""
    n = max(2, int(math.hypot(x - px, y - py)))
    for i in range(n + 1):
        u = i / n
        qx, qy = x + (px - x) * u, y + (py - y) * u
        rr = r * (1 - u) + 0.3
        m = c.disc(qx, qy, rr)
        c.put(m, pal[1] if u > 0.25 else pal[2], 1 - u * 0.8)
        g.add(m, light, gain * (1 - u) * 0.8)
    head = c.disc(x, y, r)
    c.put(outline_mask(head), pal[0], 0.7)
    c.put(head, pal[2])
    c.put(c.pixel(x, y), K['white'])
    g.add(head, light, 1.2 * gain)


def draw_pile(c, g, x, y, n, glow=0.0, bump=0):
    """The draw pile: n card backs stacked, each one a pixel up and left of the one below."""
    for i in range(n):
        paint_card(c, g, x - 2 * i, y - 2 * i - (bump if i == n - 1 else 0), 1.0, glow=glow if i == n - 1 else 0.0)


def lerp(a, b, k):
    return a + (b - a) * k


# ---------------------------------------------------------------------------------------- Agilidad
AG_X = 42.0                    # where the boots stand on the rope
AG_ROPE = ((9.0, 42.0), (79.0, 39.0))
AG_JUMPS = ((3, 8), (10, 15))  # (take-off, landing): the second is the Combo
AG_ARROWS = (6, 13)            # frame each arrow passes under the boots


BOOT_S = 1.75                  # the boots are drawn at this scale (native units of boot_mask)


def boot_mask(c, x, y, sy=1.0, lean=0.0, s=BOOT_S):
    """One boot seen from the side, toe to the right: (x, y) = back of the heel on the ground line."""
    ly = (y - c.y) / (sy * s)                 # height above the sole
    lx = (c.x - x) / s - lean * ly
    shaft = (lx >= 0) & (lx <= 4.2) & (ly >= 0) & (ly <= 10.2)
    shaft &= ~((lx > 3.4) & (ly > 3.4) & (ly < 6.0) & (lx - 3.4 > (ly - 3.4) * 0.25))     # ankle
    cuff = (lx >= -0.8) & (lx <= 5.0) & (ly > 8.2) & (ly <= 11.2)
    foot = (lx >= 0) & (lx <= 6.8) & (ly >= 0) & (ly <= 3.2)
    foot &= ~((lx > 5.0) & ((((lx - 5.0) / 1.9) ** 2 + ((ly - 0.4) / 2.9) ** 2) > 1))
    return shaft | cuff | foot, lx, ly


def paint_boot(c, g, x, y, sy=1.0, lean=0.0, dark=0.0, rune=0.0):
    m, lx, ly = boot_mask(c, x, y, sy, lean)
    c.put(outline_mask(m), K['ink'])
    if dark > 0:                                                              # the far boot: one step darker
        c.put(m, K4['boot0'])
        c.put(m & (lx < 1.4) & (ly > 1.2), K4['boot1'])
        cuff = m & (ly > 8.2)
        c.put(cuff, K['scarf0'])
        c.put(cuff & (ly > 10.2), K['scarf1'])
        rn = m & (np.abs(ly - 9.7) < 0.35) & (np.abs(lx - 2.0) < 0.9)
        c.put(rn, K['scarf1'] if rune < 0.3 else K['cyan2'])
        g.add(rn, L['cyan'], 0.6 * rune)
        return m
    c.put(m, K4['boot1'])
    c.put(m & (lx < 1.4) & (ly > 1.2), K4['boot2'])
    c.put(m & (lx < 0.7) & (ly > 3.5) & (ly < 8), K4['boot3'])
    c.put(m & (lx > 3.2) & (ly > 3.2), K4['boot0'])
    c.put(m & (ly < 0.75), K4['boot0'])                                       # sole
    c.put(m & (ly < 0.75) & (lx < 2.2), K['ink'])                             # heel
    c.put(m & (lx > 4.6) & (ly > 1.4) & (ly < 2.6), K4['boot3'])              # shine on the toe
    cuff = m & (ly > 8.2)
    c.put(cuff, K['scarf1'])
    c.put(cuff & (ly > 10.2), K['scarf2'])
    c.put(cuff & (lx > 3.6), K['scarf0'])
    strap = m & (np.abs(ly - 5.0) < 0.45) & (lx < 4.4)
    c.put(strap, K4['boot0'])
    c.put(strap & (np.abs(lx - 3.4) < 0.5), K['steel3'])                     # little buckle
    rn = m & (np.abs(ly - 9.7) < 0.35) & (np.abs(lx - 2.0) < 0.9)            # a stitched rune on the cuff
    c.put(rn, K['scarf2'] if rune < 0.3 else K['cyan3'] if rune < 0.8 else K['cyan4'])
    g.add(rn, L['cyan'], 1.1 * rune)
    return m


def boots(c, g, x, y, sy=1.0, lean=0.0, stride=0.0, rune=0.0, ghost=None):
    """The pair: the far boot a little behind and darker; stride lifts the near one forward in the air."""
    far, near = (x - 7, y), (x + 1 + stride, y - stride * 0.7)
    if ghost is not None:
        col, a = ghost
        m = boot_mask(c, far[0], far[1], sy, lean)[0] | boot_mask(c, near[0], near[1], sy, lean)[0]
        c.put(outline_mask(m), col, a)
        c.put(m, K['violet1'], a * 0.35)
        g.add(outline_mask(m), L['violet'], 0.4 * a)
        return m
    paint_boot(c, g, far[0], far[1], sy, lean, dark=0.3, rune=rune)
    return paint_boot(c, g, near[0], near[1], sy, lean, rune=rune)


def rope_y(x, dip, at):
    (x0, y0), (x1, y1) = AG_ROPE
    u = (x - x0) / (x1 - x0)
    return y0 + (y1 - y0) * u + 3.0 * math.sin(math.pi * u) + dip * math.exp(-((x - at) / 11.0) ** 2)


def tightrope_scene(c, g, t, dip):
    gradient_bg(c, K['night0'], K['night2'], K['night1'], 0.6)
    twinkling_stars(c, g, t, 4001, 16, (12, 2, 76, 30))
    moon = c.disc(66, 11, 5) & ~c.disc(68.5, 9.5, 4.4)              # thin crescent moon
    c.put(c.disc(66, 11, 8), K['night3'], 0.35)
    c.put(moon, K['moon1'])
    c.put(moon & (c.x < 64), K['moon2'])
    g.add(moon, L['blue'], 0.3)
    # far spires in the mist far below
    far = np.zeros((AH, AW), bool)
    for x, w, h in ((14, 5, 14), (22, 3, 9), (30, 6, 18), (47, 4, 12), (55, 7, 16), (66, 3, 10)):
        far |= c.poly([(x, 64), (x, 64 - h + 3), (x + w / 2, 64 - h - 3), (x + w, 64 - h + 3), (x + w, 64)])
    c.put(far, K['night3'])
    c.put(far & (c.x % 3 < 1), K['night2'])
    for i, y in enumerate((50, 55, 60)):                              # mist bands drifting
        off = (t * 88 * (1 if i % 2 else -1)) % 88
        band = (c.y > y) & (np.sin((c.x + off) / 88 * math.pi * 4 + i) * 2 + y < c.y)
        c.put(band, K['teal2'], 0.45 - i * 0.08)
    # the two towers the rope is tied between
    for x0, x1, top, sgn in ((0, 9, 39, 1), (79, 87, 36, -1)):
        tw = c.rect(x0, top, x1, AH - 1)
        for bx in range(x0, x1 + 1, 3):
            tw |= c.rect(bx, top - 2, bx + 1, top - 1)
        c.put(outline_mask(tw), K['ink'])
        c.put(tw, KR['stone1'])
        c.put(tw & ((c.x < x0 + 2) if sgn > 0 else (c.x < x0 + 2)), KR['stone2'])
        for wy in (46, 56):
            win = c.rect(x0 + 3, wy, x0 + 4, wy + 2)
            flick = 0.75 + 0.25 * math.sin((t * 3 + wy * 0.1 + x0) * math.pi * 2)
            c.put(win, K['window2'])
            c.put(c.pixel(x0 + 3.5, wy + 1.5), K['window'] if flick > 0.7 else K['window2'])
            g.add(win, L['warm'], 0.35 * flick)
    # the rope (twisted: two tones), sagging under the boots
    pts = [(x, rope_y(x, dip, AG_X)) for x in np.arange(AG_ROPE[0][0], AG_ROPE[1][0] + 0.5, 1.0)]
    for (xa, ya), (xb, yb) in zip(pts, pts[1:]):
        m = c.line(xa, ya, xb, yb)
        c.put(m, K4['straw2'] if int(xa) % 3 else K4['straw1'])
    for (xa, ya), (xb, yb) in zip(pts, pts[1:]):
        c.put(c.line(xa, ya + 1, xb, yb + 1) & (c.B < 0.5), K['ink'])


def agilidad_frame(f):
    t = f / FRAMES
    c, g = Canvas(AW, AH), Canvas(AW, AH)
    # height of the boots above the rope, squash, lean and stride for this frame
    h, sy, lean, stride, rune = 0.0, 1.0, math.sin(t * math.pi * 4) * 0.08, 0.0, 0.0
    dip = 2.0
    for i, (up, down) in enumerate(AG_JUMPS):
        top = 13.0 if i == 0 else 16.0
        if f == up:                                             # crouch
            sy, dip = 0.78, 3.6
        elif up < f < down:
            u = (f - up) / (down - up)
            h = top * math.sin(math.pi * u)
            lean = -0.25 if u < 0.5 else 0.15
            stride = 2.0 * math.sin(math.pi * u)
            rune = 1.0
            dip = 1.0 - 0.6 * math.sin(math.pi * u)
        elif f == down:                                         # landing: the rope bows
            sy, dip, rune = 0.82, 5.0, 0.6
        elif f == down + 1:
            dip = 0.6
    tightrope_scene(c, g, t, dip)
    gy = rope_y(AG_X, dip, AG_X)
    # the arrows fly in from the right, level with the boots, and pass under them
    for i, (k_pass, (up, down)) in enumerate(zip(AG_ARROWS, AG_JUMPS)):
        a = f - k_pass
        if -3 <= a <= 2:
            tx = AG_X - 6 - a * 15
            ty = gy - 5
            arrow(c, g, tx, ty, math.pi, s=1.1)
            speed_lines(c, g, tx + 18, ty, -1.0, 0.0, 1.0, 4003 + i, count=4, spread=2, length=(5, 9))
    # after-images on the way up and down
    for i, (up, down) in enumerate(AG_JUMPS):
        if up < f < down:
            top = 13.0 if i == 0 else 16.0
            for back, al in ((1, 0.5), (2, 0.25)):
                u = (f - back - up) / (down - up)
                if 0 < u < 1:
                    boots(c, g, AG_X, round(gy - top * math.sin(math.pi * u)), lean=lean,
                          stride=2.0 * math.sin(math.pi * u), ghost=(K['violet3'], al))
    boots(c, g, AG_X, round(gy - h), sy, lean, stride, rune)
    # the wards: 7 de escudo on the first landing, +5 (Combo) on the second
    for i, (up, down) in enumerate(AG_JUMPS):
        a = f - down
        if 0 <= a <= 7:
            ward_rings(c, g, AG_X - 2, gy - 9, a, size=12 if i == 0 else 18, squash=1.0)
        if a == 0:
            star4(c, g, AG_X - 2, gy - 9, 4 if i == 0 else 5, core=K['white'], col=KB['b3'], edge=KB['b2'], light=LB)
    dust_puffs(c, AG_X, gy + 1, f, AG_JUMPS[0][1], 2, 1.1, rise=0.3, life=4, seed=4005, big=0.8)
    dust_puffs(c, AG_X, gy + 1, f, AG_JUMPS[1][1], 2, 1.3, rise=0.3, life=5, seed=4007, big=0.9)
    sparks(c, g, f, AG_JUMPS[1][1], AG_X - 2, gy - 9, 4009, 9, (0.8, 2.0), 6, BLUE_SPARKS, LB, gravity=0.08)
    motes(c, g, t, 4011, 7, (4, 6, 84, 50), K['violet3'], KB['b2'], L['violet'], gain=0.45)
    vignette(c, 0.72)
    return c, g


# ---------------------------------------------------------------------------------------- Señuelo
SN_BASE = (27.0, 55.0)        # foot of the decoy's post
SN_ENEMIES = ((72.0, 15.0), (70.0, 45.0))
SN_FIRE = 5                   # the first enemy shoots
SN_HIT = 8                    # the bolt hits the decoy
SN_BACK = 12                  # the turned bolt hits the other enemy


def decoy_masks(c, lean=0.0, jolt=0.0):
    """The straw decoy on its post, as one silhouette: returns {zone: mask} (post, bar, body, straw, head, hood)."""
    bx, by = SN_BASE
    X = c.x - bx - jolt - lean * (by - c.y)
    Y = c.y
    post = (np.abs(X - 0.5) <= 1.0) & (Y >= 14) & (Y <= by)
    bar = (X >= -11) & (X <= 12) & (Y >= 27) & (Y <= 28.8)
    body = ((X / 6.2) ** 2 + ((Y - 35) / 8.2) ** 2) <= 1
    straw = np.zeros_like(post)
    for sx, sy_, dx, dy in ((-11, 28, -3, -2), (-11, 28, -3, 1), (-11, 28, -2, 3), (12, 28, 3, -2), (12, 28, 3, 1),
                            (12, 28, 2, 3), (-3, 42, -2, 4), (0, 43, 0, 5), (3, 42, 2, 4)):
        for k in np.linspace(0, 1, 6):
            straw |= (np.abs(X - (sx + dx * k)) < 0.6) & (np.abs(Y - (sy_ + dy * k)) < 0.6)
    head = ((X - 0.5) ** 2 + (Y - 19.5) ** 2) <= 4.8 ** 2
    face = (((X - 1.8) / 3.0) ** 2 + ((Y - 20.8) / 3.4) ** 2) <= 1
    hood = ((((X + 0.2) / 6.0) ** 2 + ((Y - 18.8) / 6.6) ** 2) <= 1) & ~face
    hood |= (X < -2) & (X > -7.5) & (Y > 16) & (Y < 25 + (X + 2) * 0.6) & (((X + 1) / 6.5) ** 2 + ((Y - 19) / 7) ** 2 <= 1.25)
    p0x, p0y, p1x, p1y = -0.5, 13.5, -8.5, 7.5                 # the hood's point, falling back
    L2_ = (p1x - p0x) ** 2 + (p1y - p0y) ** 2
    u = ((X - p0x) * (p1x - p0x) + (Y - p0y) * (p1y - p0y)) / L2_
    d = np.abs((X - p0x) * (p1y - p0y) - (Y - p0y) * (p1x - p0x)) / math.sqrt(L2_)
    hood |= (u >= 0) & (u <= 1) & (d <= 2.8 * (1 - u) + 0.4)
    scarf = (Y >= 23.5) & (Y <= 31) & (np.abs(X - (2.6 + (Y - 23.5) * 0.25)) <= 1.4 - (Y - 23.5) * 0.06)
    return {'post': post, 'bar': bar, 'body': body, 'straw': straw, 'head': head & ~hood, 'hood': hood | scarf,
            'scarf': scarf, 'face': face & head}, X, Y


def paint_decoy(c, g, t, lean=0.0, jolt=0.0, flash=0.0, charm=0.5):
    z, X, Y = decoy_masks(c, lean, jolt)
    whole = z['post'] | z['bar'] | z['body'] | z['straw'] | z['head'] | z['hood']
    c.put(outline_mask(whole), K['ink'])
    c.put(z['post'] | z['bar'], KR['wood1'])
    c.put((z['post'] & (X < 0)) | (z['bar'] & (Y < 27.8)), KR['wood2'])
    c.put(z['straw'], K4['straw2'])
    c.put(z['straw'] & (((X + Y) % 3) < 1), K4['straw3'])
    b = z['body']
    c.put(b, K4['sack1'])
    c.put(b & ((X / 6.2) ** 2 + ((Y - 33) / 7) ** 2 <= 0.35) & (X < 1), K4['sack2'])
    c.put(b & (X > 3.6), K4['sack0'])
    c.put(b & (np.abs(Y - 38.5) < 0.6), K4['straw1'])                  # rope round the waist
    c.put(b & (np.abs(X + 1) < 0.5) & (Y > 29) & (Y < 37), K4['sack0'])  # seam
    hd = z['head']
    c.put(hd, K4['sack2'])
    c.put(hd & (X > 2.6), K4['sack1'])
    for ex in (-0.8, 2.6):                                             # stitched X eyes
        c.put(c.pixel(SN_BASE[0] + jolt + lean * (SN_BASE[1] - 19) + ex, 19) |
              c.pixel(SN_BASE[0] + jolt + lean * (SN_BASE[1] - 21) + ex + 1, 21), K4['sack0'])
    hood = z['hood']
    c.put(hood, K['scarf1'])
    c.put(hood & (X > 2.5), K['scarf0'])
    c.put(hood & dilate(z['face']) & ~z['scarf'], K['scarf0'])          # the hood's shadowed inner edge
    c.put(hood & ~shift(hood, 0, 1) & (X < 3), K['scarf2'])
    c.put(z['scarf'] & (np.floor(Y) % 3 == 0), K['scarf2'])
    c.put(z['scarf'] & (Y > 30), K['scarf0'])
    # the decoy's charm: a violet glimmer on the hood's edge that draws the eye
    rim = outline_mask(hood) & ~whole
    c.put(rim & (c.B < 0.5), K['violet1'], 0.5 * charm)
    g.add(hood & ~shift(hood, 0, 1), L['violet'], 0.35 * charm)
    if flash > 0:
        c.put(whole, K['white'], flash)
    return whole


def barn_field(c, g, t):
    gradient_bg(c, K['night0'], K['vio2'], K['night1'], 0.62)
    twinkling_stars(c, g, t, 4101, 14, (4, 2, 84, 30), light=L['violet'])
    hills = c.poly([(0, 44), (14, 40), (30, 43), (46, 38), (62, 42), (78, 37), (88, 40), (88, 64), (0, 64)])
    c.put(hills, K['roof2'])
    c.put(hills & ~shift(hills, 0, 1), K['vio3'], 0.6)
    barn = c.poly([(4, 46), (4, 36), (11, 30), (18, 36), (18, 46)])           # a barn far off on the hill
    c.put(barn, K['roof'])
    win = c.rect(9, 38, 11, 40)
    c.put(win, K['window2'])
    c.put(c.pixel(9.5, 38.5), K['window'])
    g.add(win, L['warm'], 0.25 + 0.1 * math.sin(t * math.pi * 2))
    ground = c.rect(0, 54, AW - 1, AH - 1)
    c.put(ground, KR['tile1'])
    c.put(c.rect(0, 54, AW - 1, 54), K['vio3'])
    rng = np.random.default_rng(4103)
    for i in range(14):                                                # grass blades in the wind
        x = rng.uniform(0, AW)
        sway = math.sin((t * 2 + rng.random()) * math.pi * 2) * 1.2
        c.put(c.line(x, 56, x + sway, 52 - rng.uniform(0, 2)), K['vio2'])
    for x in range(40, 88, 9):                                        # a broken fence behind the decoy
        c.put(c.rect(x, 46, x + 1, 54), KR['wood0'])
    c.put(c.rect(40, 48, 87, 48), KR['wood0'])


def senuelo_frame(f):
    t = f / FRAMES
    c, g = Canvas(AW, AH), Canvas(AW, AH)
    barn_field(c, g, t)
    e1, e2 = SN_ENEMIES
    hit2 = f - SN_BACK
    for i, (ex, ey) in enumerate(SN_ENEMIES):
        charge = 1.0 if (i == 0 and SN_FIRE - 2 <= f <= SN_FIRE) else 0.0
        hurt = (i == 1 and 0 <= hit2 <= 4)
        jx = (1 if hit2 % 2 == 0 else -1) if hurt and hit2 <= 2 else 0
        a = 0.9 if not hurt else 1.0
        col = K['white'] if (i == 1 and hit2 == 0) else K3['crim4'] if charge or hurt else K3['crim3']
        sigil(c, g, ex + jx, ey, 6, 6, t * (1.5 if i else -1.5), a, col=col, light=LR['crimson'])
        eye = c.disc(ex + jx, ey, 1.6)
        c.put(eye, K3['crim5'] if not (i == 1 and 0 <= hit2 <= 6) else K3['crim1'])
        g.add(eye, LR['crimson'], 0.9 + charge)
        if i == 1 and 1 <= hit2 <= 8:                                      # its sigil cracks open
            cr = c.line(ex - 5, ey - 3, ex - 1, ey) | c.line(ex + 1, ey + 1, ex + 5, ey + 4) | c.line(ex, ey - 6, ex + 1, ey - 2)
            c.put(cr, K['white'] if hit2 <= 2 else K3['crim5'], 1 - hit2 / 10)
            g.add(cr, LR['crimson'], 0.8 * (1 - hit2 / 10))
    # the decoy: sways in the wind, is knocked back when the bolt hits it
    a = f - SN_HIT
    lean = math.sin(t * math.pi * 4) * 0.025
    jolt = 0.0
    if 0 <= a <= 5:
        lean += {0: 0.14, 1: 0.10, 2: -0.05, 3: 0.04, 4: -0.02, 5: 0.01}[a]
        jolt = {0: 1.0, 1: 1.0}.get(a, 0.0)
    charm = 0.5 + 0.5 * math.sin(t * math.pi * 4) if not (0 <= a <= 3) else 1.0
    paint_decoy(c, g, t, lean, jolt, flash=0.3 if a == 0 else 0.0, charm=charm)
    # 5 de escudo: a ward round the decoy when the card is played (and the lure pulses)
    if 1 <= f <= 8:
        ward_rings(c, g, SN_BASE[0], 32, f - 1, size=10, squash=1.5, life=7)
    for k in range(2):                                                     # lure rings
        r = ((f + k * 6) % 12) * 1.6
        if r > 2 and not 7 <= f <= 13:
            ring = ellipse_ring(c, SN_BASE[0] + 0.5, 19, r, r * 0.9, 0.5)
            c.put(ring, K['violet2'], 0.5 * (1 - r / 19))
            g.add(ring, L['violet'], 0.4 * (1 - r / 19))
    # the bolt: from the first enemy to the decoy (crimson), then turned (violet) into the second enemy
    hit_pt = (SN_BASE[0] + 3, 33.0)
    rpal = (K3['crim1'], K3['crim4'], K3['crim5'])
    vpal = (K['violet0'], K['violet2'], K['violet4'])
    if SN_FIRE <= f < SN_HIT:
        k = (f - SN_FIRE + 1) / (SN_HIT - SN_FIRE + 1)
        x, y = lerp(e1[0] - 4, hit_pt[0], k), lerp(e1[1] + 3, hit_pt[1], k)
        px, py = lerp(e1[0] - 4, hit_pt[0], max(0.0, k - 0.35)), lerp(e1[1] + 3, hit_pt[1], max(0.0, k - 0.35))
        comet(c, g, x, y, px, py, rpal, LR['crimson'], r=1.8)
    if f == SN_FIRE:
        star4(c, g, e1[0] - 4, e1[1] + 3, 3, core=K['white'], col=K3['crim5'], edge=K3['crim3'], light=LR['crimson'])
    curve = lambda u: (lerp(lerp(hit_pt[0], 50.0, u), lerp(50.0, e2[0] - 3, u), u),
                       lerp(lerp(hit_pt[1], 58.0, u), lerp(58.0, e2[1], u), u))
    if SN_HIT < f <= SN_BACK:
        u = (f - SN_HIT) / (SN_BACK - SN_HIT)
        x, y = curve(u)
        tail = [curve(max(0.0, u - d)) for d in (0.12, 0.24, 0.36)]
        for (qx, qy), (rx, ry) in zip([(x, y)] + tail, tail):
            comet(c, g, qx, qy, rx, ry, vpal, L['violet'], r=1.6 if (qx, qy) == (x, y) else 0.8, gain=0.8)
    if 0 <= a <= 1:                                                        # the hit on the decoy
        star4(c, g, hit_pt[0], hit_pt[1], 5 if a == 0 else 3, core=K['white'], col=K['violet3'], edge=K3['crim4'],
              light=L['violet'], gain=1.3)
    rng = np.random.default_rng(4105)
    for i in range(12):                                                    # straw bursts out and floats down
        if not 0 < a <= 11:
            break
        a0 = rng.uniform(0.3, math.pi - 0.3) + (0.6 if i % 2 else -0.4)
        sp = rng.uniform(0.9, 2.2)
        drift = min(a, 3) * sp
        x = hit_pt[0] + math.cos(a0) * drift + math.sin(a * 0.9 + i) * 1.2
        y = hit_pt[1] - math.sin(a0) * drift + max(0, a - 3) * 0.9
        ang = a * 0.7 + i
        m = c.line(x, y, x + math.cos(ang) * 1.6, y + math.sin(ang) * 1.6)
        c.put(m, K4['straw3'] if i % 2 else K4['straw2'], 1 - a / 13)
    smoke = 1 <= a <= 6                                                    # violet smoke from the hood
    if smoke:
        for j, (dx, dy) in enumerate(((-2, -8), (2, -10), (-4, -12))):
            r = 1.5 + (a - 1) * 0.7
            m = c.disc(SN_BASE[0] + dx, 18 + dy - a * 0.6, r)
            c.put(m, K['violet1'], 0.6 * (1 - a / 7))
            c.put(m & c.disc(SN_BASE[0] + dx - 0.5, 17.5 + dy - a * 0.6, r * 0.6), K['violet2'], 0.6 * (1 - a / 7))
    if 0 <= hit2 <= 2:                                                     # the turned bolt hits the other enemy
        star4(c, g, e2[0] - 2, e2[1], {0: 6, 1: 4, 2: 2}[hit2], core=K['white'], col=K['violet3'], edge=K3['crim4'],
              light=LR['crimson'], gain=1.4)
    sparks(c, g, f, SN_BACK, e2[0] - 2, e2[1], 4107, 11, (0.8, 2.4), 6, CRIM_SPARKS, LR['crimson'], gravity=0.15)
    motes(c, g, t, 4109, 6, (4, 6, 84, 52), K['violet3'], K3['crim4'], L['violet'], gain=0.4)
    vignette(c, 0.72)
    return c, g

# ---------------------------------------------------------------------------------------- Sombra Esquiva
SE_DAGGER = (44.0, 41.0)      # guard of the dagger at rest (pointing up-right)
SE_ANG = -1.0
SE_S = 0.62
SE_LAMP = (22.0, 17.0)        # the lantern hanging from the post
SE_POST = (12, 17)            # x span of the wooden post the arrow ends in
SE_SINK = 5                   # the dagger slips into its shadow
SE_OUT = 9                    # and comes back out
SE_THUNK = 8                  # the arrow hits the post
SE_PILE = (72.0, 50.0)        # the draw pile on the floor
SE_PEEL = 12                  # Combo: the shadow peels off as a card
SE_IN = 17                    # the card slides onto the pile


def warm_wall(c, g, t, lamp, flick):
    """Stone blocks lit by a lantern: each block takes one tone by its distance to the light (no dithered light)."""
    c.put(c.rect(0, 0, AW - 1, 52), K3['temple0'])
    tones = (K3['temple1'], K3['temple2'], hexc('#3a2c3c'), hexc('#4c3840'))
    for row, y in enumerate(range(-2, 52, 7)):
        off = 0 if row % 2 else 7
        for x in range(-14 + off, AW, 14):
            m = c.rect(x + 1, y + 1, x + 13, y + 6)
            d = math.hypot(x + 7 - lamp[0], y + 3.5 - lamp[1]) / (1.0 + 0.06 * flick)
            k = 3 if d < 11 else 2 if d < 21 else 1 if d < 32 else 0
            c.put(m, tones[k])
            c.put(m & (c.y < y + 2), tones[min(3, k + 1)] if k else K3['temple2'])
    c.put(c.rect(0, 52, AW - 1, AH - 1), K3['temple0'])
    c.put(c.rect(0, 52, AW - 1, 52), K3['temple3'])
    for x in range(0, AW, 9):
        c.put(c.rect(x, 56 + (x // 9) % 3, x + 5, 56 + (x // 9) % 3), K3['temple1'])


def lantern(c, g, x, y, t):
    flick = 0.5 + 0.5 * math.sin(t * math.pi * 6) * math.sin(t * math.pi * 10 + 1)
    c.put(c.line(SE_POST[1], y - 6, x, y - 6) | c.line(x, y - 6, x, y - 4), KR['iron1'])
    box = c.rect(int(x) - 2, int(y) - 3, int(x) + 2, int(y) + 3)
    c.put(outline_mask(box), K['ink'])
    c.put(box, KR['iron2'])
    glass = c.rect(int(x) - 1, int(y) - 2, int(x) + 1, int(y) + 2)
    c.put(glass, K['window2'])
    flame = c.rect(int(x), int(y) - 1, int(x), int(y) + 1)
    c.put(flame, K['window'])
    c.put(c.pixel(x + 0.5, y - 1.5), KR['spark2'] if flick > 0.5 else K['window'])
    g.add(glass, L['warm'], 0.8 + 0.4 * flick)
    g.add(c.disc(x + 0.5, y + 0.5, 5) & ~box, L['warm'], 0.08 + 0.05 * flick)
    return flick


def shadow_of(c, gx, gy, ang, s, lamp, stretch=1.25):
    """Where the lantern throws the dagger's shadow on the wall: pushed away from the light and stretched."""
    ux, uy = dir_of(ang)
    mx, my = gx + ux * 12 * s, gy + uy * 12 * s
    dx, dy = mx - lamp[0], my - lamp[1]
    d = math.hypot(dx, dy)
    ox, oy = dx / d * 10.0, dy / d * 10.0
    lab, u, v = dagger_labels(c, gx + ox, gy + oy, ang, s=s * stretch)
    return lab > 0, (mx + ox + ux * 3, my + oy + uy * 3)


def sombra_esquiva_frame(f):
    t = f / FRAMES
    c, g = Canvas(AW, AH), Canvas(AW, AH)
    flick = 0.5 + 0.5 * math.sin(t * math.pi * 6) * math.sin(t * math.pi * 10 + 1)
    warm_wall(c, g, t, SE_LAMP, flick)
    post = c.rect(SE_POST[0], 0, SE_POST[1], 52)
    c.put(outline_mask(post), K['ink'])
    c.put(post, KR['wood1'])
    c.put(post & (c.x < SE_POST[0] + 2), KR['wood2'])
    c.put(post & (c.x > SE_POST[1] - 1), KR['wood0'])
    c.put(post & ((np.floor(c.y / 5) + c.x) % 7 == 0), KR['wood0'])
    lantern(c, g, SE_LAMP[0], SE_LAMP[1], t)
    draw_pile(c, g, SE_PILE[0], SE_PILE[1], 3, glow=0.8 if SE_IN <= f <= SE_IN + 2 else 0.0,
              bump=1 if f == SE_IN else 0)
    bob = math.sin(t * math.pi * 4) * 0.8
    gx, gy = SE_DAGGER[0], SE_DAGGER[1] + bob
    # the shadow on the wall (gone while it is a card, back as the lantern relights it)
    shade = 1.0
    if SE_PEEL <= f < SE_PEEL + 8:
        shade = 0.0
    elif SE_PEEL + 8 <= f <= SE_PEEL + 10:
        shade = (f - SE_PEEL - 7) / 4.0
    sh, sc = shadow_of(c, gx, round(gy), SE_ANG, SE_S, SE_LAMP)
    deep = SE_SINK <= f < SE_OUT
    if shade > 0:
        c.put(sh, K['body0'], shade)
        c.put(sh & ~shift(sh, 1, 1), K3['temple0'], shade)
        if deep:                                                     # the dagger is inside its shadow
            rim = outline_mask(sh)
            c.put(rim, K['violet2'])
            g.add(rim, L['violet'], 0.7)
            c.put(sh & ((c.x + c.y + f) % 5 < 1), K['violet1'])
            g.add(sh & ((c.x + c.y + f) % 5 < 1), L['violet'], 0.3)
    if f == SE_PEEL:                                                 # Combo: the shadow lifts off the wall
        c.put(sh, K['violet1'], 0.8)
        g.add(outline_mask(sh), L['violet'], 0.8)
        star4(c, g, sc[0], sc[1], 3, core=K['white'], col=K['violet3'], edge=K['violet2'], light=L['violet'])
    if SE_PEEL < f < SE_IN:                                          # it flies to the pile as a card (Daga Oculta)
        k = ease_in_out((f - SE_PEEL) / (SE_IN - SE_PEEL))
        x = lerp(sc[0], SE_PILE[0] - 3, k)
        y = lerp(sc[1], SE_PILE[1] - 3, k) - math.sin(k * math.pi) * 12
        paint_card(c, g, x, y, math.cos(k * math.pi * 2) if k < 0.75 else 1.0, glow=0.6)
        for back in (1, 2):
            kb = ease_in_out(max(0.0, (f - back * 0.5 - SE_PEEL) / (SE_IN - SE_PEEL)))
            px_ = lerp(sc[0], SE_PILE[0] - 3, kb)
            py_ = lerp(sc[1], SE_PILE[1] - 3, kb) - math.sin(kb * math.pi) * 12
            m = c.disc(px_, py_, 1.2)
            c.put(m, K['violet2'], 0.6 / back)
            g.add(m, L['violet'], 0.5 / back)
    if SE_IN <= f <= SE_IN + 1:
        star4(c, g, SE_PILE[0] - 3, SE_PILE[1] - 10, 4 if f == SE_IN else 2, core=K['white'], col=K['violet3'],
              edge=K['violet2'], light=L['violet'])
    # the arrow: flies in from the right, through where the dagger was, into the post
    src, end = (100.0, 36.0), (SE_POST[1] + 3.0, 32.0)
    ang = math.atan2(end[1] - src[1], end[0] - src[0])
    if 3 <= f < SE_THUNK:
        k = (f - 2) / (SE_THUNK - 2)
        ax, ay = lerp(src[0], end[0], k), lerp(src[1], end[1], k)
        arrow(c, g, ax, ay, ang, s=1.1)
        speed_lines(c, g, ax + 20, ay, -1.0, 0.0, 1.0, 4201, count=3, spread=2, length=(5, 8))
    elif SE_THUNK <= f <= 19:
        a = f - SE_THUNK
        q = {0: 1, 1: -1, 2: 1}.get(a, 0)
        fade = 1.0 if a < 9 else max(0.0, 1 - (a - 8) / 3.0)
        arrow(c, g, end[0], end[1], ang + q * 0.06, a=fade, s=1.1)
        if a >= 9:
            converge(c, g, f, end[0] + 10, end[1], 17, 4, 4203, 6, 8, (K['violet3'], K['violet2']), L['violet'])
    if SE_THUNK <= f <= SE_THUNK + 1:
        star4(c, g, end[0] - 1, end[1], 3 if f == SE_THUNK else 2, core=K['white'], col=KR['spark2'], edge=KR['spark1'],
              light=L['warm'])
    sparks(c, g, f, SE_THUNK, end[0], end[1], 4205, 7, (0.6, 1.6), 5, WARM_SPARKS, L['warm'], gravity=0.18,
           spread=(-1.2, 1.2))
    # the dagger: rests, melts into its shadow as the arrow comes, steps back out of it
    if f < SE_SINK or f > SE_OUT:
        dagger(c, g, gx, round(gy), SE_ANG, SE_S, t * 2, -2 + (f - 20) * 10.0 if 20 <= f <= 23 else None)
    elif f == SE_SINK:
        dagger(c, g, gx + 2, round(gy) + 2, SE_ANG, SE_S, t * 2, ghost=(K['violet3'], 0.8, L['violet']))
        converge(c, g, 1, sc[0], sc[1], 0, 2, 4207, 8, 10, (K['violet3'], K['violet2']), L['violet'])
    elif f == SE_OUT:
        ox, oy = (sc[0] - gx - 12 * SE_S * math.cos(SE_ANG)) * 0.5, (sc[1] - gy - 12 * SE_S * math.sin(SE_ANG)) * 0.5
        dagger(c, g, gx + ox, round(gy + oy), SE_ANG, SE_S, t * 2, ghost=(K['violet3'], 0.5, L['violet']))
        dagger(c, g, gx + ox * 0.35, round(gy + oy * 0.35), SE_ANG, SE_S, t * 2, rune_gain=1.6)
    # 7 de escudo: a ward rises round the dagger once it is back
    if SE_OUT <= f <= SE_OUT + 7:
        mx, my = tip_of(gx, gy, SE_ANG, SE_S, 20.0)
        ward_rings(c, g, mx, my, f - SE_OUT, size=12, squash=1.0)
    motes(c, g, t, 4209, 6, (20, 6, 86, 50), K['violet3'], KR['spark1'], L['violet'], gain=0.4)
    vignette(c, 0.72)
    return c, g


# ---------------------------------------------------------------------------------------- Capa de Sombras
CS_COLLAR = (38.0, 12.0)
CS_GLASS = (71.0, 39.0)       # the hourglass
CS_WARD = (38.0, 35.0)


def cs_open(f):
    keys = {0: 0.04, 1: 0.04, 2: 0.25, 3: 0.65, 4: 0.9, 5: 1.0, 6: 1.0, 7: 1.0, 8: 0.6, 9: 0.2, 10: 0.06, 11: 0.04,
            12: 0.04, 13: 0.04, 14: 0.04, 15: 0.5, 16: 0.95, 17: 1.0, 18: 1.0, 19: 0.6, 20: 0.2, 21: 0.06, 22: 0.04,
            23: 0.04}
    return keys[f]


def starry_cloak(c, t, x, y0, open_, hold=0.0):
    """Her cloak of night floating on its own: returns (flaps, lining, hood, trim) masks. open_ 0..1 parts the front
    and spreads the sides like wings; hold pulls it tight (the wave pushing on it)."""
    flaps = np.zeros((AH, AW), bool)
    lining = np.zeros((AH, AW), bool)
    trim = np.zeros((AH, AW), bool)
    H = 40
    for y in range(int(y0), min(AH, int(y0) + H)):
        k = (y - y0) / H
        wav = math.sin((k * 1.6 - t * 2) * math.pi * 2) * (0.2 + 1.3 * k) * (1 - hold)
        if k < 0.16:                                                  # collar to shoulders
            half = 2.2 + 7.3 * ease_in_out(k / 0.16)
        else:
            half = 9.5 + 6.0 * ((k - 0.16) / 0.84) ** 0.9
        half += open_ * 6.5 * math.sin(min(1.0, max(0.0, k - 0.1) * 1.6) * math.pi / 2) - hold * 2.0 * k
        gap = open_ * (1.2 + 10.0 * ((k - 0.1) / 0.9) ** 0.7) if k > 0.1 else 0.0
        xc = x + wav * 0.5
        X = c.x[y]
        row = np.abs(X - xc) <= half
        if k > 0.94:                                                  # the hem: a soft wave
            row &= np.sin((X - xc) * 0.9 + t * math.pi * 4) > (k - 0.94) * 18 - 1
        flaps[y] = row & (np.abs(X - xc) > gap)
        lining[y] = row & (np.abs(X - xc) <= gap)
        trim[y] = row & (np.abs(np.abs(X - xc) - gap) < 1.0) & (gap > 0.6)
    hood = (((c.x - x + 1.0) / 6.5) ** 2 + ((c.y - y0 + 1.0) / 5.5) ** 2) <= 1
    p0x, p0y, p1x, p1y = x - 3.0, y0 - 4.0, x - 8.5, y0 - 8.5          # its point falls back
    L2_ = (p1x - p0x) ** 2 + (p1y - p0y) ** 2
    u = ((c.x - p0x) * (p1x - p0x) + (c.y - p0y) * (p1y - p0y)) / L2_
    d = np.abs((c.x - p0x) * (p1y - p0y) - (c.y - p0y) * (p1x - p0x)) / math.sqrt(L2_)
    hood |= (u >= 0) & (u <= 1) & (d <= 2.6 * (1 - u) + 0.3)
    hood &= c.y < y0 + 3
    return flaps, lining, hood & ~flaps, trim & flaps


def hourglass(c, g, x, y, th, top_sand, stream):
    """An hourglass on a plinth; th = its turn (0 upright, pi turned over); top_sand = 0..1 of the sand in its upper
    bulb (in its own frame)."""
    co, si = math.cos(th), math.sin(th)
    dx, dy = c.x - x, c.y - y
    lx, ly = dx * co + dy * si, -dx * si + dy * co
    plates = (np.abs(lx) <= 5.2) & (np.abs(np.abs(ly) - 8.2) <= 1.0)
    posts = (np.abs(np.abs(lx) - 4.4) <= 0.5) & (np.abs(ly) <= 7.4)
    bulb_w = 0.7 + 3.0 * np.clip(np.abs(ly) / 7.0, 0, 1) ** 0.6 * np.clip((7.6 - np.abs(ly)) / 2.2, 0, 1) ** 0.4
    glass = (np.abs(lx) <= bulb_w) & (np.abs(ly) <= 7.2)
    sand_top = glass & (ly < 0) & (ly > -7.2 * top_sand * 0.95) & (np.abs(lx) <= bulb_w - 0.6)
    sand_bot = glass & (ly > 0) & (ly > 7.2 - 6.6 * (1 - top_sand)) & (np.abs(lx) <= bulb_w - 0.6)
    m = plates | posts | glass
    c.put(outline_mask(m), K['ink'])
    c.put(glass, K4['glass1'])
    c.put(glass & (lx < -0.8), K4['glass2'])
    c.put(sand_top | sand_bot, KR['gold2'])
    c.put((sand_top | sand_bot) & (lx < 0), KR['gold3'])
    if stream:
        st = glass & (np.abs(lx) < 0.6) & (ly >= -0.5) & (ly < 7.2 - 6.6 * (1 - top_sand))
        c.put(st, KR['gold3'])
    c.put(plates | posts, KR['wood1'])
    c.put((plates | posts) & (lx < -3.6), KR['wood2'])
    g.add(glass & ~(sand_top | sand_bot), L['blue'], 0.12)
    return m


def nebula(c, g, t):
    gradient_bg(c, K['vio1'], K['vio2'], K['vio0'], 0.6)
    rng = np.random.default_rng(4301)
    for i in range(6):                                                # soft nebula clouds (dithered, in the paint)
        x, y, r = rng.uniform(0, AW), rng.uniform(4, 40), rng.uniform(8, 16)
        c.put(c.disc(x, y, r), K['vio3'], 0.25)
        c.put(c.disc(x + 2, y - 1, r * 0.5), K['violet0'], 0.3)
    twinkling_stars(c, g, t, 4303, 22, (2, 2, 86, 46), light=L['violet'])
    dais = (((c.x - 44) / 46) ** 2 + ((c.y - 60) / 8) ** 2) <= 1
    c.put(dais, K3['temple1'])
    c.put(dais & ~shift(dais, 0, 1), K3['temple3'])
    ring = ellipse_ring(c, CS_COLLAR[0], 56, 18, 3.2, 0.5)
    ang = np.arctan2((c.y - 56) / 3.2, (c.x - CS_COLLAR[0]) / 18)
    ring &= ((ang / (2 * math.pi) * 14 + t * 2) % 1) < 0.6
    c.put(ring, K['violet2'])
    g.add(ring, L['violet'], 0.35)
    plinth = c.rect(int(CS_GLASS[0]) - 5, 49, int(CS_GLASS[0]) + 5, 56)
    c.put(outline_mask(plinth), K['ink'])
    c.put(plinth, K3['temple2'])
    c.put(plinth & (c.y < 50), K3['temple3'])


def capa_de_sombras_frame(f):
    t = f / FRAMES
    c, g = Canvas(AW, AH), Canvas(AW, AH)
    nebula(c, g, t)
    # the hourglass: sand runs, it turns over (a new turn begins) and runs again
    if f <= 9:
        top, th, stream = 0.5 - 0.4 * f / 9, 0.0, True
    elif f <= 12:
        top, th, stream = 0.1, math.pi * ease_in_out((f - 9) / 3.0), False
    else:
        top, th, stream = 0.9 - 0.4 * (f - 13) / 11, 0.0, f > 13
    if 10 <= f <= 12:                                       # while it turns, its sand is still in the old bulb
        top = 0.1
    hourglass(c, g, CS_GLASS[0], CS_GLASS[1], th, top, stream)
    if f == 12:
        star4(c, g, CS_GLASS[0], CS_GLASS[1], 4, core=K['white'], col=K['moon2'], edge=K['moon1'], light=L['blue'])
    op = cs_open(f)
    hold = 1.0 if 13 <= f <= 14 else 0.0
    x, y0 = CS_COLLAR
    flaps, lining, hood, trim = starry_cloak(c, t, x, y0, op, hold)
    whole = flaps | lining | hood
    # the ward inside the cloak (8 de escudo): forms while it is open, is kept through the turn, fades at the end
    wk = 0.0
    if 4 <= f <= 20:
        wk = min(1.0, (f - 3) / 3.0) if f < 19 else max(0.0, 1 - (f - 18) / 3.0)
    c.put(outline_mask(whole), K['ink'])
    c.put(lining, K4['lining1'])
    c.put(lining & (c.y > y0 + 28), K4['lining0'])
    c.put(lining & (c.y < y0 + 14), K4['lining2'])
    rng = np.random.default_rng(4305)
    for i in range(14):                                              # the stars sewn in its lining
        sx, sy = x + rng.uniform(-9, 9), y0 + rng.uniform(6, 38)
        b = 0.5 + 0.5 * math.sin((t * (1 + i % 2) + rng.random()) * math.pi * 2)
        m = c.pixel(sx, sy) & lining
        c.put(m, K['white'] if b > 0.7 else K['moon1'])
        g.add(m, L['white'] if b > 0.7 else L['violet'], 0.4 * b)
    if wk > 0:
        keep = lining
        def ward_draw(c2, g2):
            ky = ease_out(min(1.0, wk))
            for i, (r, w) in enumerate(((4 + 5 * ky, 0.6), (2 + 3 * ky, 0.5))):
                ring = diamond_ring(c2, CS_WARD[0], CS_WARD[1], r, w, squash=1.35)
                c2.put(ring, KB['b2'] if i == 0 else KB['b1'], wk)
                c2.put(ring & (((c2.x + c2.y + f * 2) % 7) < 2), KB['b3'], wk)
                g2.add(ring, LB, (0.9 if i == 0 else 0.5) * wk)
            core = c2.pixel(CS_WARD[0], CS_WARD[1])
            c2.put(core, KB['b3'], wk)
            g2.add(core, LB, 0.8 * wk)
        clipped(c, g, keep, ward_draw)
        g.add(trim, LB, 0.25 * wk)                                    # its light spills on the gold trim
    # the cloak's cloth
    xc = x
    c.put(flaps, K3['cloth1'])
    c.put(flaps & (np.sin((c.x - xc) * 0.9 + (c.y - y0) * 0.12 + t * math.pi * 4) > 0.6), K3['cloth2'])
    c.put(flaps & ~shift(flaps, -1, 0) & (c.x < xc), K3['cloth3'])
    c.put(flaps & ~shift(flaps, 1, 0) & (c.x > xc), K3['cloth0'])
    c.put(trim, KR['gold2'])
    c.put(trim & (c.y < y0 + 16), KR['gold3'])
    hem = flaps & ~shift(flaps, 0, -1) & (c.y > y0 + 30)
    c.put(hem, KR['gold1'])
    c.put(flaps & (np.abs(c.y - y0 - 6.5) < 0.6), K3['cloth0'])           # the seam across the shoulders
    c.put(hood, K3['cloth0'])
    c.put(hood & ~shift(hood, 0, 1), K3['cloth2'])
    clasp = c.disc(x, y0 + 2, 1.5)
    c.put(clasp, KR['gold2'])
    c.put(c.pixel(x, y0 + 2), K['cyan3'])
    g.add(c.pixel(x, y0 + 2), L['cyan'], 0.8)
    # the cold wave of the new turn: from the hourglass, it breaks on the cloak
    a = f - 12
    if 0 <= a <= 4:
        r = 6 + a * 11
        ring = ellipse_ring(c, CS_GLASS[0], CS_GLASS[1], r, r * 0.8, 0.7) & (c.x < CS_GLASS[0] + 2)
        right = np.where(whole.any(axis=1), AW - 1 - np.argmax(whole[:, ::-1], axis=1), -1)
        blocked = c.x < (right[:, None] + 2.5)                       # the cloak's lee: the wave does not get past it
        ring &= ~blocked
        fade = 1 - a / 5
        c.put(ring, K['moon2'], fade)
        c.put(ring & (c.B < 0.5), KB['b3'], fade)
        g.add(ring, L['blue'], 0.7 * fade)
    if 13 <= f <= 14:                                                # the cloak holds: its edge flares
        edge = outline_mask(whole) & (c.x > x)
        c.put(edge, K['violet3'])
        g.add(edge, L['violet'], 1.0)
    sparks(c, g, f, 13, x + 14, y0 + 22, 4307, 10, (0.8, 2.0), 5, VIOLET_SPARKS, L['violet'], gravity=0.06,
           spread=(-1.4, 1.4))
    if f == 16:                                                      # still there: the ward was kept
        star4(c, g, CS_WARD[0], CS_WARD[1], 5, core=K['white'], col=KB['b3'], edge=KB['b2'], light=LB, gain=1.3)
    sparks(c, g, f, 16, CS_WARD[0], CS_WARD[1], 4309, 9, (0.6, 1.6), 5, BLUE_SPARKS, LB, gravity=0.04)
    motes(c, g, t, 4311, 7, (4, 6, 84, 52), K['violet3'], KB['b2'], L['violet'], gain=0.45)
    vignette(c, 0.72)
    return c, g


# ---------------------------------------------------------------------------------------- Corte Afortunado
CA_WELL = (15.0, 44.0)        # centre of the well's rim
CA_DAGGER = (41.0, 31.0)      # guard at rest, pointing at the enemy
CA_ANG = -0.15
CA_S = 0.62
CA_SIGIL = (77.0, 26.0)
CA_POP = 2                    # the coin leaps out of the well
CA_SHOW = 8                   # it shows its clover over the blade
CA_HIT = 11                   # the critical hit
CA_COIN = (53.0, 14.0)


def lucky_coin(c, g, x, y, phase, size=1.0, a=1.0, lit=0.0):
    """A gold coin spinning (width |cos phase|); face on, it shows a four-leaf clover (luck)."""
    wide = abs(math.cos(phase))
    rx, ry = (0.7 + 3.0 * wide) * size, 3.6 * size
    m = (((c.x - x) / rx) ** 2 + ((c.y - y) / ry) ** 2) <= 1
    if not m.any():
        m = c.pixel(x, y)
    c.put(outline_mask(m), K['ink'], a)
    c.put(m, KR['gold2'], a)
    c.put(m & (c.x < x - 0.3 * rx) & (c.y < y), KR['gold3'], a)
    c.put(m & (c.x > x + 0.4 * rx), KR['gold1'], a)
    if wide > 0.65:
        rim = m & ~erode(m)
        c.put(rim, KR['gold1'], a)
        leaves = np.zeros_like(m)
        for dx, dy in ((-1.1, -1.1), (1.1, -1.1), (-1.1, 1.1), (1.1, 1.1)):
            leaves |= c.disc(x + dx * size * wide, y + dy * size, 1.05 * size)
        c.put(leaves, K2['pois2'], a)
        c.put(leaves & (c.x < x) & (c.y < y), K2['pois3'], a)
        c.put(c.pixel(x, y), K2['pois4'], a)
        g.add(leaves, LUCK, (0.25 + 0.5 * lit) * a)
    g.add(m & ~erode(m), GOLD, (0.1 + 0.3 * lit) * a)
    return m


def gold_runes(c, g, lab, k):
    """Turn the dagger's cyan runes gold (luck in the blade), k 0..1."""
    if k <= 0:
        return
    fl = lab == 5
    c.put(fl, KR['gold2'], k)
    c.put(fl & (c.B < 0.5), KR['gold3'], k)
    g.add(fl, GOLD, 1.0 * k)


def wishing_plaza(c, g, t):
    gradient_bg(c, K['night0'], K['night2'], K['night1'], 0.6)
    twinkling_stars(c, g, t, 4401, 14, (2, 2, 86, 24))
    houses = c.poly([(0, 30), (8, 22), (16, 30), (16, 26), (28, 26), (28, 20), (36, 14), (44, 20), (44, 30), (54, 30),
                     (54, 24), (62, 18), (70, 24), (70, 28), (88, 28), (88, 50), (0, 50)])
    c.put(houses, K['roof'])
    c.put(houses & ~shift(houses, 0, 1), K['night3'])
    for i, (wx, wy) in enumerate(((5, 33), (31, 24), (60, 30), (80, 33))):
        win = c.rect(wx, wy, wx + 1, wy + 2)
        on = math.sin((t * (1 + i % 2) + i * 0.37) * math.pi * 2) > -0.5
        c.put(win, K['window2'])
        c.put(c.pixel(wx + 0.5, wy + 1.5), K['window'] if on else K['window2'])
        g.add(win, L['warm'], 0.25 if on else 0.08)
    cobbles(c, 49)


def well(c, g, t, splash=0):
    x, y = CA_WELL
    body = c.rect(int(x) - 11, int(y), int(x) + 11, 53) & ((((c.x - x) / 11.5) ** 2 + ((c.y - 53) / 3.0) ** 2 <= 1) | (c.y < 53))
    c.put(outline_mask(body), K['ink'])
    c.put(body, KR['stone2'])
    c.put(body & (c.x > x + 5), KR['stone1'])
    c.put(body & (c.x < x - 6), KR['stone3'])
    for row, yy in enumerate(range(int(y) + 2, 54, 4)):
        off = 0 if row % 2 else 3
        for xx in range(int(x) - 12 + off, int(x) + 12, 6):
            c.put(c.rect(xx, yy, xx, yy + 3) & body, KR['stone0'])
        c.put(c.rect(int(x) - 11, yy, int(x) + 11, yy) & body, KR['stone0'])
    # its little roof on two posts, a rope and a bucket
    posts = c.rect(int(x) - 10, int(y) - 15, int(x) - 9, int(y)) | c.rect(int(x) + 9, int(y) - 15, int(x) + 10, int(y))
    roof = c.poly([(x - 13, y - 14), (x, y - 21), (x + 13, y - 14), (x + 11, y - 13), (x - 11, y - 13)])
    beam = c.rect(int(x) - 10, int(y) - 12, int(x) + 10, int(y) - 11)
    rope = c.rect(int(x), int(y) - 11, int(x), int(y) - 6)
    bucket = c.rect(int(x) - 2, int(y) - 6, int(x) + 2, int(y) - 3)
    frame = posts | roof | beam | bucket
    c.put(outline_mask(frame), K['ink'])
    c.put(posts | beam, KR['wood1'])
    c.put(posts & (c.x < x - 9), KR['wood2'])
    c.put(roof, KR['tile2'])
    c.put(roof & ~shift(roof, 0, 1), K['night3'])
    c.put(rope, K4['straw1'])
    c.put(bucket, KR['wood2'])
    c.put(bucket & (np.abs(c.y - (y - 4.5)) < 0.6), KR['iron2'])
    rim = (((c.x - x) / 12) ** 2 + ((c.y - y) / 3.4) ** 2) <= 1
    water = (((c.x - x) / 9.5) ** 2 + ((c.y - y) / 2.2) ** 2) <= 1
    c.put(outline_mask(rim), K['ink'])
    c.put(rim, KR['stone3'])
    c.put(rim & (c.y < y - 1.5) & (c.x < x), hexc('#3a4a6a'))
    c.put(rim & (c.y > y), KR['stone2'])
    c.put(water, K3['water1'])
    c.put(water & (np.abs(c.x - x - 3 - math.sin(t * math.pi * 4)) < 1.4) & (c.y < y), K['moon1'])   # the moon in it
    g.add(water & (c.y < y) & (np.abs(c.x - x - 3) < 2), L['blue'], 0.2)
    if splash > 0:
        ring = ellipse_ring(c, x, y, 2 + splash * 2.2, 0.8 + splash * 0.5, 0.5) & water
        c.put(ring, K3['water3'], 1 - splash / 5)


def corte_afortunado_frame(f):
    t = f / FRAMES
    c, g = Canvas(AW, AH), Canvas(AW, AH)
    wishing_plaza(c, g, t)
    well(c, g, t, splash=(f - CA_POP) if 0 <= f - CA_POP <= 4 else 0)
    hit = f - CA_HIT
    jx = (1 if hit % 2 == 0 else -1) if 0 <= hit <= 2 else 0
    sigil(c, g, CA_SIGIL[0] + jx, CA_SIGIL[1], 6, 6, t * 1.5, 1.0 if 0 <= hit <= 5 else 0.9,
          col=K['white'] if hit == 0 else K3['crim4'] if 0 <= hit <= 4 else K3['crim3'], light=LR['crimson'])
    eye = c.disc(CA_SIGIL[0] + jx, CA_SIGIL[1], 1.6)
    c.put(eye, K3['crim5'] if not 0 <= hit <= 6 else K3['crim1'])
    g.add(eye, LR['crimson'], 0.8)
    if 1 <= hit <= 8:                                               # its sigil cracks
        cr = c.line(CA_SIGIL[0] - 5, CA_SIGIL[1] - 4, CA_SIGIL[0] + 4, CA_SIGIL[1] + 5) | \
             c.line(CA_SIGIL[0] + 4, CA_SIGIL[1] - 5, CA_SIGIL[0] - 3, CA_SIGIL[1] + 4)
        c.put(cr, KR['gold3'] if hit <= 3 else K3['crim5'], 1 - hit / 10)
        g.add(cr, GOLD, 0.8 * (1 - hit / 10))
    # the coin: leaps out of the well, spins over to the blade, shows its clover, bursts into gold
    a = f - CA_POP
    if 0 <= a <= CA_SHOW - CA_POP:
        u = a / (CA_SHOW - CA_POP)
        x = lerp(CA_WELL[0], CA_COIN[0], u)
        y = lerp(CA_WELL[1] - 1, CA_COIN[1], u) - math.sin(u * math.pi) * 9
        ph = a * 1.1 if a < CA_SHOW - CA_POP else 0.0
        lucky_coin(c, g, x, y, ph, size=1.0 + 0.8 * u, lit=1.0 if a == CA_SHOW - CA_POP else 0.0)
        if a == 0:
            sparks(c, g, 1, 0, CA_WELL[0], CA_WELL[1] - 1, 4403, 6, (0.6, 1.4), 3, (K3['water3'], K['moon1'], K3['water2']),
                   L['blue'], gravity=0.3, spread=(0.6, 2.5))
    if f == CA_SHOW:
        star4(c, g, CA_COIN[0] + 6, CA_COIN[1] - 5, 3, core=K['white'], col=K2['pois4'], edge=K2['pois3'], light=LUCK)
        ring = ellipse_ring(c, CA_COIN[0], CA_COIN[1], 9, 9, 0.5) & (((c.x + c.y) % 3) < 2)
        c.put(ring, K2['pois3'], 0.7)
        g.add(ring, LUCK, 0.5)
    if f == CA_SHOW + 1:                                            # the coin bursts; its gold runs into the blade
        lucky_coin(c, g, CA_COIN[0], CA_COIN[1] + 2, 0.0, size=1.8, a=0.5, lit=1.0)
        star4(c, g, CA_COIN[0], CA_COIN[1] + 2, 5, core=K['white'], col=KR['gold3'], edge=KR['gold2'], light=GOLD)
    gx_, gy_ = tip_of(CA_DAGGER[0], CA_DAGGER[1], CA_ANG, CA_S, 18.0)
    converge(c, g, f, gx_, gy_, CA_SHOW + 1, 3, 4405, 10, 10, (KR['gold3'], KR['gold2']), GOLD)
    sparks(c, g, f, CA_SHOW + 1, CA_COIN[0], CA_COIN[1], 4407, 8, (0.5, 1.3), 4, GOLD_SPARKS, GOLD, gravity=0.05)
    # the dagger: rest, gold, wind-up, the thrust, back
    lunge = {9: -3.0, 10: -4.0, 11: 12.0, 12: 11.0, 13: 9.0, 14: 6.0, 15: 3.0, 16: 1.0}.get(f, 0.0)
    ux, uy = dir_of(CA_ANG)
    bob = math.sin(t * math.pi * 4) * 0.8 if not 9 <= f <= 16 else 0.0
    gx, gy = CA_DAGGER[0] + ux * lunge, CA_DAGGER[1] + uy * lunge + bob
    if f == CA_HIT:
        dagger(c, g, CA_DAGGER[0] + ux * 2, round(CA_DAGGER[1] + uy * 2), CA_ANG, CA_S, t * 2,
               ghost=(KR['gold3'], 0.5, GOLD))
        speed_lines(c, g, gx + ux * 6, gy + uy * 6, ux, uy, 1.0, 4409, count=5, spread=4, length=(6, 10))
    lab = dagger(c, g, round(gx), round(gy), CA_ANG, CA_S, t * 2, -2 + (f - 18) * 10.0 if 18 <= f <= 22 else None,
                 rune_gain=1.4 if 9 <= f <= 14 else 1.0)
    gk = 0.0 if f < CA_SHOW + 1 else 1.0 if f <= 15 else max(0.0, 1 - (f - 15) / 3.0)
    gold_runes(c, g, lab, gk)
    if 0 <= hit <= 2:                                               # critical: a big gold star and a second cut
        star4(c, g, CA_SIGIL[0] - 2, CA_SIGIL[1] + 1, {0: 8, 1: 5, 2: 3}[hit], core=K['white'], col=KR['gold3'],
              edge=KR['gold2'], light=GOLD, gain=1.5)
    if 1 <= hit <= 3:
        q = (hit - 1) / 3.0
        slash_line(c, g, CA_SIGIL[0], CA_SIGIL[1], 1.0, 11 * (1 - q * 0.3), 2.2 * (1 - q),
                   (KR['gold0'], KR['gold2'], KR['gold3'], K['white']), GOLD, a=1 - q)
        slash_line(c, g, CA_SIGIL[0], CA_SIGIL[1], -1.0, 11 * (1 - q * 0.3), 2.2 * (1 - q),
                   (KR['gold0'], KR['gold2'], KR['gold3'], K['white']), GOLD, a=1 - q)
    sparks(c, g, f, CA_HIT, CA_SIGIL[0] - 2, CA_SIGIL[1], 4411, 12, (1.0, 2.6), 6, GOLD_SPARKS, GOLD, gravity=0.15)
    plus_marks(c, g, CA_SIGIL[0] + 5, CA_SIGIL[1] - 9, hit - 1 if hit >= 1 else -1, count=2, col=KR['gold3'], light=GOLD)
    motes(c, g, t, 4413, 7, (4, 6, 84, 48), KR['gold2'], K2['pois3'], GOLD, gain=0.4)
    vignette(c, 0.72)
    return c, g


# ---------------------------------------------------------------------------------------- Hoja Única
HU_GUARD = (44.0, 13.0)       # the dagger hangs point down over the enemy's sigil
HU_ANG = math.pi / 2
HU_S = 0.62
HU_MIRRORS = (15.0, 73.0)     # the two mirrors at the sides, each with a copy of the blade
HU_CRACK, HU_BURST = 4, 6
HU_DIVE, HU_HIT = 10, 11
HU_FLOOR = (44.0, 56.0)


def mirror_frame(c, g, x, glass=1.0, cracks=0.0, t=0.0):
    """A tall arched mirror on the wall: iron frame with gold beading; glass 0..1 (0 = broken out, dark)."""
    pane = c.rect(int(x) - 6, 16, int(x) + 6, 44) | (c.disc(x + 0.5, 16, 6.5) & (c.y < 16))
    frame = dilate(dilate(pane)) & ~pane
    c.put(outline_mask(frame | pane), K['ink'])
    c.put(frame, KR['iron1'])
    c.put(frame & (c.x < x - 6), KR['iron2'])
    c.put(frame & (((c.y + c.x) % 4) < 1), KR['gold1'])
    c.put(pane, K['ink'])
    if glass > 0:                                                     # the silver comes back from the top down
        gl = pane & (c.y < 9 + 37 * glass)
        c.put(gl, K4['glass1'])
        c.put(gl & (c.y > 30), K4['glass0'])
        sheen = gl & (np.abs((c.x - x) + (c.y - 30) * 0.5 + 4 - math.sin(t * math.pi * 2) * 3) < 1.0)
        c.put(sheen, K4['glass2'])
        c.put(gl & ~shift(pane, -1, 0), K4['glass2'])
        if glass < 1:
            c.put(gl & ~shift(gl, 0, -1) & (c.y > 10), K4['glass3'])
    if cracks > 0:
        cr = (c.line(x, 28, x - 5, 18) | c.line(x, 28, x + 4, 22) | c.line(x, 28, x + 5, 40) | c.line(x, 28, x - 4, 42) |
              c.line(x, 28, x - 6, 31)) & pane
        c.put(cr, K['white'], cracks)
        g.add(cr, L['white'], 0.6 * cracks)
    return pane


def reflection(c, g, pane, gx, gy, ang, s, flicker=0.0):
    """The copy of the blade in a mirror: the same dagger in the glass's cold tones, flat (a reflection)."""
    lab, u, v = dagger_labels(c, gx, gy, ang, s=s)
    lab[~pane] = 0
    m = lab > 0
    c.put(outline_mask(m) & pane, K4['glass0'])
    tones = {1: K4['glass3'], 2: K4['glass2'], 3: K4['glass2'], 4: K4['glass1'], 5: K['violet2'], 6: K4['glass3'],
             7: K4['glass2'], 8: K4['glass1'], 9: K4['glass2'], 10: K['violet3'], 11: K4['glass2'], 12: K4['glass3']}
    for k, col in tones.items():
        c.put(lab == k, col)
    g.add((lab == 5) | (lab == 10), L['violet'], 0.4 + 0.6 * flicker)


def mirror_hall(c, g, t):
    gradient_bg(c, K['vio0'], K['vio2'], K['vio1'], 0.6)
    wall = c.rect(0, 0, AW - 1, 50)
    c.put(wall, K['vio1'])
    for x in range(0, AW, 11):                                        # wall panels
        c.put(c.rect(x, 4, x, 50), K['vio0'])
    c.put(c.rect(0, 48, AW - 1, 50), K['vio2'])
    floor = c.rect(0, 51, AW - 1, AH - 1)
    c.put(floor, K['vio0'])
    for row, y in enumerate((52, 55, 59)):
        for x in range(-8 + row * 4, AW, 9 + row * 3):
            c.put(c.rect(x, y, x + 5 + row * 2, y + row), K['vio1'])
    for x in (30, 58):                                                # candelabra glow between the mirrors
        c.put(c.rect(x, 34, x, 48), KR['iron1'])
        fl = c.rect(x, 32, x, 33)
        c.put(fl, K['window'])
        g.add(fl, L['warm'], 0.5 + 0.2 * math.sin((t * 3 + x) * math.pi * 2))


def hoja_unica_frame(f):
    t = f / FRAMES
    c, g = Canvas(AW, AH), Canvas(AW, AH)
    mirror_hall(c, g, t)
    hit = f - HU_HIT
    # the enemy's sigil on the floor, under the blade
    sk = 1.0 if 0 <= hit <= 4 else 0.85
    ring = ellipse_ring(c, HU_FLOOR[0], HU_FLOOR[1], 10, 2.8, 0.55)
    ang = np.arctan2((c.y - HU_FLOOR[1]) / 2.8, (c.x - HU_FLOOR[0]) / 10)
    ring &= ((ang / (2 * math.pi) * 10 + t * 2) % 1) < 0.7
    c.put(ring, K['white'] if hit == 0 else K3['crim4'] if 0 <= hit <= 4 else K3['crim3'], sk)
    g.add(ring, LR['crimson'], 0.6)
    inner = ellipse_ring(c, HU_FLOOR[0], HU_FLOOR[1], 5, 1.4, 0.5)
    c.put(inner, K3['crim2'])
    # mirrors: copies of the blade in them; they crack, burst, and silver back over at the end
    if f < HU_CRACK:
        glass, cracks = 1.0, 0.0
    elif f < HU_BURST:
        glass, cracks = 1.0, (f - HU_CRACK + 1) / 2.0
    elif f < 19:
        glass, cracks = 0.0, 0.0
    else:
        glass, cracks = (f - 18) / 5.0, 0.0
    bob = math.sin(t * math.pi * 4) * 0.8
    for i, mx in enumerate(HU_MIRRORS):
        pane = mirror_frame(c, g, mx, glass, cracks, t + i * 0.3)
        if glass >= 1.0:
            jit = (1 if f % 2 else -1) if HU_CRACK <= f < HU_BURST else 0
            reflection(c, g, pane, mx + jit, round(15 + bob), HU_ANG, 0.6, cracks)
    rng = np.random.default_rng(4501)
    for i in range(16):                                               # the glass bursts outwards and falls
        a = f - HU_BURST
        if not 0 <= a <= 7:
            break
        mx = HU_MIRRORS[i % 2]
        a0 = rng.uniform(0, 2 * math.pi)
        sp = rng.uniform(0.8, 2.0)
        x = mx + math.cos(a0) * sp * a * 1.2
        y = 28 + rng.uniform(-10, 10) - math.sin(a0) * sp * a + 0.35 * a * a
        m = c.pixel(x, y) | c.pixel(x + 1, y + (1 if i % 3 else 0))
        c.put(m, K4['glass3'] if a < 3 or i % 3 == 0 else K4['glass2'], 1 - a / 9)
        g.add(m, L['blue'], 0.4 * (1 - a / 8))
    if f == HU_BURST:
        for mx in HU_MIRRORS:
            star4(c, g, mx, 28, 4, core=K['white'], col=K4['glass3'], edge=K4['glass2'], light=L['blue'])
    # Singular: what was in the mirrors comes into the one blade, which glows violet
    for i, mx in enumerate(HU_MIRRORS):
        converge(c, g, f, HU_GUARD[0], 26, HU_BURST + 1, 3, 4503 + i, 6, abs(mx - HU_GUARD[0]),
                 (K['violet4'], K['violet3']), L['violet'], gain=1.1)
    power = 0.0 if f < HU_BURST + 1 else 1.0 if f <= 15 else max(0.0, 1 - (f - 15) / 4.0)
    drop = {9: -4.0, 10: 6.0, 11: 13.0, 12: 13.0, 13: 12.0, 14: 8.0, 15: 4.0, 16: 1.0}.get(f, 0.0)
    gx, gy = HU_GUARD[0], HU_GUARD[1] + drop + (bob if not 9 <= f <= 16 else 0)
    if f == HU_DIVE:
        for back, al in ((6.0, 0.35), (2.0, 0.6)):
            dagger(c, g, gx, round(gy - back), HU_ANG, HU_S, t * 2, ghost=(K['violet3'], al, L['violet']))
        speed_lines(c, g, gx, gy, 0.0, 1.0, 1.0, 4505, count=5, spread=5, length=(6, 10))
    cut = (HU_FLOOR[1] - 1 - gy) / HU_S if drop >= 12 else None
    lab = dagger(c, g, gx, round(gy), HU_ANG, HU_S, t * 2, -2 + (f - 19) * 10.0 if 19 <= f <= 22 else None,
                 rune_gain=1.0 + 1.2 * power, cut=cut)
    if power > 0:                                                     # violet runes and a violet rim: Singular
        fl = lab == 5
        c.put(fl, K['violet3'], power)
        c.put(fl & (c.B < 0.4), K['violet4'], power)
        g.add(fl, L['violet'], 1.2 * power)
        g.add(dilate(lab > 0) & ~(lab > 0), L['violet'], 0.35 * power)
    if 0 <= hit <= 2:
        star4(c, g, HU_FLOOR[0], HU_FLOOR[1] - 1, {0: 8, 1: 5, 2: 3}[hit], core=K['white'], col=K['violet4'],
              edge=K['violet3'], light=L['violet'], gain=1.5)
        for k, rr in enumerate((4 + hit * 5, 2 + hit * 3)):
            shock = ellipse_ring(c, HU_FLOOR[0], HU_FLOOR[1], rr * 1.6, rr * 0.45, 0.6)
            c.put(shock, K['violet3'] if k == 0 else K['white'], 1 - hit / 3)
            g.add(shock, L['violet'], 0.7 * (1 - hit / 3))
    sparks(c, g, f, HU_HIT, HU_FLOOR[0], HU_FLOOR[1] - 2, 4507, 12, (1.0, 2.4), 6, VIOLET_SPARKS, L['violet'],
           gravity=0.2, spread=(0.3, math.pi - 0.3))
    plus_marks(c, g, HU_FLOOR[0] + 8, HU_FLOOR[1] - 10, hit - 1 if hit >= 1 else -1, count=2, col=K['violet4'],
               light=L['violet'])
    if hit == 0:
        shake(c, g, 0, 1)
    motes(c, g, t, 4509, 7, (4, 6, 84, 50), K['violet3'], K4['glass3'], L['violet'], gain=0.45)
    vignette(c, 0.72)
    return c, g


# ---------------------------------------------------------------------------------------- Ráfaga de Cortes
RC_GONG = (63.0, 29.0)
RC_R = 11.0
RC_HOME = (16.0, 40.0)
RC_ANG = -0.6
RC_S = 0.6
RC_CARDS = ((8.0, 12.0, -0.2), (18.0, 10.0, -0.07), (28.0, 10.0, 0.07), (38.0, 12.0, 0.2))   # played this turn; the
                              # fourth is this card
RC_CUTS = ((5, -0.55), (7, 0.6), (9, -0.05), (11, 1.15))  # (frame, angle of the cut); the fourth is this card
RC_BACK = 14


def gong(c, g, x, y, r, swing=0.0, ring=0.0, scars=()):
    """A bronze gong hanging from a beam on two cords; scars = [(angle, glow 0..1)] cut marks across it."""
    x += swing
    for sx in (-5, 5):
        c.put(c.line(x + sx * 0.6, y - r + 1, RC_GONG[0] + sx, 10), K4['straw1'])
    disc_ = c.disc(x, y, r)
    c.put(outline_mask(disc_), K['ink'])
    c.put(disc_, K4['bronze2'])
    c.put(disc_ & ~c.disc(x, y, r - 1.5), K4['bronze1'])
    c.put(disc_ & ~c.disc(x, y, r - 1.5) & (c.x < x) & (c.y < y), K4['bronze3'])
    c.put(ellipse_ring(c, x, y, r * 0.62, r * 0.62, 0.5), K4['bronze1'])
    boss = c.disc(x, y, 2.6)
    c.put(boss, K4['bronze3'])
    c.put(c.disc(x - 0.8, y - 0.8, 1.1), K4['bronze4'])
    c.put(disc_ & (np.abs((c.x - x) + (c.y - y) + r * 0.45) < 0.8) & ~boss, K4['bronze3'])          # sheen
    for ang, gl in scars:
        ux, uy = math.cos(ang), math.sin(ang)
        m = c.line(x - ux * (r - 2), y - uy * (r - 2), x + ux * (r - 2), y + uy * (r - 2)) & disc_
        c.put(m, K['white'] if gl > 0.7 else K4['bronze4'] if gl > 0.25 else K4['bronze0'])
        g.add(m, L['cyan'], 0.9 * gl)
    if ring > 0:
        for k in range(2):
            rr = r + 2 + (1 - ring) * 8 + k * 4
            m = ellipse_ring(c, x, y, rr, rr, 0.5) & (((c.x + c.y) % 3) < 2)
            c.put(m, K4['bronze4'], ring * (1 - k * 0.4))
            g.add(m, GOLD, 0.4 * ring * (1 - k * 0.4))
    return disc_


def gong_court(c, g, t):
    gradient_bg(c, K['night0'], K['teal2'], K['night1'], 0.6)
    twinkling_stars(c, g, t, 4601, 12, (2, 2, 86, 20))
    roof = c.poly([(0, 26), (10, 20), (30, 20), (40, 26), (40, 30), (0, 30)])
    c.put(roof, K['roof'])
    c.put(roof & ~shift(roof, 0, 1), K['teal3'])
    c.put(c.rect(2, 30, 38, 50), K['roof2'])
    for x in (8, 18, 28):
        lit = c.rect(x, 36, x + 2, 41)
        c.put(lit, K['window2'])
        c.put(lit & (c.y > 39), hexc('#5a3324'))
        g.add(lit, L['warm'], 0.15)
    floor = c.rect(0, 50, AW - 1, AH - 1)
    c.put(floor, KR['stone1'])
    c.put(c.rect(0, 50, AW - 1, 50), KR['stone3'])
    for x in range(-4, AW, 12):
        c.put(c.line(x, 51, x - 4, 63), KR['stone0'])
    # the gong's wooden frame
    frame = c.rect(int(RC_GONG[0]) - 17, 8, int(RC_GONG[0]) - 15, 50) | c.rect(int(RC_GONG[0]) + 15, 8, int(RC_GONG[0]) + 17, 50)
    frame |= c.rect(int(RC_GONG[0]) - 19, 7, int(RC_GONG[0]) + 19, 9)
    c.put(outline_mask(frame), K['ink'])
    c.put(frame, KR['wood1'])
    c.put(frame & (c.y < 8), KR['wood2'])
    c.put(c.rect(int(RC_GONG[0]) - 21, 6, int(RC_GONG[0]) - 19, 7) | c.rect(int(RC_GONG[0]) + 19, 6, int(RC_GONG[0]) + 21, 7),
          KR['wood2'])


def rafaga_de_cortes_frame(f):
    t = f / FRAMES
    c, g = Canvas(AW, AH), Canvas(AW, AH)
    gong_court(c, g, t)
    # the gong: each cut leaves a scar that glows, then dulls; it swings and rings after the flurry
    scars = []
    for k, (cf, ang) in enumerate(RC_CUTS):
        a = f - cf
        if a >= 0 and f <= 20:
            scars.append((ang, 1.0 if a == 0 else max(0.0, 0.8 - a * 0.08)))
    swing = math.sin((f - RC_CUTS[-1][0]) * 0.9) * 2.0 * max(0.0, 1 - (f - 11) / 9.0) if f >= 11 else 0.0
    last = max([cf for cf, _ in RC_CUTS if cf <= f], default=None)
    ring = max(0.0, 1 - (f - last) / 4.0) if last is not None else 0.0
    gong(c, g, RC_GONG[0], RC_GONG[1], RC_R, round(swing), ring, scars)
    # the cards played this turn, lighting one by one; the fourth is this card, which joins the row as it is played
    for k, (cx, cy, ca) in enumerate(RC_CARDS):
        cf = RC_CUTS[k][0]
        if k == 3 and not cf - 2 <= f <= 19:
            continue
        al = 1.0 if k < 3 else min(1.0, (f - cf + 3) / 2.0) if f < cf else 1.0 if f <= 17 else max(0.0, 1 - (f - 17) / 3.0)
        lit = 1.0 if f == cf - 1 or f == cf else 0.45 if cf < f <= 17 else 0.0
        card_rot(c, g, cx, cy - (1 if f in (cf - 1, cf) else 0), ca, size=0.9, alpha=al, lit=lit)
        if f == cf - 1:
            star4(c, g, cx, cy - 8, 3, core=K['white'], col=KR['gold3'], edge=KR['gold2'], light=GOLD)
        if f == cf:                                               # its light leaps to the blade
            ux, uy = dir_of(RC_CUTS[k][1])
            m = c.line(cx, cy + 6, RC_GONG[0] - 6, RC_GONG[1] - 4)
            c.put(m & (((c.x + c.y) % 3) < 1), KR['gold3'])
            g.add(m & (((c.x + c.y) % 3) < 1), GOLD, 0.5)
    # the dagger: home, then a flurry round the gong, then back
    flurry = RC_CUTS[0][0] - 1 <= f <= RC_CUTS[-1][0] + 1
    if f in [cf for cf, _ in RC_CUTS]:
        k = [cf for cf, _ in RC_CUTS].index(f)
        ang = RC_CUTS[k][1]
        ux, uy = dir_of(ang)
        hx, hy = RC_GONG[0] + ux * (RC_R + 5), RC_GONG[1] + uy * (RC_R + 5)
        pal = RPAL if k == 3 else CPAL
        slash_line(c, g, RC_GONG[0] + swing, RC_GONG[1], ang, RC_R + 7, 2.4 if k == 3 else 1.8, pal,
                   LR['crimson'] if k == 3 else L['cyan'])
        dagger(c, g, hx, hy, ang, RC_S, t * 2, rune_gain=2.0)
        star4(c, g, RC_GONG[0] + swing - ux * 4, RC_GONG[1] - uy * 4, 5 if k == 3 else 3, gain=1.3,
              col=K3['crim5'] if k == 3 else K['cyan3'], edge=K3['crim3'] if k == 3 else K['cyan2'],
              light=LR['crimson'] if k == 3 else L['cyan'])
    elif flurry:
        prev = [cf for cf, _ in RC_CUTS if cf < f]
        k = len(prev) - 1
        if k >= 0:                                                    # between cuts: after-images round the gong
            ang = RC_CUTS[k][1] + math.pi * 0.9
            ux, uy = dir_of(ang)
            dagger(c, g, RC_GONG[0] + ux * (RC_R + 6), RC_GONG[1] + uy * (RC_R + 6), ang + math.pi, RC_S, t * 2,
                   ghost=(K['cyan3'], 0.55, L['cyan']))
        else:
            dagger(c, g, RC_HOME[0] + 6, RC_HOME[1] - 3, RC_ANG, RC_S, t * 2, ghost=(K['cyan3'], 0.6, L['cyan']))
    else:
        if RC_CUTS[-1][0] + 1 < f <= RC_BACK:
            k = ease_out((f - RC_CUTS[-1][0] - 1) / (RC_BACK - RC_CUTS[-1][0] - 1))
            x0, y0 = RC_GONG[0] - 14, RC_GONG[1] + 6
            gx, gy = lerp(x0, RC_HOME[0], k), lerp(y0, RC_HOME[1], k)
        else:
            gx, gy = RC_HOME[0], RC_HOME[1] + math.sin(t * math.pi * 4) * 0.8
        dagger(c, g, round(gx), round(gy), RC_ANG, RC_S, t * 2, -2 + (f - 18) * 10.0 if 18 <= f <= 22 else None)
    for cf, ang in RC_CUTS:
        sparks(c, g, f, cf, RC_GONG[0], RC_GONG[1], 4603 + cf, 6, (0.8, 2.0), 4, WARM_SPARKS, L['warm'], gravity=0.2)
    if f == RC_CUTS[-1][0]:
        shake(c, g, 1, 0)
    motes(c, g, t, 4605, 6, (4, 6, 84, 48), K['cyan3'], KR['gold2'], L['cyan'], gain=0.4)
    vignette(c, 0.72)
    return c, g


# ---------------------------------------------------------------------------------------- Tirar los Dados
TD_CUP = (11.0, 52.0)         # bottom centre of the dice cup
TD_FLOOR = 54.0               # where the die rests (its bottom)
# the die in the air: frame -> (x, y of its centre, turn, face shown)
TD_FLIGHT = {3: (22.0, 40.0, 0.4, 5), 4: (29.0, 36.0, 1.3, 2), 5: (35.0, 48.0, 2.0, 6), 6: (40.0, 42.0, 2.9, 4),
             7: (45.0, 48.0, 3.5, 2)}
TD_FIRST = (48.0, 1)          # lands at x showing 1
TD_LUCK = 9                   # a clover: the luck's extra roll
TD_REROLL = {10: (52.0, 41.0, 0.7, 6), 11: (56.0, 37.0, 1.6, 2), 12: (60.0, 46.0, 2.4, 5)}
TD_SECOND = (63.0, 3)         # lands on 3: three mana
TD_LIGHT = 14
TD_POOF = 20

PIPS = {1: ((0, 0),), 2: ((-1, -1), (1, 1)), 3: ((-1, -1), (0, 0), (1, 1)), 4: ((-1, -1), (1, -1), (-1, 1), (1, 1)),
        5: ((-1, -1), (1, -1), (0, 0), (-1, 1), (1, 1)), 6: ((-1, -1), (1, -1), (-1, 0), (1, 0), (-1, 1), (1, 1))}


def die_in_air(c, g, x, y, th, face, s=1.35):
    """A die tumbling: a rounded square turned by th, with the pips of the face it shows."""
    co, si = math.cos(th), math.sin(th)
    dx, dy = c.x - x, c.y - y
    lx, ly = (dx * co + dy * si) / s, (-dx * si + dy * co) / s
    m = (np.abs(lx) <= 3.8) & (np.abs(ly) <= 3.8) & ~((np.abs(lx) > 2.9) & (np.abs(ly) > 2.9))
    c.put(outline_mask(m), K['ink'])
    c.put(m, K4['bone2'])
    c.put(m & (lx + ly < -2.5), K4['bone3'])
    c.put(m & (lx + ly > 3.0), K4['bone1'])
    for px, py in PIPS[face]:
        qx, qy = x + (px * co - py * si) * 2.1 * s, y + (px * si + py * co) * 2.1 * s
        c.put(c.disc(qx, qy, 0.75), K4['bone0'])
    return m


def die_landed(c, g, x, base, face, lit=0.0):
    """A die at rest in three-quarter view: front face (10x10) with the result, top and side faces."""
    xi, by = int(x), int(base)
    front = c.rect(xi - 5, by - 10, xi + 4, by - 1)
    front &= ~(c.pixel(xi - 5, by - 10) | c.pixel(xi + 4, by - 10) | c.pixel(xi - 5, by - 1) | c.pixel(xi + 4, by - 1))
    top = c.poly([(xi - 5, by - 10), (xi + 5, by - 10), (xi + 8, by - 13.5), (xi - 2, by - 13.5)])
    side = c.poly([(xi + 5, by - 10), (xi + 8, by - 13.5), (xi + 8, by - 3.5), (xi + 5, by)])
    m = front | top | side
    c.put(outline_mask(m), K['ink'])
    c.put(top, K4['bone3'])
    c.put(side, K4['bone1'])
    c.put(front, K4['bone2'])
    c.put(front & ~shift(front, 1, 0), K4['bone3'])                        # lit left edge
    c.put(front & ~shift(front, 0, -1) & (c.x > xi - 4), K4['bone1'])      # shaded bottom edge
    c.put(c.rect(xi + 1, by - 12, xi + 2, by - 12), K4['bone1'])           # the pip on the top face
    pips = []
    for px, py in PIPS[face]:
        x0, y0 = xi - 1 + px * 3, by - 6 + py * 3
        q = c.rect(x0, y0, x0 + 1, y0 + 1)
        pips.append((x0 + 1.0, y0 + 1.0))
        c.put(q, K4['bone0'] if lit <= 0 else KR['mana2'] if lit < 0.9 else KR['mana3'])
        if lit > 0:
            g.add(q, LR['mana'], 1.2 * lit)
    return m, pips


def dice_cup(c, g, x, y, tilt=0.0, rattle=0):
    """A leather dice cup with gold bands; tilt turns it (towards the right) about its bottom right corner."""
    x += rattle
    co, si = math.cos(tilt), math.sin(tilt)
    px, py = x + 6.0, y
    dx, dy = c.x - px, c.y - py
    lx, ly = dx * co + dy * si + 6.0, -dx * si + dy * co
    body = (ly <= 0) & (ly >= -15) & (np.abs(lx) <= 4.4 + (-ly) * 0.11)
    c.put(outline_mask(body), K['ink'])
    c.put(body, K4['boot2'])
    c.put(body & (lx < -2.6), K4['boot3'])
    c.put(body & (lx > 2.4), K4['boot1'])
    c.put(body & (lx > 4.0), K4['boot0'])
    c.put(body & (ly < -13.4), KR['gold2'])
    c.put(body & (ly < -13.4) & (lx > 2.4), KR['gold1'])
    c.put(body & (ly > -1.6), KR['gold1'])
    return body


def gambling_den(c, g, t):
    c.put(np.ones((AH, AW), bool), K['vio1'])
    for x in range(0, AW, 7):                                             # plank wall
        c.put(c.rect(x, 0, x, 40), K['vio0'])
    lamp = (46.0, 6.0)
    for r, tone in ((26, K['vio2']), (16, hexc('#3a2238')), (8, hexc('#4c2c3a'))):   # the lamp's warm pool, in steps
        pool = c.disc(lamp[0], lamp[1] + 16, r) & (c.y < 41) & ~((c.x.astype(int) % 7) < 1)
        c.put(pool, tone)
    c.put(c.line(lamp[0], 0, lamp[0], 3), KR['iron1'])
    shade = c.poly([(lamp[0] - 4, 7), (lamp[0] + 4, 7), (lamp[0] + 2, 3), (lamp[0] - 2, 3)])
    c.put(outline_mask(shade), K['ink'])
    c.put(shade, KR['iron2'])
    bulb = c.rect(int(lamp[0]) - 1, 7, int(lamp[0]) + 1, 8)
    c.put(bulb, K['window'])
    g.add(bulb, L['warm'], 1.0 + 0.2 * math.sin(t * math.pi * 6))
    floor = c.rect(0, 41, AW - 1, AH - 1)
    c.put(floor, K['vio0'])
    c.put(c.disc(46, 50, 24) & (c.y > 41) & (c.y < 60), K['vio1'])
    c.put(c.disc(46, 48, 13) & (c.y > 41) & (c.y < 56), hexc('#2e1a2c'))
    for y in (43, 46, 50, 55, 61):                                        # floor planks running away from us
        c.put(c.rect(0, y, AW - 1, y), K['ink'])
    c.put(c.rect(0, 41, AW - 1, 41), K['ink'])
    chalk = ellipse_ring(c, 52, 51, 22, 5.5, 0.5)
    ang = np.arctan2((c.y - 51) / 5.5, (c.x - 52) / 22)
    chalk &= ((ang / (2 * math.pi) * 18) % 1) < 0.65
    c.put(chalk, K['moon0'])


def tirar_los_dados_frame(f):
    t = f / FRAMES
    c, g = Canvas(AW, AH), Canvas(AW, AH)
    gambling_den(c, g, t)
    # the cup rattles, tips the die out, stands up again
    rattle = {0: 1, 1: -1, 2: 1}.get(f, 0)
    tilt = {3: 0.9, 4: 1.0, 5: 0.7, 6: 0.35, 7: 0.1}.get(f, 0.0)
    dice_cup(c, g, TD_CUP[0], TD_CUP[1], tilt, rattle)
    if f <= 2:                                                            # rattle marks
        for k in range(2):
            sx = TD_CUP[0] - 8 + k * 16 + rattle
            m = c.line(sx, TD_CUP[1] - 12 - (f + k) % 2, sx + (1 if k else -1), TD_CUP[1] - 14 - (f + k) % 2)
            c.put(m, K4['bone2'])
    if f == 3:
        star4(c, g, TD_CUP[0] + 8, TD_CUP[1] - 12, 2, core=K['white'], col=K4['bone3'], edge=K4['bone2'], light=L['white'],
              gain=0.5)
    # the die
    pips = []
    if f in TD_FLIGHT:
        x, y, th, face = TD_FLIGHT[f]
        die_in_air(c, g, x, y, th, face)
        if f > 3:
            px, py = TD_FLIGHT[f - 1][:2]
            m = c.line(px, py, x - 3, y) & (((c.x + c.y) % 2) < 1)
            c.put(m, K4['bone1'])
    elif 8 <= f <= TD_LUCK:
        die_landed(c, g, TD_FIRST[0], TD_FLOOR, TD_FIRST[1])
    elif f in TD_REROLL:
        x, y, th, face = TD_REROLL[f]
        die_in_air(c, g, x, y, th, face)
        m = c.disc(x - 3, y + 2, 1.5)
        c.put(m, K2['pois3'], 0.8)
        g.add(m, LUCK, 0.6)
    elif 13 <= f < TD_POOF:
        lit = 0.0 if f < TD_LIGHT else 1.0 if f <= 18 else 0.5
        _, pips = die_landed(c, g, TD_SECOND[0], TD_FLOOR, TD_SECOND[1], lit)
    for b in (5, 7):                                                      # dust where it bounces
        dust_puffs(c, TD_FLIGHT[b][0], TD_FLOOR, f, b, 2, 0.9, rise=0.2, life=3, seed=4701 + b, big=0.6)
    dust_puffs(c, TD_FIRST[0], TD_FLOOR, f, 8, 2, 0.9, rise=0.2, life=3, seed=4709, big=0.7)
    dust_puffs(c, TD_SECOND[0], TD_FLOOR, f, 13, 2, 1.0, rise=0.2, life=4, seed=4711, big=0.8)
    # luck: a four-leaf clover pops over the die and makes it roll again
    a = f - TD_LUCK
    if -1 <= a <= 2:
        cx, cy = TD_FIRST[0] + 2, 30 - a
        k = {-1: 0.55, 0: 1.25, 1: 1.2, 2: 0.7}[a]
        leaves = np.zeros((AH, AW), bool)
        for dx, dy in ((-1.7, -1.7), (1.7, -1.7), (-1.7, 1.7), (1.7, 1.7)):
            leaves |= c.disc(cx + dx * k, cy + dy * k, 1.75 * k)
        stem = c.line(cx + 1, cy + 2, cx + 3, cy + 7) if k > 0.7 else np.zeros_like(leaves)
        c.put(outline_mask(leaves | stem), K['ink'])
        c.put(leaves, K2['pois2'])
        c.put(leaves & (c.x < cx) & (c.y < cy), K2['pois3'])
        c.put(stem, K2['pois1'])
        c.put(c.pixel(cx, cy), K2['pois4'])
        g.add(leaves, LUCK, 0.7)
        if a == 0:
            star4(c, g, cx + 6, cy - 4, 3, core=K['white'], col=K2['pois4'], edge=K2['pois3'], light=LUCK)
    sparks(c, g, f, TD_LUCK, TD_FIRST[0] + 2, 30, 4713, 8, (0.6, 1.5), 4, POISON_SPARKS, LUCK, gravity=0.05)
    # three pips light up blue and three mana drops rise from them
    if f == TD_LIGHT:
        star4(c, g, TD_SECOND[0], TD_FLOOR - 5, 6, core=K['white'], col=KR['mana3'], edge=KR['mana2'],
              light=LR['mana'])
    for i in range(3):
        a = f - (TD_LIGHT + 1 + i)
        if 0 <= a <= 5:
            px, py = pips[i] if pips else (TD_SECOND[0], TD_FLOOR - 6)
            k = ease_out(a / 5.0)
            tx_, ty_ = TD_SECOND[0] + (i - 1) * 9, 12 + abs(i - 1) * 3
            mana_drop(c, g, round(lerp(px, tx_, k)), round(lerp(py, ty_, k)), 0.6 + 0.5 * min(1.0, a / 2.0),
                      gain=1.0 + 0.5 * (a == 5))
            if a == 5:
                star4(c, g, tx_, ty_, 3, col=KR['mana3'], edge=KR['mana2'], light=LR['mana'])
    # the die goes back into the cup in a puff of violet
    if TD_POOF <= f <= TD_POOF + 2:
        a = f - TD_POOF
        r = 4 + a * 1.8
        m = c.disc(TD_SECOND[0] + 1, TD_FLOOR - 7 - a, r)
        c.put(m, K['violet0'], 1 - a / 3)
        c.put(m & c.disc(TD_SECOND[0] + 0, TD_FLOOR - 8 - a, r * 0.7), K['violet1'], 1 - a / 3)
        c.put(m & c.disc(TD_SECOND[0] - 1, TD_FLOOR - 9 - a, r * 0.35), K['violet2'], 1 - a / 3)
        g.add(m, L['violet'], 0.3 * (1 - a / 3))
    converge(c, g, f, TD_CUP[0], TD_CUP[1] - 14, TD_POOF + 1, 3, 4715, 6, 14, (K['violet3'], K['violet2']), L['violet'])
    motes(c, g, t, 4717, 6, (4, 10, 84, 40), KR['spark1'], KR['mana2'], L['warm'], gain=0.35)
    vignette(c, 0.72)
    return c, g


# ---------------------------------------------------------------------------------------- Torbellino
TB_HOME = (18.0, 42.0)        # guard of the dagger at rest
TB_ANG = -0.9
TB_S = 0.6
TB_SIGIL = (66.0, 22.0)
TB_GROUND = 52.0
TB_HIT, TB_COMBO = 10, 14


def tb_state(f):
    """(x of the whirlwind, its strength 0..1, its size)"""
    if f <= 2:
        return TB_HOME[0] + 8, 0.0, 1.0
    if f <= 5:
        return TB_HOME[0] + 8, (f - 2) / 3.0, 1.0
    if f <= 9:
        return lerp(TB_HOME[0] + 8, TB_SIGIL[0] - 4, ease_in_out((f - 5) / 4.0)), 1.0, 1.0
    if f < TB_COMBO - 1:
        return TB_SIGIL[0] - 4, 1.0, 1.0
    if f <= 16:
        return TB_SIGIL[0] - 4, 1.0, 1.25
    if f <= 20:
        k = ease_in_out((f - 16) / 4.0)
        return lerp(TB_SIGIL[0] - 4, TB_HOME[0] + 8, k), 1.0 - k, 1.25 - 0.25 * k
    return TB_HOME[0] + 8, 0.0, 1.0


def funnel(c, g, wx, base, a, size, t):
    """The whirlwind: a funnel narrow at the ground, wide at the top, with spiral bands turning round it.
    a 0..1 grows it up from the ground. Returns its mask."""
    H = 30.0 * size * a
    if H < 2:
        return np.zeros((AH, AW), bool)
    k = np.clip((base - c.y) / H, 0, 1)                                  # 0 at the ground, 1 at the top
    sway = np.sin(k * math.pi * 1.2 + t * math.pi * 4) * 2.0 * k
    half = (1.6 + 12.0 * k ** 1.3) * size
    X = (c.x - wx - sway) / np.maximum(half, 0.5)
    m = (c.y <= base) & (c.y >= base - H) & (np.abs(X) <= 1)
    ph = np.arcsin(np.clip(X, -1, 1)) / math.pi                          # where round the funnel a pixel faces us
    band = ((ph + (base - c.y) * 0.16 - t * 6) % 1)
    c.put(m, K['teal2'])
    c.put(m & (band < 0.45), K['teal3'])
    c.put(m & (band < 0.22), K['cyan1'])
    c.put(m & (band < 0.08) & (np.abs(X) < 0.85), K['cyan3'])
    edge = m & ((np.abs(X) > 0.82) | ~shift(m, 0, 1))
    c.put(edge & (c.x < wx), K['cyan2'])
    g.add(m & (band < 0.22), L['cyan'], 0.35 * a)
    g.add(m & (band < 0.08), L['cyan'], 0.5 * a)
    return m


def moor(c, g, t):
    gradient_bg(c, K['teal0'], K['teal2'], K['teal1'], 0.6)
    rng = np.random.default_rng(4801)
    for i in range(5):                                                    # racing clouds
        y = rng.uniform(3, 22)
        w = rng.uniform(10, 22)
        x = (rng.uniform(0, 120) + t * 120 * (1 + i % 2)) % 130 - 25
        cl = (((c.x - x) / w) ** 2 + ((c.y - y) / 2.2) ** 2) <= 1
        c.put(cl, K['teal3'])
        c.put(cl & (c.y < y - 0.5), hexc('#244a66'))
    twinkling_stars(c, g, t, 4803, 8, (2, 2, 86, 24))
    for x, w, h, back in ((5, 7, 22, False), (19, 5, 14, True), (78, 8, 25, False), (63, 5, 13, True)):
        st = c.poly([(x, 52), (x + 0.5, 52 - h + 3), (x + w * 0.4, 52 - h), (x + w, 52 - h + 2), (x + w + 0.5, 52)])
        c.put(outline_mask(st), K['ink'])
        c.put(st, K4['heath1'] if back else KR['stone1'])
        if not back:
            c.put(st & (c.x < x + 2), KR['stone2'])
            c.put(st & ((c.x + c.y) % 9 < 1), K4['heath2'])
    ground = c.rect(0, 50, AW - 1, AH - 1)
    c.put(ground, K4['heath1'])
    c.put(c.rect(0, 50, AW - 1, 50), K4['heath2'])
    for i in range(16):
        x = rng.uniform(0, AW)
        sway = math.sin((t * 2 + rng.random()) * math.pi * 2) * 1.0
        c.put(c.line(x, 53, x + sway + 1, 49 - rng.uniform(0, 2)), K4['heath2'])


def torbellino_frame(f):
    t = f / FRAMES
    c, g = Canvas(AW, AH), Canvas(AW, AH)
    moor(c, g, t)
    wx, a, size = tb_state(f)
    hit = f - TB_HIT
    combo = f - TB_COMBO
    hurt = (0 <= hit <= 4) or (0 <= combo <= 3)
    # the dagger rides round the funnel: behind it half the turn, in front the other half
    ph = f * 1.7
    oy = TB_GROUND - 15 * size
    orbit = lambda q: (wx + math.cos(q) * 11.0 * size, oy + math.sin(q) * 3.0 * size)
    behind = math.sin(ph) < 0
    def ride(c_, g_):
        if a <= 0:
            return
        for back, al in ((0.7, 0.3), (0.35, 0.55)):
            q = ph - back
            x_, y_ = orbit(q)
            dagger(c_, g_, x_, y_, q + math.pi / 2, TB_S * 0.8, t * 2, ghost=(K['cyan3'], al, L['cyan']))
        x_, y_ = orbit(ph)
        if f in (3, 20):                                                 # easing in and out of the spin
            x_, y_ = lerp(TB_HOME[0], x_, 0.6), lerp(TB_HOME[1], y_, 0.6)
        dagger(c_, g_, round(x_), round(y_), ph + math.pi / 2, TB_S * 0.8, t * 2, rune_gain=1.8)
        if 0 <= combo <= 3 or 13 <= f <= 16:                              # Combo: a second blade rides with it
            x2, y2 = orbit(ph + math.pi)
            dagger(c_, g_, round(x2), round(y2), ph + math.pi * 1.5, TB_S * 0.75, t * 2, ghost=(K3['crim5'], 0.8,
                   LR['crimson']))
    if behind:
        ride(c, g)
    funnel(c, g, wx, TB_GROUND, a, size, t)
    jx = (1 if f % 2 else -1) if (0 <= hit <= 1 or 0 <= combo <= 1) else 0
    sigil(c, g, TB_SIGIL[0] + jx, TB_SIGIL[1], 6, 6, t * 1.5, 1.0 if hurt else 0.9,
          col=K['white'] if hit == 0 or combo == 0 else K3['crim4'] if hurt else K3['crim3'], light=LR['crimson'])
    eye = c.disc(TB_SIGIL[0] + jx, TB_SIGIL[1], 1.6)
    c.put(eye, K3['crim5'] if not hurt else K3['crim1'])
    g.add(eye, LR['crimson'], 0.8)
    if not behind:
        ride(c, g)
    if a <= 0:
        dagger(c, g, TB_HOME[0], round(TB_HOME[1] + math.sin(t * math.pi * 4) * 0.8), TB_ANG, TB_S, t * 2,
               -2 + (f - 21) * 10.0 if f >= 21 else None)
    # dust and leaves whirled up at its foot
    rng = np.random.default_rng(4805)
    for i in range(4):
        if a > 0.3:
            q = rng.random() * 2 * math.pi + t * math.pi * 2 * 3
            lx = wx + math.cos(q) * (5 + i * 2.5) * size
            ly = TB_GROUND - 3 - i * 6 * size + math.sin(q) * 1.2
            paint_leaf(c, g, lx, ly, q * 2, size=0.55)
        else:
            paint_leaf(c, g, 10 + i * 20 + rng.uniform(-2, 2), 56 + (i % 2) * 3, rng.uniform(0, 3), size=0.55)
    if a > 0.3:
        dust_puffs(c, wx, TB_GROUND + 1, (f % 4) + 1, 0, 2, 1.3, rise=0.3, life=5, seed=4815, big=0.8)
    if 6 <= f <= 9 or 17 <= f <= 19:
        dirn = 1.0 if f <= 9 else -1.0
        speed_lines(c, g, wx - dirn * 12, TB_GROUND - 14, dirn, 0.0, 1.0, 4807 + f, count=3, spread=8, length=(4, 7))
    # the hits: cuts across the sigil (8 de daño), then the Combo's (+4 de daño)
    for h, big in ((hit, False), (combo, True)):
        if 0 <= h <= 2:
            star4(c, g, TB_SIGIL[0], TB_SIGIL[1], {0: 6 if not big else 7, 1: 4, 2: 2}[h], gain=1.3,
                  col=K3['crim5'] if big else K['cyan3'], edge=K3['crim3'] if big else K['cyan2'],
                  light=LR['crimson'] if big else L['cyan'])
        if 0 <= h <= 1:
            for k, an in enumerate((0.5, -0.7)):
                slash_line(c, g, TB_SIGIL[0], TB_SIGIL[1], an + h * 0.3, 9, 1.5 * (1 - h * 0.5),
                           RPAL if big else CPAL, LR['crimson'] if big else L['cyan'], a=1 - h * 0.4)
    sparks(c, g, f, TB_HIT, TB_SIGIL[0], TB_SIGIL[1], 4809, 10, (0.8, 2.2), 5, CYAN_SPARKS, L['cyan'], gravity=0.12)
    sparks(c, g, f, TB_COMBO, TB_SIGIL[0], TB_SIGIL[1], 4811, 10, (0.8, 2.4), 5, CRIM_SPARKS, LR['crimson'],
           gravity=0.12)
    plus_marks(c, g, TB_SIGIL[0] + 7, TB_SIGIL[1] - 9, combo - 1 if combo >= 1 else -1, count=2)
    # its wind turns into a ward (5 de escudo), and a bigger one with the Combo (+4)
    if TB_HIT + 1 <= f <= TB_HIT + 6:
        ward_rings(c, g, TB_SIGIL[0] - 4, TB_GROUND - 13, f - TB_HIT - 1, size=10, squash=1.15, life=5)
    if TB_COMBO + 1 <= f <= TB_COMBO + 7:
        ward_rings(c, g, TB_SIGIL[0] - 4, TB_GROUND - 13, f - TB_COMBO - 1, size=15, squash=1.15, life=6)
    motes(c, g, t, 4813, 6, (4, 6, 84, 48), K['cyan3'], KB['b2'], L['cyan'], gain=0.4)
    vignette(c, 0.72)
    return c, g


# ---------------------------------------------------------------------------------------- Dagas Ocultas
DO_DAGGER = (25.0, 42.0)
DO_ANG = -0.85
DO_S = 0.62
DO_PILE = (73.0, 36.0)
# (peels off, folds into a card, slips into the pile, the way it drifts off the blade): the third is the Combo
DO_COPIES = ((3, 6, 9, (6.0, -12.0)), (6, 9, 12, (12.0, -4.0)), (10, 13, 16, (2.0, -18.0)))


def hidden_card(c, g, x, y, wscale, combo=False, glow=0.6):
    """A card back with a dagger drawn on it: a Daga Oculta."""
    m = paint_card(c, g, x, y, wscale, glow=glow)
    if abs(wscale) > 0.5:
        col = K['cyan3'] if combo else K['violet3']
        light = L['cyan'] if combo else L['violet']
        glyph = c.rect(int(x), int(y) - 4, int(x), int(y) + 2) | c.rect(int(x) - 1, int(y) + 1, int(x) + 1, int(y) + 1)
        glyph |= c.pixel(x, y + 3.5)
        c.put(glyph & m, col)
        g.add(glyph & m, light, 0.8)
    return m


def attic(c, g, t):
    c.put(np.ones((AH, AW), bool), K['night1'])
    for x in range(0, AW, 6):                                             # boarded wall
        c.put(c.rect(x, 0, x, 50), K['night0'])
    wx, wy = 11.0, 30.0
    win = c.disc(wx, wy, 6.5)                                             # round window with the moon
    c.put(dilate(win) & ~win, KR['wood1'])
    c.put(outline_mask(dilate(win)), K['ink'])
    c.put(win, K['night3'])
    c.put(c.disc(wx + 2, wy - 2, 3), K['moon1'])
    c.put(c.disc(wx + 1, wy - 3, 1.5), K['moon2'])
    c.put(win & ((np.abs(c.x - wx) < 0.6) | (np.abs(c.y - wy) < 0.6)), KR['wood1'])
    g.add(c.disc(wx + 2, wy - 2, 3), L['blue'], 0.3)
    beam = c.poly([(wx - 5, wy + 5), (wx + 6, wy + 5), (44, 52), (22, 52)])   # moonlight on the boards, in one step
    c.put(beam & ~((c.x.astype(int) % 6) < 1), K['night2'])
    c.put(beam & ((c.x.astype(int) % 6) < 1), K['night1'])
    for (x0, y0, x1, y1) in ((0, 22, 44, 0), (88, 22, 44, 0)):          # roof beams
        m = c.line(x0, y0, x1, y1) | c.line(x0, y0 + 1, x1, y1 + 1) | c.line(x0, y0 + 2, x1, y1 + 2)
        c.put(outline_mask(m), K['ink'])
        c.put(m, KR['wood1'])
        c.put(c.line(x0, y0, x1, y1), KR['wood2'])
    floor = c.rect(0, 51, AW - 1, AH - 1)
    c.put(floor, KR['wood0'])
    for y in (54, 58):
        c.put(c.rect(0, y, AW - 1, y), K['ink'])
    c.put(c.rect(0, 51, AW - 1, 51), KR['wood1'])
    c.put(beam & floor & (c.y < 58), KR['wood1'])
    crate = c.rect(int(DO_PILE[0]) - 10, 41, int(DO_PILE[0]) + 10, 52)
    c.put(outline_mask(crate), K['ink'])
    c.put(crate, KR['wood1'])
    c.put(crate & (c.y < 43), KR['wood2'])
    c.put(crate & (c.line(DO_PILE[0] - 9, 44, DO_PILE[0] + 9, 51) | c.line(DO_PILE[0] + 9, 44, DO_PILE[0] - 9, 51)), KR['wood0'])
    candle = c.rect(int(DO_PILE[0]) - 9, 37, int(DO_PILE[0]) - 8, 40)     # a candle stub by the pile
    c.put(candle, K4['bone2'])
    fl = c.pixel(DO_PILE[0] - 8.5, 35.5) | c.pixel(DO_PILE[0] - 8.5, 36.5)
    c.put(fl, K['window'])
    g.add(fl, L['warm'], 0.7 + 0.2 * math.sin(t * math.pi * 6))


def dagas_ocultas_frame(f):
    t = f / FRAMES
    c, g = Canvas(AW, AH), Canvas(AW, AH)
    attic(c, g, t)
    landed = [land for _, _, land, _ in DO_COPIES]
    bump = 1 if f in landed else 0
    glow = 1.0 if f in landed else 0.5 if 16 < f <= 19 else 0.0
    draw_pile(c, g, DO_PILE[0] + 2, DO_PILE[1] + 2, 3, glow=glow, bump=bump)
    bob = math.sin(t * math.pi * 4) * 0.8
    gx, gy = DO_DAGGER[0], DO_DAGGER[1] + bob
    shiver = 1.0 if any(p <= f < fo for p, fo, _, _ in DO_COPIES) else 0.0
    dagger(c, g, gx, round(gy), DO_ANG, DO_S, t * 2, -2 + (f - 19) * 10.0 if 19 <= f <= 22 else None,
           rune_gain=1.0 + 0.8 * shiver)
    for k, (peel, fold, land, (ox, oy)) in enumerate(DO_COPIES):
        combo = k == 2
        col = K['cyan3'] if combo else K['violet3']
        light = L['cyan'] if combo else L['violet']
        if peel <= f < fold:                                          # a shadow copy slides off the blade
            u = ease_out((f - peel + 1) / (fold - peel))
            dagger(c, g, gx + ox * u, round(gy + oy * u), DO_ANG + 0.25 * u, DO_S, t * 2, ghost=(col, 0.85, light))
        elif f == fold:                                               # it folds into a card
            mx, my = tip_of(gx + ox, gy + oy, DO_ANG + 0.25, DO_S, 18.0)
            dagger(c, g, gx + ox, round(gy + oy), DO_ANG + 0.25, DO_S * 0.6, t * 2, ghost=(col, 0.4, light))
            hidden_card(c, g, mx, my, 0.5, combo, glow=0.9)
            star4(c, g, mx, my, 3, core=K['white'], col=col, edge=K['violet2'] if not combo else K['cyan2'], light=light)
        elif fold < f < land:                                         # and flies over into the draw pile
            mx, my = tip_of(gx + ox, gy + oy, DO_ANG + 0.25, DO_S, 18.0)
            u = ease_in_out((f - fold) / (land - fold))
            x = lerp(mx, DO_PILE[0] - 2, u)
            y = lerp(my, DO_PILE[1] - 3, u) - math.sin(u * math.pi) * 10
            for back in (1, 2):
                ub = ease_in_out(max(0.0, (f - back * 0.45 - fold) / (land - fold)))
                tx_ = lerp(mx, DO_PILE[0] - 2, ub)
                ty_ = lerp(my, DO_PILE[1] - 3, ub) - math.sin(ub * math.pi) * 10
                m = c.disc(tx_, ty_, 1.3 / back)
                c.put(m, col, 0.7)
                g.add(m, light, 0.5 / back)
            hidden_card(c, g, x, y, 0.45 + 0.55 * abs(math.cos(u * math.pi * 1.5)), combo)
        if f == land:
            star4(c, g, DO_PILE[0] - 2, DO_PILE[1] - 10, 4, core=K['white'], col=col, edge=K['violet2'], light=light)
        sparks(c, g, f, land, DO_PILE[0] - 2, DO_PILE[1] - 6, 4901 + k, 7, (0.5, 1.5), 4,
               CYAN_SPARKS if combo else VIOLET_SPARKS, light, gravity=0.08)
        if f == peel:
            star4(c, g, *tip_of(gx, gy, DO_ANG, DO_S, 30.0), 2, core=K['white'], col=col, edge=K['violet2'], light=light)
    motes(c, g, t, 4903, 7, (4, 6, 84, 50), K['violet3'], K['cyan3'], L['violet'], gain=0.45)
    vignette(c, 0.72)
    return c, g


ILLUSTRATIONS = {
    'agilidad': agilidad_frame,
    'senuelo': senuelo_frame,
    'sombra_esquiva': sombra_esquiva_frame,
    'capa_de_sombras': capa_de_sombras_frame,
    'corte_afortunado': corte_afortunado_frame,
    'hoja_unica': hoja_unica_frame,
    'rafaga_de_cortes': rafaga_de_cortes_frame,
    'tirar_los_dados': tirar_los_dados_frame,
    'torbellino': torbellino_frame,
    'dagas_ocultas': dagas_ocultas_frame,
}


def render(card_id):
    fn = ILLUSTRATIONS[card_id]
    base, glow = [], []
    for f in range(FRAMES):
        b, gl = fn(f)
        base.append(b)
        glow.append(gl)
    return base, glow
