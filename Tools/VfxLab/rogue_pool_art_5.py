"""Animated pixel-art illustrations for a fifth batch of the rogue's loot cards (88x64, 24 frames at 12 fps, seamless).

Same technique and helpers as rogue_pool_art.py .. rogue_pool_art_4.py (docs/art-style.md): every frame has a base
image (opaque pixel art, readable without bloom) and an emissive light map, baked for the game by
export_game_card_art.py. No characters: her daggers, boots, cards and dice, a jester's cap, a fox in the stars.

  paso_atras        Paso Atrás (3 de escudo): in a bamboo grove her boots hop one step back; an enemy's crimson slash
                    cuts the ground where they stood; a small ward; they step forward again.
  rodar             Rodar (2 de escudo, roba 1, descarta una carta al azar): she rolls (a ball of her cloak, gold
                    stripe turning) under an arrow; on landing a card tumbles out (discarded) and another rises.
  rebuscar          Rebuscar (roba un poder, un ataque y una habilidad): the dagger digs in a chest in the cellar and
                    flicks out three cards, red (attack), blue (skill) and violet (power).
  vaciar_bolsillos  Vaciar Bolsillos (descarta tu mano y roba esa misma cantidad): her hand of four cards drops into
                    the discard with the coins from her satchel; four new cards rise from the draw pile into the fan.
  abanico           Abanico de Cuchillas (5 de daño a todos; Singular: +5 de escudo, roba 1): the dagger spreads into a
                    fan of seven blades that fly at the three sigils; with Singular, a violet ward and a card.
  astucia           Astucia (roba 2): a fox drawn in stars over the forest; it winks and two stars fall as cards.
  danza_de_sombras  Danza de Sombras (poder: tus Combos se activan sin jugar otra carta antes): her dagger and two
                    shadow daggers dance round under the Combo sign, which lights up for good.
  estilo_propio     Estilo Propio (12 de escudo; Singular: roba 2): her jester's cap on a stage takes a bolt behind a
                    big ward; with Singular it rings violet and two cards spring from its bells.
  estocada_oportuna Estocada Oportuna (3 de daño; Combo: roba 1): iron plates turn round an enemy; the dagger waits for
                    the gap and thrusts through it; with Combo a card rises.
  fortuna_audaz     Fortuna Audaz (poder: al inicio de cada turno, 50 % roba 1, 50 % gana 4 de escudo): a coin of
                    fortune is tossed each turn; it lands on the card (a card rises), then on the shield (a ward).
  maestra_de_dagas  Maestra de Dagas (poder: cada Daga Oculta que robes mete otra en tu pila de robo): a mandala of
                    eight daggers; a drawn Daga Oculta goes through its core and comes out as two.
  preparacion       Preparación (la siguiente carta cuesta 1 menos): on the war table the dagger taps the next card's
                    mana gem and one of its two mana chips off.
"""
import math

import numpy as np

from pixelkit import Canvas, hexc, dilate, erode, shift
from rogue_art import (AW, AH, FRAMES, K, L, ease_out, ease_in_out, gradient_bg, vignette, star4, motes, outline_mask,
                       crescent)
from mage_art import twinkling_stars, sparks, ellipse_ring
from warrior_art import dust_puffs, CYAN_SPARKS
from rogue_pool_art import (KR, LR, KB, LB, VIOLET_SPARKS, GOLD_SPARKS, dir_of, dagger, tip_of, speed_lines, converge,
                            clipped, mana_drop, sigil, coin)
from rogue_pool_art_2 import paint_card, arrow
from rogue_pool_art_3 import K3, CRIM_SPARKS, ward_rings, card_rot, rising_card, RPAL
from rogue_pool_art_4 import K4, GOLD, BLUE_SPARKS, boots, draw_pile, lerp, comet

K5 = dict(K4)
K5.update({
    # the game's Combo colour (CardFx: 60, 230, 170) as a ramp
    'combo0': hexc('#0b2e24'), 'combo1': hexc('#16664d'), 'combo2': hexc('#3ce6aa'), 'combo3': hexc('#c8ffe8'),
    'bamboo0': hexc('#08161a'), 'bamboo1': hexc('#10282a'), 'bamboo2': hexc('#1b3f3a'), 'bamboo3': hexc('#2f5f50'),
    'chest0': hexc('#1a0e10'), 'chest1': hexc('#33201c'), 'chest2': hexc('#4e3226'),
})
LC = np.array([0.25, 1.0, 0.65, 1])
COMBO_SPARKS = (K5['combo3'], K5['combo2'], K5['combo1'])
TYPE_COL = {'attack': (K3['crim2'], K3['crim4']), 'skill': (KB['b1'], KB['b2']), 'power': (K['violet1'], K['violet2'])}


# ---------------------------------------------------------------------------------------- shared bits
def enemy_sigil(c, g, x, y, t, hit=-99, r=6.0, spin=1.5, a=0.9):
    """An enemy's crimson sigil with its eye; hit = frames since it was struck (flash, shake, crack)."""
    hurt = 0 <= hit <= 4
    jx = (1 if hit % 2 == 0 else -1) if 0 <= hit <= 2 else 0
    col = K['white'] if hit == 0 else K3['crim4'] if hurt else K3['crim3']
    sigil(c, g, x + jx, y, r, r, t * spin, 1.0 if hurt else a, col=col, light=LR['crimson'])
    eye = c.disc(x + jx, y, r * 0.27)
    c.put(eye, K3['crim5'] if not 0 <= hit <= 6 else K3['crim1'])
    g.add(eye, LR['crimson'], 0.8)
    if 1 <= hit <= 8:
        cr = c.line(x - r + 1, y - r * 0.6, x + r * 0.6, y + r * 0.8) | c.line(x + r * 0.7, y - r * 0.8, x - r * 0.5, y + r * 0.7)
        c.put(cr, K['white'] if hit <= 2 else K3['crim5'], 1 - hit / 10)
        g.add(cr, LR['crimson'], 0.8 * (1 - hit / 10))


def type_glyph(c, x, y, kind):
    """The small picture of a card's type: a sword (attack), a shield (skill), a star (power)."""
    x, y = int(x), int(y)
    if kind == 'attack':
        return c.line(x - 2, y + 2, x + 2, y - 2) | c.pixel(x - 1.5, y + 0.5) | c.pixel(x + 0.5, y + 2.5)
    if kind == 'skill':
        return c.rect(x - 2, y - 2, x + 2, y) | c.rect(x - 1, y + 1, x + 1, y + 1) | c.pixel(x + 0.5, y + 2.5)
    return c.rect(x, y - 2, x, y + 2) | c.rect(x - 2, y, x + 2, y)


def type_card(c, g, x, y, kind, wscale=1.0, alpha=1.0, glow=0.0, size=1.0):
    """A card of a given type: gold border, face in its type's colour and its glyph."""
    hw, hh = max(0.6, 4.5 * size * abs(wscale)), 6.5 * size
    dx, dy = np.abs(c.x - x), np.abs(c.y - y)
    m = (dx <= hw) & (dy <= hh) & ~((dx > hw - 1) & (dy > hh - 1))
    inner = erode(m)
    dark, mid = TYPE_COL[kind]
    c.put(outline_mask(m), K['ink'], alpha)
    c.put(m, KR['gold2'], alpha)
    c.put(m & (c.y < y - hh + 1.5) & (c.x < x), KR['gold3'], alpha)
    c.put(inner, dark, alpha)
    c.put(inner & (c.y < y), mid, alpha)
    if hw > 2:
        gl = type_glyph(c, x, y, kind) & inner
        c.put(gl, K['white'], alpha)
        g.add(gl, L['white'], 0.4 * alpha)
    if glow > 0:
        g.add(outline_mask(m), GOLD, glow * alpha)
    return m


def kind_card_rot(c, g, x, y, ang, kind, size=1.0, alpha=1.0):
    """A card of a given type turned by `ang` (tumbling into the discard, lying on the heap)."""
    ux, uy = math.cos(ang), math.sin(ang)
    dx, dy = c.x - x, c.y - y
    lx = dx * ux + dy * uy
    ly = -dx * uy + dy * ux
    w, h = 4.5 * size, 6.5 * size
    m = (np.abs(lx) <= w) & (np.abs(ly) <= h) & ~((np.abs(lx) > w - 1) & (np.abs(ly) > h - 1))
    inner = erode(m)
    dark, mid = TYPE_COL[kind]
    c.put(outline_mask(m), K['ink'], alpha)
    c.put(m, KR['gold2'], alpha)
    c.put(inner, dark, alpha)
    c.put(inner & (ly < 0), mid, alpha)
    gl = inner & (np.abs(lx) < 1.6) & (np.abs(ly) < 1.6) & ((np.abs(lx) < 0.6) | (np.abs(ly) < 0.6))
    c.put(gl, K['white'], alpha)
    return m


def power_ring(c, g, x, y, rx, ry, t, a, col=None, light=None, ticks=14):
    """The rune circle a power leaves under what it changes."""
    if a <= 0:
        return
    col = K['violet2'] if col is None else col
    light = L['violet'] if light is None else light
    ring = ellipse_ring(c, x, y, rx, ry, 0.6)
    ang = np.arctan2((c.y - y) / ry, (c.x - x) / rx)
    ring &= ((ang / (2 * math.pi) * ticks + t * 2) % 1) < 0.65
    c.put(ring, col, a)
    g.add(ring, light, 0.7 * a)
    inner = ellipse_ring(c, x, y, rx * 0.7, ry * 0.7, 0.45) & (((ang / (2 * math.pi) * ticks * 0.5 - t * 2) % 1) < 0.4)
    c.put(inner, col, a * 0.8)
    g.add(inner, light, 0.4 * a)


def falling_card(c, g, f, born, x0, y0, x1, y1, life=6, kind='skill', spin=1.2):
    """A card thrown away: tumbles from (x0, y0) down to (x1, y1) and is gone in a puff."""
    a = f - born
    if not 0 <= a <= life:
        return
    u = a / life
    x = lerp(x0, x1, u)
    y = lerp(y0, y1, u * u) - math.sin(u * math.pi) * 4
    if a == life:                                                     # it lands and is gone in a puff
        dust_puffs(c, x1, y1 + 2, 1, 0, 2, 0.8, rise=0.3, life=3, seed=int(x1 * 7 + born), big=0.6)
        return
    kind_card_rot(c, g, x, y, a * spin * 0.5, kind, size=0.9)


# ---------------------------------------------------------------------------------------- Paso Atrás
PA_HOME = 52.0
PA_BACK = 33.0
PA_GROUND = 54.0
PA_HOP = (4, 7)               # crouch on 4, airborne 5-6, lands on 7
PA_SLASH = 7                  # the enemy's slash passes where the boots stood (just after they left)
PA_RETURN = (16, 19)


def bamboo_grove(c, g, t):
    gradient_bg(c, K['teal0'], K['teal2'], K['teal1'], 0.6)
    twinkling_stars(c, g, t, 5001, 8, (20, 2, 70, 14))
    c.put(c.disc(62, 10, 5), K['moon1'])
    c.put(c.disc(61, 9, 3), K['moon2'])
    g.add(c.disc(62, 10, 5), L['blue'], 0.3)
    rng = np.random.default_rng(5003)
    for layer, (n, tones, w) in enumerate(((9, (K5['bamboo0'], K5['bamboo1']), 2), (5, (K5['bamboo1'], K5['bamboo3']), 3))):
        for i in range(n):
            x = int(rng.uniform(0, AW))
            sway = round(math.sin((t * 2 + i * 0.3 + layer) * math.pi * 2) * (0.5 + layer * 0.5))
            stalk = np.zeros((AH, AW), bool)
            for y in range(0, 56):
                off = round(sway * (56 - y) / 56)
                stalk[y, max(0, x + off):min(AW, x + off + w)] = True
            c.put(stalk, tones[0])
            if layer:
                c.put(stalk & ~shift(stalk, 1, 0), tones[1])
            for ny in range(int(rng.uniform(2, 10)), 56, 11):
                c.put(stalk & (np.abs(c.y - ny) < 0.6), K5['bamboo3'] if layer else K5['bamboo2'])
            if layer:                                                  # a few leaves
                for ly in (12, 30):
                    lx = x + w + round(sway * (56 - ly) / 56)
                    c.put(c.line(lx, ly, lx + 4, ly - 2) | c.line(lx, ly + 1, lx + 3, ly + 1), K5['bamboo2'])
    path = c.rect(0, 54, AW - 1, AH - 1)
    c.put(path, K4['heath1'])
    c.put(c.rect(0, 54, AW - 1, 54), K4['heath2'])
    for x in range(-3, AW, 10):
        c.put(c.rect(x, 57, x + 6, 58), K4['heath0'])


def paso_atras_frame(f):
    t = f / FRAMES
    c, g = Canvas(AW, AH), Canvas(AW, AH)
    bamboo_grove(c, g, t)
    x, h, sy, lean, stride, rune = PA_HOME, 0.0, 1.0, math.sin(t * math.pi * 4) * 0.06, 0.0, 0.0
    up, down = PA_HOP
    if f == up:
        sy = 0.8
    elif up < f < down:                                           # the hop back
        u = (f - up) / (down - up)
        x, h, lean, stride, rune = lerp(PA_HOME, PA_BACK, u), 6 * math.sin(math.pi * u), 0.2, -1.5, 1.0
    elif down <= f < PA_RETURN[0]:
        x, sy = PA_BACK, 0.85 if f == down else 1.0
    elif PA_RETURN[0] <= f < PA_RETURN[1]:                          # and a step forward again
        u = (f - PA_RETURN[0] + 1) / (PA_RETURN[1] - PA_RETURN[0] + 1)
        x, h, lean = lerp(PA_BACK, PA_HOME, ease_in_out(u)), 3 * math.sin(math.pi * u), -0.12
    elif f == PA_RETURN[1]:
        sy = 0.9
    if up < f < down:
        for back, al in ((1, 0.5),):
            u = (f - back - up) / (down - up)
            if u > 0:
                boots(c, g, round(lerp(PA_HOME, PA_BACK, u)), round(PA_GROUND - 6 * math.sin(math.pi * u)), lean=0.2,
                      ghost=(K['violet3'], al))
    # the enemy's slash: a glint in the top right, then a crimson crescent through where the boots stood
    if f == PA_SLASH - 2:
        star4(c, g, 80, 10, 3, core=K['white'], col=K3['crim5'], edge=K3['crim3'], light=LR['crimson'])
    if PA_SLASH - 1 <= f <= PA_SLASH + 1:
        k = f - PA_SLASH + 1
        heads = (math.radians(214), math.radians(242), math.radians(284))
        crescent(c, g, 72, 12, 36, heads[k], 1.2 if k < 2 else 0.9, 1, 4.5 if k < 2 else 2.5, RPAL, LR['crimson'],
                 a=1.0 if k < 2 else 0.55, gain=1.3)
    if PA_SLASH + 1 <= f <= PA_SLASH + 4:                             # it bites the ground
        a = f - PA_SLASH - 1
        gash = c.line(PA_HOME - 8, PA_GROUND + 1, PA_HOME + 6, PA_GROUND - 0.5)
        c.put(gash, K3['crim5'] if a < 2 else K3['crim2'])
        g.add(gash, LR['crimson'], 0.8 * (1 - a / 4))
    sparks(c, g, f, PA_SLASH + 1, PA_HOME - 2, PA_GROUND, 5005, 9, (0.8, 2.0), 5, CRIM_SPARKS, LR['crimson'],
           gravity=0.2, spread=(0.3, math.pi - 0.3))
    dust_puffs(c, PA_HOME, PA_GROUND + 1, f, PA_SLASH + 1, 3, 1.3, rise=0.4, life=5, seed=5007)
    dust_puffs(c, PA_BACK, PA_GROUND + 1, f, down, 2, 1.0, rise=0.3, life=4, seed=5009, big=0.8)
    boots(c, g, round(x), round(PA_GROUND - h), sy, lean, stride, rune)
    if down <= f <= down + 6:                                         # 3 de escudo
        ward_rings(c, g, PA_BACK - 2, PA_GROUND - 9, f - down, size=8, squash=1.0, life=6)
    motes(c, g, t, 5011, 6, (4, 6, 84, 50), K['cyan3'], KB['b2'], L['cyan'], gain=0.4)
    vignette(c, 0.72)
    return c, g


# ---------------------------------------------------------------------------------------- Rodar
RO_HOME = 62.0
RO_LAND = 26.0
RO_GROUND = 54.0
RO_ROLL = (4, 9)              # crouch on 4, rolling 5-8, lands on 9
RO_BACK = (17, 21)            # rolls home again


def torch_corridor(c, g, t):
    gradient_bg(c, K['night0'], K['vio1'], K['night1'], 0.6)
    wall = c.rect(0, 0, AW - 1, 52)
    c.put(wall, K3['temple1'])
    for x0 in (4, 34, 64):                                            # arches into the dark
        arch = c.rect(x0, 18, x0 + 18, 52) | (c.disc(x0 + 9, 18, 9) & (c.y < 18))
        c.put(arch, K['ink'])
        c.put(dilate(arch) & ~arch, K3['temple2'])
    for tx in (30, 62):                                               # two wall torches
        flick = 0.5 + 0.5 * math.sin((t * 5 + tx * 0.1) * math.pi * 2)
        c.put(c.rect(tx, 22, tx + 1, 27), KR['wood1'])
        fl = c.poly([(tx - 1, 22), (tx + 0.5, 16 - flick), (tx + 2, 22)])
        c.put(fl, K['window2'])
        c.put(fl & (c.y > 18), K['window'])
        g.add(fl, L['warm'], 0.8 + 0.3 * flick)
        for r, tone in ((10, K3['temple2']), (6, hexc('#3a2c3c'))):    # its warm pool on the wall, in steps
            c.put(c.disc(tx + 0.5, 20, r) & wall & ~fl & ~c.rect(tx, 22, tx + 1, 27), tone)
    floor = c.rect(0, 53, AW - 1, AH - 1)
    c.put(floor, K3['temple0'])
    c.put(c.rect(0, 53, AW - 1, 53), K3['temple3'])
    for x in range(-6, AW, 13):
        c.put(c.line(x, 54, x - 5, 63), K['ink'])


def cloak_ball(c, g, x, y, r, phase):
    """Her cloak rolled into a ball as she tumbles: violet cloth, a gold stripe turning round it, a speed ring."""
    ball = c.disc(x, y, r)
    c.put(outline_mask(ball), K['ink'])
    c.put(ball, K3['cloth1'])
    c.put(ball & c.disc(x - r * 0.3, y - r * 0.3, r * 0.6), K3['cloth2'])
    ang = np.arctan2(c.y - y, c.x - x)
    stripe = ball & (np.abs(((ang - phase) % (2 * math.pi)) - math.pi) < 0.35)
    stripe |= ball & (np.abs(((ang - phase + math.pi * 0.5) % (2 * math.pi)) - math.pi) < 0.18)
    c.put(stripe, KR['gold2'])
    c.put(c.disc(x + math.cos(phase + math.pi) * r * 0.55, y + math.sin(phase + math.pi) * r * 0.55, 1.2) & ball,
          K4['boot2'])                                                 # a boot heel flashing past
    crescent(c, g, x, y, r + 2.5, -phase, 2.2, -1, 2.0, VPAL5, L['violet'], a=0.9)


VPAL5 = (K['violet0'], K['violet2'], K['violet3'], K['violet4'])


def rodar_frame(f):
    t = f / FRAMES
    c, g = Canvas(AW, AH), Canvas(AW, AH)
    torch_corridor(c, g, t)
    x, sy, rolling, lean = RO_HOME, 1.0, None, math.sin(t * math.pi * 4) * 0.06
    a0, a1 = RO_ROLL
    if f == a0:
        sy = 0.78
    elif a0 < f < a1:
        u = (f - a0) / (a1 - a0)
        rolling = (lerp(RO_HOME, RO_LAND, ease_in_out(u)), u * math.pi * 4)
    elif a1 <= f < RO_BACK[0]:
        x, sy = RO_LAND, 0.82 if f == a1 else 1.0
    elif RO_BACK[0] <= f < RO_BACK[1]:
        u = (f - RO_BACK[0] + 1) / (RO_BACK[1] - RO_BACK[0] + 1)
        rolling = (lerp(RO_LAND, RO_HOME, ease_in_out(u)), -u * math.pi * 3)
    elif f == RO_BACK[1]:
        sy = 0.85
    # the arrow flies over the roll
    if 4 <= f <= 9:
        ax = 96 - (f - 3) * 17
        arrow(c, g, ax, 35, math.pi, s=1.1)
        speed_lines(c, g, ax + 19, 35, -1.0, 0.0, 1.0, 5101, count=3, spread=2, length=(5, 8))
    if rolling is not None:
        bx, ph = rolling
        dirn = -1 if f < RO_BACK[0] else 1
        for back, al in ((6, 0.3), (3, 0.55)):
            m = c.disc(bx - dirn * back, RO_GROUND - 7, 6)
            c.put(outline_mask(m) & ~c.disc(bx, RO_GROUND - 7, 7), K['violet2'], al)
        cloak_ball(c, g, bx, RO_GROUND - 7, 6.5, ph)
        dust_puffs(c, bx - dirn * 4, RO_GROUND + 1, 1, 0, 2, 0.9, rise=0.2, life=3, seed=5103 + f, big=0.7)
    else:
        boots(c, g, round(x), RO_GROUND, sy, lean)
    dust_puffs(c, RO_LAND, RO_GROUND + 1, f, a1, 3, 1.1, rise=0.3, life=5, seed=5105)
    # on landing: a card tumbles out of her hand (discarded), a card rises (roba 1), a small ward (2)
    falling_card(c, g, f, a1, RO_LAND + 4, RO_GROUND - 14, RO_LAND + 16, RO_GROUND - 2, life=6, kind='attack')
    rising_card(c, g, f, a1 + 2, RO_LAND - 2, RO_GROUND - 8, RO_GROUND - 32)
    if a1 <= f <= a1 + 6:
        ward_rings(c, g, RO_LAND - 2, RO_GROUND - 9, f - a1, size=7, squash=1.0, life=6)
    motes(c, g, t, 5109, 6, (4, 6, 84, 50), K['violet3'], KR['spark1'], L['violet'], gain=0.4)
    vignette(c, 0.72)
    return c, g


# ---------------------------------------------------------------------------------------- Rebuscar
RB_CHEST = (44.0, 47.0)        # centre of the chest's front
RB_HOME = (18.0, 32.0)
RB_ANG = 0.75
RB_S = 0.6
RB_CARDS = ((8, 'attack', (28.0, 13.0)), (11, 'skill', (44.0, 10.0)), (14, 'power', (60.0, 13.0)))
RB_DIG = (6, 15)


def cellar(c, g, t):
    gradient_bg(c, K['night0'], K['vio1'], K['night1'], 0.6)
    wall = c.rect(0, 0, AW - 1, 52)
    c.put(wall, K['vio1'])
    for row, y in enumerate(range(2, 52, 6)):                         # rough stone wall
        off = 0 if row % 2 else 5
        for x in range(-10 + off, AW, 10):
            c.put(c.rect(x + 1, y + 1, x + 9, y + 5), K['vio2'] if (x + row) % 3 else K['vio1'])
    for bx in (6, 76):                                                # barrels at the sides
        b = (((c.x - bx) / 7) ** 2 + ((c.y - 44) / 9) ** 2) <= 1
        c.put(outline_mask(b), K['ink'])
        c.put(b, KR['wood1'])
        c.put(b & (c.x < bx - 2), KR['wood2'])
        for hy in (38, 50):
            c.put(b & (np.abs(c.y - hy) < 0.7), KR['iron2'])
    c.put(c.rect(0, 53, AW - 1, AH - 1), K['vio0'])
    c.put(c.rect(0, 53, AW - 1, 53), K['vio3'])
    win = c.rect(40, 2, 48, 6)                                        # a grate high in the wall
    c.put(win, K['night3'])
    for x in (42, 44, 46):
        c.put(c.rect(x, 2, x, 6), K['ink'])
    beam = c.poly([(40, 6), (48, 6), (56, 53), (32, 53)])          # the grate's light: the stones it falls on, one tone up
    c.put(beam & wall & (c.px[..., 0] < K['vio2'][0] + 0.01) & (c.px[..., 2] > K['vio1'][2] - 0.01), K['vio3'])


def chest(c, g, t, lid, glow, rattle=0):
    """A wooden chest with iron bands; lid 0 closed .. 1 thrown open (it tips back)."""
    x, y = RB_CHEST
    x += rattle
    body = c.rect(int(x) - 14, int(y) - 6, int(x) + 14, int(y) + 7)
    c.put(outline_mask(body), K['ink'])
    c.put(body, K5['chest1'])
    c.put(body & (c.x < x - 10), K5['chest2'])
    for bx in (x - 9, x + 9):
        c.put(body & (np.abs(c.x - bx) < 1.0), KR['iron2'])
    c.put(c.rect(int(x) - 1, int(y) - 5, int(x) + 1, int(y) - 2), KR['gold2'])            # lock
    mouth = c.rect(int(x) - 13, int(y) - 7, int(x) + 13, int(y) - 6)
    if lid > 0.1:
        c.put(mouth, K['ink'])
        g.add(mouth, GOLD, 0.6 * glow)
        c.put(mouth & (c.B < 0.5 * glow), KR['gold3'])
    h = 6 * (1 - lid) + 1
    top = c.rect(int(x) - 14, int(y - 6 - h - 3 * lid), int(x) + 14, int(y) - 7)
    if lid > 0.5:                                                     # open: the lid stands up behind
        top = c.rect(int(x) - 14, int(y) - 16, int(x) + 14, int(y) - 8)
    c.put(outline_mask(top), K['ink'])
    c.put(top, K5['chest2'] if lid < 0.5 else K5['chest0'])
    for bx in (x - 9, x + 9):
        c.put(top & (np.abs(c.x - bx) < 1.0), KR['iron1'] if lid > 0.5 else KR['iron2'])
    if glow > 0 and lid > 0.5:
        g.add(c.rect(int(x) - 12, int(y) - 16, int(x) + 12, int(y) - 8), GOLD, 0.1 * glow)
    return body


def rebuscar_frame(f):
    t = f / FRAMES
    c, g = Canvas(AW, AH), Canvas(AW, AH)
    cellar(c, g, t)
    lid = {3: 0.4, 4: 1.0}.get(f, 1.0 if 4 < f < 20 else 0.6 if f == 20 else 0.0)
    rattle = (1 if f % 2 else -1) if RB_DIG[0] + 1 <= f <= RB_DIG[1] - 1 else 0
    glow = 1.0 if 5 <= f <= 16 else 0.4 if lid > 0.5 else 0.0
    # the dagger: hovers, dives into the chest and rummages, comes back out
    dig = RB_DIG[0] <= f <= RB_DIG[1]
    if dig:
        k = f - RB_DIG[0]
        gx = RB_CHEST[0] - 10 + (k % 3) * 6 + rattle
        gy = RB_CHEST[1] - 20 + (2 if k % 2 else 0)
        ang = math.pi / 2 + (0.35 if k % 2 else -0.35)
        keep = c.y < RB_CHEST[1] - 7
        clipped(c, g, keep, lambda c2, g2: dagger(c2, g2, gx, gy, ang, RB_S, t * 2, rune_gain=1.6))
    elif f in (4, 5, 16, 17):
        k = {4: 0.5, 5: 0.85, 16: 0.6, 17: 0.3}[f]
        dagger(c, g, round(lerp(RB_HOME[0], RB_CHEST[0] - 8, k)), round(lerp(RB_HOME[1], RB_CHEST[1] - 22, k)),
               lerp(RB_ANG, math.pi / 2, k), RB_S, t * 2)
    else:
        dagger(c, g, RB_HOME[0], round(RB_HOME[1] + math.sin(t * math.pi * 4) * 0.8), RB_ANG, RB_S, t * 2,
               -2 + (f - 20) * 10.0 if 20 <= f <= 23 else None)
    chest(c, g, t, lid, glow, rattle)
    # junk bounces out while she digs: coins and a bottle cork
    rng = np.random.default_rng(5201)
    for i in range(5):
        born = RB_DIG[0] + 1 + i * 2
        a = f - born
        if 0 <= a <= 5:
            vx, vy = rng.uniform(-1.6, 1.6), rng.uniform(-2.6, -1.8)
            px = RB_CHEST[0] + rng.uniform(-8, 8) + vx * a
            py = RB_CHEST[1] - 7 + vy * a + 0.35 * a * a
            coin(c, g, px, py, a * 1.3 + i, gain=0.4)
    # the three cards: one of each type, flicked out of the chest into a row above it
    for born, kind, (tx, ty) in RB_CARDS:
        a = f - born
        if a < 0 or f > 21:
            continue
        if a <= 4:
            u = ease_out(a / 4.0)
            x = lerp(RB_CHEST[0], tx, u)
            y = lerp(RB_CHEST[1] - 8, ty, u) - math.sin(u * math.pi) * 6
            type_card(c, g, x, y, kind, math.cos(a * 1.4) if a < 4 else 1.0, glow=0.6)
            if a == 0:
                star4(c, g, RB_CHEST[0], RB_CHEST[1] - 9, 3, core=K['white'], col=KR['gold3'], edge=KR['gold2'], light=GOLD)
        elif f <= 19:
            lift = 0 if f < 18 else (f - 17) * 3
            type_card(c, g, tx, ty - lift, kind, glow=0.9 if 16 <= f <= 17 else 0.3)
            if f == 17:
                star4(c, g, tx, ty - 8, 2, core=K['white'], col=KR['gold3'], edge=KR['gold2'], light=GOLD)
        elif f == 20:                                                 # into her hand
            star4(c, g, tx, ty - 7, 3, core=K['white'], col=KR['gold3'], edge=KR['gold2'], light=GOLD)
    motes(c, g, t, 5203, 6, (4, 6, 84, 50), KR['gold2'], K['violet3'], GOLD, gain=0.4)
    vignette(c, 0.72)
    return c, g


# ---------------------------------------------------------------------------------------- Vaciar Bolsillos
VB_FAN = (40.0, 34.0)          # centre of her hand of four cards
VB_ANGS = (-0.42, -0.14, 0.14, 0.42)
VB_PILE = (74.0, 50.0)
VB_HEAP = (16.0, 54.0)
VB_DROP = (3, 4, 5, 6)         # each card of the hand drops into the discard
VB_DRAW = (9, 10, 11, 12)      # four new ones rise from the draw pile
VB_SATCHEL = (14.0, 14.0)


def fan_slot(i):
    ang = VB_ANGS[i]
    return VB_FAN[0] + math.sin(ang) * 26, VB_FAN[1] - math.cos(ang) * 8 + 8, ang


def attic_room(c, g, t):
    gradient_bg(c, K['night0'], K['night2'], K['night1'], 0.6)
    c.put(c.rect(0, 0, AW - 1, 50), K['night1'])
    for x in range(0, AW, 8):
        c.put(c.rect(x, 0, x, 50), K['night0'])
    win = c.rect(54, 6, 70, 22)
    c.put(dilate(win) & ~win, KR['wood1'])
    c.put(win, K['night3'])
    c.put(c.disc(64, 11, 3), K['moon1'])
    c.put(c.rect(62, 6, 62, 22) | c.rect(54, 14, 70, 14), KR['wood1'])
    twinkling_stars(c, g, t, 5301, 4, (55, 7, 69, 21))
    table = c.rect(0, 50, AW - 1, AH - 1)
    c.put(table, KR['wood1'])
    c.put(c.rect(0, 50, AW - 1, 50), KR['wood2'])
    for y in (55, 60):
        c.put(c.rect(0, y, AW - 1, y), KR['wood0'])


def satchel(c, g, x, y, tip):
    """Her leather satchel hanging from a peg by its strap; tip 0..1 turns it over, the flap falls open."""
    c.put(c.rect(int(x) - 1, int(y) - 7, int(x) + 1, int(y) - 5), KR['iron2'])
    ox, oy = round(tip * 3), round(-tip * 1)
    strap = c.line(x, y - 5, x - 6 + ox, y + 3 + oy) | c.line(x, y - 5, x + 6 + ox, y + 3 + oy)
    c.put(strap, K4['boot1'])
    bx, by = x + ox, y + 8 + oy
    bag = ((((c.x - bx) / 8.0) ** 2 + ((c.y - by - 1) / 6.0) ** 2) <= 1) & (c.y >= by - 4)
    bag |= c.rect(int(bx) - 8, int(by) - 4, int(bx) + 8, int(by))
    c.put(outline_mask(bag), K['ink'])
    c.put(bag, K4['boot2'])
    c.put(bag & (c.x < bx - 4), K4['boot3'])
    c.put(bag & (c.x > bx + 5), K4['boot1'])
    c.put(bag & (np.abs(c.y - by - 3) < 0.6) & (np.abs(c.x - bx) < 6), K4['boot1'])     # a seam
    if tip < 0.5:
        flap = bag & (c.y < by + 1) & (np.abs(c.x - bx) < 8 - (c.y - by + 4) * 0.4)
        c.put(flap, K4['boot1'])
        c.put(flap & ~shift(flap, 0, -1), K4['boot0'])
        c.put(c.rect(int(bx), int(by), int(bx), int(by) + 1), KR['gold2'])            # buckle
    else:                                                             # open: the mouth is dark, the flap hangs down
        c.put(bag & (c.y < by - 2), K['ink'])
        flap = c.rect(int(bx) + 7, int(by) - 4, int(bx) + 10, int(by) + 4)
        c.put(outline_mask(flap), K['ink'])
        c.put(flap, K4['boot1'])
        c.put(c.pixel(bx + 8.5, by + 3.5), KR['gold2'])


VB_KINDS = ('attack', 'skill', 'power', 'attack')


def vaciar_bolsillos_frame(f):
    t = f / FRAMES
    c, g = Canvas(AW, AH), Canvas(AW, AH)
    attic_room(c, g, t)
    tip = {2: 0.5, 3: 1.0, 4: 1.0, 5: 1.0, 6: 1.0, 7: 0.6, 8: 0.3}.get(f, 0.0)
    satchel(c, g, VB_SATCHEL[0], VB_SATCHEL[1], tip)
    draw_pile(c, g, VB_PILE[0], VB_PILE[1] - 4, 4, glow=0.7 if f in VB_DRAW else 0.0, bump=1 if f in VB_DRAW else 0)
    # the discard heap grows with what fell, then sinks away in embers
    fallen = [i for i, b in enumerate(VB_DROP) if f >= b + 3]
    heap_fade = 1.0 if f < 16 else max(0.0, 1 - (f - 15) / 5.0)
    if heap_fade > 0:
        for i in fallen:
            kind_card_rot(c, g, VB_HEAP[0] + (i - 1.5) * 4, VB_HEAP[1] - 3 - (i % 2), (i - 1.5) * 0.5 + 1.4, VB_KINDS[i],
                          size=0.85, alpha=heap_fade)
    if 16 <= f <= 20:
        converge(c, g, f, VB_HEAP[0], VB_HEAP[1] - 3, 16, 5, 5303, 8, 8, (KR['spark2'], KR['spark1']), L['warm'])
    # coins from the satchel
    rng = np.random.default_rng(5305)
    for i in range(3):
        a = f - (3 + i)
        if 0 <= a <= 5:
            px = VB_SATCHEL[0] + 4 + rng.uniform(-2, 3) + a * 0.6
            py = VB_SATCHEL[1] + 12 + 0.8 * a * a
            if py < VB_HEAP[1] - 2:
                coin(c, g, px, py, a * 1.4 + i, gain=0.4)
    # her hand: four cards; each drops into the discard; four new cards rise from the pile into the same places
    for i in range(4):
        sx, sy, ang = fan_slot(i)
        kind = VB_KINDS[i]
        drop, draw = VB_DROP[i], VB_DRAW[i]
        if f < drop or f >= 22:
            type_card(c, g, sx, sy, kind)
        elif drop <= f < drop + 3:
            a = f - drop + 1
            u = a / 3.0
            kind_card_rot(c, g, lerp(sx, VB_HEAP[0] + (i - 1.5) * 4, u),
                          lerp(sy, VB_HEAP[1] - 3, u * u) - math.sin(u * math.pi) * 3, ang + a * 0.8, kind, size=0.95)
        elif draw <= f < draw + 3:
            a = f - draw + 1
            u = ease_out(a / 3.0)
            card_rot(c, g, lerp(VB_PILE[0] - 6, sx, u), lerp(VB_PILE[1] - 10, sy, u) - math.sin(u * math.pi) * 6, ang * (1 - u),
                     size=1.0, lit=0.8)
            if a == 3:
                star4(c, g, sx, sy - 8, 2, core=K['white'], col=K['violet3'], edge=K['violet2'], light=L['violet'])
        elif f >= draw + 3:
            if f < 19:
                paint_card(c, g, sx, sy, 1.0, glow=0.5 if f < 16 else 0.0)
            else:                                                     # they turn over: her new hand
                turn = (f - 18) / 3.0
                w = abs(math.cos(turn * math.pi))
                if turn <= 0.5:
                    paint_card(c, g, sx, sy, max(0.15, w))
                else:
                    type_card(c, g, sx, sy, kind, wscale=max(0.15, w))
    motes(c, g, t, 5307, 6, (4, 6, 84, 48), K['violet3'], KR['gold2'], L['violet'], gain=0.4)
    vignette(c, 0.72)
    return c, g


# ---------------------------------------------------------------------------------------- Abanico de Cuchillas
AB_GUARD = (44.0, 52.0)
AB_SIGILS = ((18.0, 15.0), (44.0, 10.0), (70.0, 15.0))
AB_FAN = (-150, -125, -102, -90, -78, -55, -30)    # angles (degrees) of the seven blades
AB_SPREAD = (4, 7)
AB_HIT = 9
AB_SINGULAR = 12


def blade_hall(c, g, t):
    gradient_bg(c, K['vio0'], K['vio2'], K['vio1'], 0.6)
    for x in (2, 80):                                                 # columns at the sides
        col = c.rect(x, 0, x + 5, 56)
        c.put(outline_mask(col), K['ink'])
        c.put(col, K3['temple2'])
        c.put(col & (c.x < x + 2), K3['temple3'])
    for x in range(10, 80, 12):                                       # banners
        b = c.poly([(x, 0), (x + 6, 0), (x + 6, 20), (x + 3, 24), (x, 20)])
        c.put(b, K['vio2'])
        c.put(b & (c.x < x + 2), K['vio3'])
    c.put(c.rect(0, 56, AW - 1, AH - 1), K['vio0'])
    c.put(c.rect(0, 56, AW - 1, 56), K['vio3'])


def abanico_frame(f):
    t = f / FRAMES
    c, g = Canvas(AW, AH), Canvas(AW, AH)
    blade_hall(c, g, t)
    hit = f - AB_HIT
    for (sx, sy) in AB_SIGILS:
        enemy_sigil(c, g, sx, sy, t, hit)
    # the fan: ghost blades unfold round the dagger, fly out along their rays, three hit the sigils
    sp0, sp1 = AB_SPREAD
    gx, gy = AB_GUARD[0], AB_GUARD[1] + (math.sin(t * math.pi * 4) * 0.8 if f < sp0 or f > 16 else 0)
    targets = {1: 0, 3: 1, 5: 2}
    for i, deg in enumerate(AB_FAN):
        ang = math.radians(deg)
        if sp0 <= f < sp1:                                            # the blades open like a fan, steel and runes
            k = ease_out((f - sp0 + 1) / (sp1 - sp0))
            a_ = lerp(-math.pi / 2, ang, k)
            if k < 0.6:
                dagger(c, g, gx, gy, a_, 0.55, t * 2, ghost=(K['cyan3'], 0.7, L['cyan']))
            elif i != 3:
                dagger(c, g, gx, gy, a_, 0.55, t * 2, rune_gain=1.4)
        elif sp1 <= f <= AB_HIT + 1:
            a = f - sp1 + 1
            if i in targets:
                tx, ty = AB_SIGILS[targets[i]]
                u = min(1.0, a / (AB_HIT - sp1 + 1))
                sx_, sy_ = tip_of(gx, gy, ang, 0.55, 20)
                hx, hy = lerp(sx_, tx, u), lerp(sy_, ty, u)
                da = math.atan2(ty - sy_, tx - sx_)
                if u < 1:
                    dagger(c, g, hx - math.cos(da) * 22 * 0.5, hy - math.sin(da) * 22 * 0.5, da, 0.5, t * 2, rune_gain=1.5)
                    m = c.line(sx_, sy_, hx - math.cos(da) * 12, hy - math.sin(da) * 12)
                    c.put(m & (((c.x + c.y) % 2) < 1), K['cyan3'])
                    g.add(m, L['cyan'], 0.4)
            else:
                ux, uy = dir_of(ang)
                d = 10 + a * 13
                if d < 70:
                    dagger(c, g, gx + ux * d, gy + uy * d, ang, 0.5, t * 2, ghost=(K['cyan3'], 0.6, L['cyan']))
    if f in (sp1 - 1, sp1):
        star4(c, g, gx, gy - 4, 4 if f == sp1 else 2, gain=1.2)
    if sp1 <= f <= AB_HIT:
        pass
    else:
        dagger(c, g, gx, round(gy), -math.pi / 2, 0.6, t * 2, -2 + (f - 18) * 10.0 if 18 <= f <= 22 else None,
               rune_gain=1.0 + (1.0 if AB_SINGULAR <= f <= 16 else 0.0))
    for (sx, sy) in AB_SIGILS:
        if 0 <= hit <= 1:
            star4(c, g, sx, sy + 1, 5 if hit == 0 else 3, gain=1.3)
        sparks(c, g, f, AB_HIT, sx, sy, 5401 + int(sx), 7, (0.8, 2.0), 5, CYAN_SPARKS, L['cyan'], gravity=0.12)
    # Singular: the blade glows violet, a ward (+5) and a card (roba 1)
    if AB_SINGULAR <= f <= AB_SINGULAR + 6:
        ward_rings(c, g, gx, gy - 12, f - AB_SINGULAR, size=11, squash=1.1, life=6)
        g.add(dilate(c.disc(gx, gy - 12, 3)), L['violet'], 0.4)
    if f == AB_SINGULAR:
        star4(c, g, gx, gy - 12, 4, core=K['white'], col=K['violet4'], edge=K['violet3'], light=L['violet'])
    rising_card(c, g, f, AB_SINGULAR + 2, gx + 16, gy - 4, gy - 28)
    motes(c, g, t, 5403, 7, (4, 6, 84, 54), K['cyan3'], K['violet3'], L['cyan'], gain=0.4)
    vignette(c, 0.72)
    return c, g


# ---------------------------------------------------------------------------------------- Astucia
AS_STARS = {'ear_l': (28.0, 4.0), 'ear_r': (56.0, 4.0), 'brow_l': (35.0, 15.0), 'brow_r': (49.0, 15.0),
            'cheek_l': (24.0, 21.0), 'cheek_r': (60.0, 21.0), 'ruff_l': (31.0, 28.0), 'ruff_r': (53.0, 28.0),
            'eye_l': (36.0, 21.0), 'eye_r': (48.0, 21.0), 'snout': (42.0, 38.0), 'crown': (42.0, 13.0)}
AS_LINES = (('cheek_l', 'ear_l'), ('ear_l', 'brow_l'), ('brow_l', 'crown'), ('crown', 'brow_r'), ('brow_r', 'ear_r'),
            ('ear_r', 'cheek_r'), ('cheek_r', 'ruff_r'), ('ruff_r', 'snout'), ('snout', 'ruff_l'), ('ruff_l', 'cheek_l'),
            ('eye_l', 'ruff_l'), ('eye_r', 'ruff_r'))
AS_DRAW = (2, 8)               # the lines light up one by one
AS_WINK = 9
AS_FALL = ((10, 'eye_l', (32.0, 52.0)), (12, 'eye_r', (54.0, 52.0)))


def fox_night(c, g, t):
    gradient_bg(c, K['night0'], K['night2'], K['night1'], 0.62)
    twinkling_stars(c, g, t, 5501, 20, (2, 2, 86, 40))
    rng = np.random.default_rng(5503)
    trees = np.zeros((AH, AW), bool)
    for i in range(12):                                               # pines along the bottom
        x = i * 8 + rng.uniform(-2, 2)
        h = rng.uniform(12, 20)
        trees |= c.poly([(x - 5, 64), (x, 64 - h), (x + 5, 64)])
    c.put(trees, K['roof'])
    c.put(trees & ~shift(trees, 0, 1), K['night3'])
    c.put(c.rect(0, 60, AW - 1, AH - 1), K['roof'])


def astucia_frame(f):
    t = f / FRAMES
    c, g = Canvas(AW, AH), Canvas(AW, AH)
    fox_night(c, g, t)
    d0, d1 = AS_DRAW
    show = 0.0 if f < d0 else min(1.0, (f - d0 + 1) / (d1 - d0)) if f < d1 else 1.0 if f <= 16 else \
        max(0.0, 1 - (f - 16) / 6.0)
    # the constellation: always there, faint; its lines light up one by one, then fade back
    n = len(AS_LINES)
    for j, (a_, b_) in enumerate(AS_LINES):
        (x0, y0), (x1, y1) = AS_STARS[a_], AS_STARS[b_]
        m = c.line(x0, y0, x1, y1) & (((c.x + c.y) % 2) < 1)
        lit = 1.0 if f >= d1 and f <= 16 else max(0.0, min(1.0, (f - d0 + 1) * n / (d1 - d0) - j)) if d0 <= f < d1 else show
        c.put(m, K['violet3'] if lit > 0.5 else K['violet1'])
        g.add(m, L['violet'], 0.15 + 0.45 * lit)
    for name, (x, y) in AS_STARS.items():
        if any(nm == name and born <= f <= 21 for born, nm, _ in AS_FALL):
            continue
        b = (0.4 + 0.6 * show) * (0.8 + 0.2 * math.sin((t * 2 + x * 0.07) * math.pi * 2))
        if name == 'eye_r' and f in (AS_WINK, AS_WINK + 1):           # the wink
            m = c.rect(int(x) - 1, int(y), int(x) + 1, int(y))
            c.put(m, K['white'])
            g.add(m, L['white'], 0.8)
            continue
        big = name.startswith('eye') or name == 'snout'
        if b > 0.6:
            star4(c, g, x, y, 2 if big else 1, core=K['white'], col=K['violet4'], edge=K['violet3'], light=L['violet'],
                  gain=b)
        else:
            m = c.pixel(x, y)
            c.put(m, K['moon2'] if big else K['moon1'])
            g.add(m, L['violet'], 0.5 * b)
    if f == AS_WINK:
        star4(c, g, AS_STARS['snout'][0], AS_STARS['snout'][1], 4, core=K['white'], col=K['violet4'], edge=K['violet3'],
              light=L['violet'], gain=1.3)
    # two stars fall from it and turn into cards (roba 2)
    for born, name, (tx, ty) in AS_FALL:
        a = f - born
        sx, sy = AS_STARS[name]
        if 0 <= a <= 3:
            u = ease_in_out(a / 3.0)
            x, y = lerp(sx, tx, u), lerp(sy, ty, u)
            px, py = lerp(sx, tx, max(0.0, u - 0.4)), lerp(sy, ty, max(0.0, u - 0.4))
            comet(c, g, x, y, px, py, (K['violet0'], K['violet3'], K['violet4']), L['violet'], r=1.3)
        elif a == 4:
            star4(c, g, tx, ty, 5, core=K['white'], col=K['violet4'], edge=K['violet3'], light=L['violet'], gain=1.3)
            paint_card(c, g, tx, ty - 2, 0.3, glow=0.8)
        rising_card(c, g, f, born + 5, tx, ty - 2, ty - 24, life=5)
    motes(c, g, t, 5505, 6, (4, 6, 84, 50), K['violet3'], K['moon1'], L['violet'], gain=0.4)
    vignette(c, 0.72)
    return c, g


# ---------------------------------------------------------------------------------------- Danza de Sombras
DS_C = (44.0, 47.0)            # guard height of the dancers; the Combo sign hangs above
DS_SIGN = (44.0, 15.0)
DS_RX, DS_RY = 24.0, 5.0
DS_SPLIT = (3, 6)              # the two shadows step out of the dagger
DS_LIGHT = 10                  # the Combo sign lights for good (the power)
DS_MERGE = (16, 20)


def combo_sign(c, g, x, y, k, size=1.0):
    """The Combo sign: two chevrons pointing on, in the game's Combo green; k 0..1 lights it."""
    m = np.zeros((AH, AW), bool)
    for dx in (-3.5, 2.5):
        for w in (0, 1):
            m |= c.line(x + dx * size - 2 * size + w, y - 4 * size, x + dx * size + 2 * size + w, y) | \
                c.line(x + dx * size + 2 * size + w, y, x + dx * size - 2 * size + w, y + 4 * size)
    c.put(dilate(m) & ~m, K['ink'])
    c.put(m, K5['combo1'] if k < 0.3 else K5['combo2'] if k < 0.8 else K5['combo3'])
    g.add(m, LC, 0.25 + 1.1 * k)
    return m


def ballroom(c, g, t):
    gradient_bg(c, K['vio0'], K['vio2'], K['vio1'], 0.55)
    for x0, sgn in ((0, 1), (AW - 12, -1)):                           # curtains
        cur = c.rect(x0, 0, x0 + 11, 56)
        c.put(cur, K['vio2'])
        for fx in range(x0 + 1, x0 + 12, 3):
            c.put(c.rect(fx, 0, fx, 56) & cur, K['vio3'])
    c.put(c.poly([(12, 0), (76, 0), (70, 5), (18, 5)]), K['vio2'])
    for x in range(14, 76, 6):                                        # chandelier sparks of light, far away
        b = 0.5 + 0.5 * math.sin((t * 2 + x * 0.13) * math.pi * 2)
        m = c.pixel(x, 7 + (x // 6) % 2)
        c.put(m, K['moon1'] if b > 0.5 else K['moon0'])
        g.add(m, L['violet'], 0.3 * b)
    floor = c.rect(0, 50, AW - 1, AH - 1)
    c.put(floor, K['vio1'])
    for row, y in enumerate((50, 53, 57, 62)):
        c.put(c.rect(0, y, AW - 1, y), K['vio0'])
        for x in range(-12 + row * 5, AW, 12 + row * 4):
            c.put(c.rect(x, y + 1, x + 5 + row * 2, y + 1 + row), K['vio2'])


def danza_de_sombras_frame(f):
    t = f / FRAMES
    c, g = Canvas(AW, AH), Canvas(AW, AH)
    ballroom(c, g, t)
    lit = 0.0 if f < DS_LIGHT else 1.0 if f <= 19 else max(0.0, 1 - (f - 19) / 4.0)
    power_ring(c, g, DS_C[0], 55, 22, 3.5, t, 0.35 + 0.65 * lit, col=K5['combo1'] if lit < 0.5 else K5['combo2'], light=LC)
    # the dance: her dagger and two shadow daggers turn round under the Combo sign, upright, leaning into the turn
    s0, s1 = DS_SPLIT
    m0, m1 = DS_MERGE
    spread = 0.0 if f < s0 else min(1.0, (f - s0 + 1) / (s1 - s0 + 1)) if f <= s1 else 1.0 if f < m0 else \
        max(0.0, 1 - (f - m0 + 1) / (m1 - m0 + 1))
    base_ph = math.pi / 2 + (0.0 if f < s0 or f > m1 else (f - s0) * 0.62)
    blades = []
    for k in range(3):
        ph = base_ph + k * spread * (2 * math.pi / 3)
        x = DS_C[0] + math.cos(ph) * DS_RX * spread
        y = DS_C[1] + math.sin(ph) * DS_RY * spread
        tilt = -math.pi / 2 - math.sin(ph) * 0.0 + math.cos(ph + math.pi / 2) * 0.45 * spread
        hop = abs(math.sin(ph * 2)) * 2.5 * spread                     # a little step on each half turn
        blades.append((math.sin(ph), k, x, y - hop, tilt, 0.5 + 0.06 * math.sin(ph) * spread))
    def draw(b):
        _, k, x, y, tilt, sc = b
        if k == 0:
            dagger(c, g, round(x), round(y + math.sin(t * math.pi * 4) * 0.8 * (1 - spread)), tilt, sc, t * 2,
                   -2 + (f - 21) * 10.0 if f >= 21 else None, rune_gain=1.0 + spread * 0.8)
        elif spread > 0.05:
            dagger(c, g, round(x), round(y), tilt, sc, t * 2, ghost=(K['violet3'], 0.9, L['violet']))
    for b in sorted(blades, key=lambda b: b[0]):
        draw(b)
    sign_k = max(lit, 0.2 + 0.3 * spread)
    if lit > 0:                                                       # the sign's light falls on the dance floor
        beam = c.poly([(DS_SIGN[0] - 4, DS_SIGN[1] + 5), (DS_SIGN[0] + 4, DS_SIGN[1] + 5), (DS_C[0] + 16, 55),
                       (DS_C[0] - 16, 55)])
        g.add(beam, LC, 0.06 * lit)
    combo_sign(c, g, DS_SIGN[0], DS_SIGN[1], sign_k, size=1.2)
    if f == DS_LIGHT:
        star4(c, g, DS_SIGN[0], DS_SIGN[1], 6, core=K['white'], col=K5['combo3'], edge=K5['combo2'], light=LC, gain=1.4)
        ring = ellipse_ring(c, DS_SIGN[0], DS_SIGN[1], 11, 11, 0.6)
        c.put(ring, K5['combo2'])
        g.add(ring, LC, 0.8)
    sparks(c, g, f, DS_LIGHT, DS_SIGN[0], DS_SIGN[1], 5601, 12, (0.8, 2.0), 6, COMBO_SPARKS, LC, gravity=0.06)
    motes(c, g, t, 5603, 8, (4, 6, 84, 52), K['violet3'], K5['combo2'], L['violet'], gain=0.45)
    vignette(c, 0.72)
    return c, g


# ---------------------------------------------------------------------------------------- Estilo Propio
EP_CAP = (44.0, 40.0)          # middle of the cap's band
EP_HIT = 8                     # the bolt hits its ward
EP_SINGULAR = 12


def tube(c, pts, w0, w1):
    """A soft tapering tube along a polyline (the cap's floppy points)."""
    m = np.zeros((AH, AW), bool)
    n = len(pts) - 1
    for i in range(n):
        (x0, y0), (x1, y1) = pts[i], pts[i + 1]
        for u in np.linspace(0, 1, 6):
            k = (i + u) / n
            m |= c.disc(lerp(x0, x1, u), lerp(y0, y1, u), lerp(w0, w1, k))
    return m


def jester_cap(c, g, t, x, y, jingle=0.0, glow=0.0):
    """Her jester's cap: a band with gold diamonds and three floppy points (crimson, blue, crimson) with gold bells."""
    sw = math.sin(t * math.pi * 4) * 1.0 + jingle
    points = (
        [(x - 6, y - 3), (x - 12, y - 10), (x - 19, y - 9 + sw * 0.5), (x - 22, y - 3 + sw)],
        [(x, y - 4), (x + 1, y - 14), (x + 5, y - 22 + sw * 0.3), (x + 11, y - 23 + sw)],
        [(x + 6, y - 3), (x + 13, y - 11), (x + 20, y - 10 - sw * 0.5), (x + 22, y - 4 - sw)],
    )
    cols = ((K3['crim2'], K3['crim4']), (KB['b1'], KB['b2']), (K3['crim2'], K3['crim4']))
    band = (((c.x - x) / 11.0) ** 2 + ((c.y - y) / 3.6) ** 2) <= 1
    parts = [tube(c, p, 4.2, 1.2) for p in points]
    whole = band | parts[0] | parts[1] | parts[2]
    bells = []
    for p in points:
        bx, by = p[-1]
        bells.append(c.disc(bx, by + 1.5, 2.1))
    c.put(outline_mask(whole | bells[0] | bells[1] | bells[2]), K['ink'])
    for m, (dark, mid), p in zip(parts, cols, points):
        c.put(m, dark)
        lit_ = m & (c.x + c.y < p[0][0] + p[0][1] + (p[-1][0] - p[0][0]) * 0.3)
        c.put(m & ~shift(m, 1, 1), mid)
        c.put(lit_ & (c.B < 0.5), mid)
    c.put(band, K['scarf1'])
    c.put(band & (c.y < y - 1), K['scarf2'])
    for k in range(-2, 3):
        dm = (np.abs(c.x - x - k * 4.2) + np.abs(c.y - y) * 1.4) <= 1.6
        c.put(dm & band, KR['gold2'])
    for b, p in zip(bells, points):
        bx, by = p[-1]
        c.put(b, KR['gold2'])
        c.put(b & (c.x < bx) & (c.y < by + 1.5), KR['gold3'])
        c.put(c.rect(int(bx) - 1, int(by + 2.5), int(bx), int(by + 2.5)) & b, K['ink'])
        g.add(b, GOLD, 0.3 + 0.6 * glow)
    if glow > 0:                                                      # Singular: a violet rim round the whole cap
        rim = outline_mask(dilate(whole))
        c.put(rim, K['violet2'], glow)
        g.add(rim, L['violet'], 0.8 * glow)
    return whole, [p[-1] for p in points]


def theatre(c, g, t):
    gradient_bg(c, K['night0'], K['vio1'], K['night1'], 0.6)
    for x0 in (0, AW - 14):                                           # crimson curtains, gathered
        cur = c.rect(x0, 0, x0 + 13, 54)
        c.put(cur, K3['crim1'])
        for fx in range(x0 + 1, x0 + 14, 3):
            c.put(c.rect(fx, 0, fx, 54) & cur, K3['crim2'])
        tie = c.rect(x0, 30, x0 + 13, 31)
        c.put(tie, KR['gold1'])
    c.put(c.rect(0, 0, AW - 1, 4), K3['crim1'])
    for x in range(0, AW, 6):
        c.put(c.poly([(x, 4), (x + 6, 4), (x + 3, 8)]), K3['crim2'])
    stage = c.rect(0, 52, AW - 1, AH - 1)
    c.put(stage, KR['wood1'])
    c.put(c.rect(0, 52, AW - 1, 52), KR['wood2'])
    for x in range(8, AW, 14):                                        # footlights
        fl = c.rect(x, 53, x + 2, 54)
        c.put(fl, K['window'])
        g.add(fl, L['warm'], 0.6)
    beam = c.poly([(38, 5), (50, 5), (64, 52), (24, 52)])            # the spotlight on the cap, one tone up
    c.put(beam & ~c.rect(0, 0, 13, 63) & ~c.rect(AW - 14, 0, AW - 1, 63), K['vio2'])
    pool = (((c.x - 44) / 21) ** 2 + ((c.y - 54) / 3) ** 2) <= 1
    c.put(pool, KR['wood2'])


def estilo_propio_frame(f):
    t = f / FRAMES
    c, g = Canvas(AW, AH), Canvas(AW, AH)
    theatre(c, g, t)
    a = f - EP_HIT
    jingle = {0: 2.5, 1: -2.0, 2: 1.4, 3: -0.8}.get(a, 0.0)
    glow = 0.0 if f < EP_SINGULAR else 1.0 if f <= 18 else max(0.0, 1 - (f - 18) / 3.0)
    x, y = EP_CAP[0] + (1 if a == 0 else 0), EP_CAP[1] + math.sin(t * math.pi * 4) * 0.8
    _, bells = jester_cap(c, g, t, x, round(y), jingle, glow)
    # a crimson bolt from the wings: the big ward (12 de escudo) takes it in front of the cap
    hit_pt = (EP_CAP[0] + 15, EP_CAP[1] - 10)
    if 4 <= f < EP_HIT:
        k = (f - 3) / (EP_HIT - 3)
        cx, cy = lerp(92, hit_pt[0], k), lerp(18, hit_pt[1], k)
        px, py = lerp(92, hit_pt[0], max(0.0, k - 0.3)), lerp(18, hit_pt[1], max(0.0, k - 0.3))
        comet(c, g, cx, cy, px, py, (K3['crim1'], K3['crim4'], K3['crim5']), LR['crimson'], r=1.7)
    if EP_HIT - 1 <= f <= EP_HIT + 7:
        ward_rings(c, g, EP_CAP[0], EP_CAP[1] - 12, f - EP_HIT + 1, size=22, squash=0.95, life=8)
    if 0 <= a <= 1:
        star4(c, g, hit_pt[0], hit_pt[1], 5 if a == 0 else 3, core=K['white'], col=KB['b3'], edge=KB['b2'], light=LB, gain=1.3)
    sparks(c, g, f, EP_HIT, hit_pt[0], hit_pt[1], 5701, 10, (0.8, 2.0), 5, BLUE_SPARKS, LB, gravity=0.1)
    # Singular: the bells ring violet and two cards spring out of them (roba 2)
    if f == EP_SINGULAR:
        for bx, by in bells:
            star4(c, g, bx, by + 1, 3, core=K['white'], col=K['violet4'], edge=K['violet3'], light=L['violet'])
    rising_card(c, g, f, EP_SINGULAR + 1, bells[0][0] + 2, bells[0][1], bells[0][1] - 20, life=6)
    rising_card(c, g, f, EP_SINGULAR + 2, bells[2][0] - 2, bells[2][1], bells[2][1] - 20, life=6)
    motes(c, g, t, 5703, 7, (14, 6, 74, 50), K['violet3'], KR['gold2'], L['violet'], gain=0.4)
    vignette(c, 0.72)
    return c, g


# ---------------------------------------------------------------------------------------- Estocada Oportuna
EO_SIGIL = (64.0, 30.0)
EO_R = 13.0                    # radius of the ring of iron plates round the enemy
EO_HOME = (14.0, 32.0)
EO_THRUST = 9                  # the gap comes round: the thrust goes through
EO_ANG = 0.0


def eo_plate_angle(f):
    """The plates turn steadily; the gap faces the blade (angle pi) exactly on the thrust frame."""
    return math.pi + (f - EO_THRUST) * (2 * math.pi / FRAMES) * 2


def fencing_hall(c, g, t):
    gradient_bg(c, K['night0'], K['teal2'], K['teal1'], 0.6)
    for x in range(0, AW, 11):                                        # tall windows
        win = c.rect(x + 2, 6, x + 7, 34) | (c.disc(x + 4.5, 6, 2.5) & (c.y < 6))
        c.put(win, K['teal3'])
        c.put(win & (c.y > 20), K['teal2'])
        c.put(c.rect(x + 4, 6, x + 5, 34) & win, K['teal1'])
    c.put(c.rect(0, 36, AW - 1, 52), K['teal1'])
    for x in range(0, AW, 9):                                         # racks of foils on the wall
        c.put(c.line(x + 2, 38, x + 6, 50), K['steel0'])
    floor = c.rect(0, 52, AW - 1, AH - 1)
    c.put(floor, K['teal0'])
    c.put(c.rect(0, 52, AW - 1, 52), K['teal3'])
    c.put(c.rect(6, 57, 82, 57), K['teal2'])                          # the fencing strip


def estocada_oportuna_frame(f):
    t = f / FRAMES
    c, g = Canvas(AW, AH), Canvas(AW, AH)
    fencing_hall(c, g, t)
    hit = f - EO_THRUST
    # the enemy behind three turning iron plates (one gap)
    base = eo_plate_angle(f)
    ang = np.arctan2(c.y - EO_SIGIL[1], c.x - EO_SIGIL[0])
    d = np.hypot(c.x - EO_SIGIL[0], c.y - EO_SIGIL[1])
    band = (d >= EO_R - 2.0) & (d <= EO_R + 1.0)
    plates = np.zeros((AH, AW), bool)
    for k in range(3):
        mid = base + math.pi / 3 + k * (2 * math.pi / 4) * 1.0 + math.pi / 4
        diff = np.abs(((ang - mid + math.pi) % (2 * math.pi)) - math.pi)
        plates |= band & (diff < 0.62)
    enemy_sigil(c, g, EO_SIGIL[0], EO_SIGIL[1], t, hit, r=6.5)
    c.put(outline_mask(plates), K['ink'])
    c.put(plates, KR['iron2'])
    c.put(plates & (d > EO_R - 0.2), KR['iron3'])
    c.put(plates & (d < EO_R - 1.2), KR['iron1'])
    gap_dir = base
    gx_, gy_ = EO_SIGIL[0] + math.cos(gap_dir) * (EO_R + 2), EO_SIGIL[1] + math.sin(gap_dir) * (EO_R + 2)
    if 5 <= f <= EO_THRUST:                                           # the gap's edges glint as it comes round
        star4(c, g, gx_, gy_, 2 if f < EO_THRUST - 1 else 3, core=K['white'], col=K['cyan3'], edge=K['cyan2'], light=L['cyan'])
    # the dagger: waits, draws back, thrusts through the gap at the right moment, pulls back
    lunge = {6: -2.0, 7: -4.0, 8: -5.0, 9: 30.0, 10: 30.0, 11: 22.0, 12: 12.0, 13: 5.0, 14: 1.0}.get(f, 0.0)
    bob = math.sin(t * math.pi * 4) * 0.8 if not 6 <= f <= 14 else 0.0
    gx, gy = EO_HOME[0] + lunge, EO_HOME[1] + bob
    if f == EO_THRUST:
        for back, al in ((24, 0.3), (12, 0.55)):
            dagger(c, g, gx - back, gy, EO_ANG, 0.62, t * 2, ghost=(K['cyan3'], al, L['cyan']))
        speed_lines(c, g, gx + 10, gy, 1.0, 0.0, 1.0, 5801, count=5, spread=3, length=(8, 14))
    dagger(c, g, round(gx), round(gy), EO_ANG, 0.62, t * 2, -2 + (f - 18) * 10.0 if 18 <= f <= 22 else None,
           rune_gain=1.6 if 6 <= f <= 11 else 1.0)
    if 0 <= hit <= 2:
        star4(c, g, EO_SIGIL[0] - 2, EO_SIGIL[1], {0: 6, 1: 4, 2: 2}[hit], gain=1.3)
    sparks(c, g, f, EO_THRUST, EO_SIGIL[0] - 2, EO_SIGIL[1], 5803, 9, (0.8, 2.0), 5, CYAN_SPARKS, L['cyan'], gravity=0.12)
    rising_card(c, g, f, EO_THRUST + 3, 34, 42, 18)                    # Combo: roba 1
    if f == EO_THRUST + 3:
        combo_sign(c, g, 34, 50, 1.0, size=0.7)
    motes(c, g, t, 5805, 6, (4, 6, 84, 50), K['cyan3'], K5['combo2'], L['cyan'], gain=0.4)
    vignette(c, 0.72)
    return c, g


# ---------------------------------------------------------------------------------------- Fortuna Audaz
FA_COIN = (44.0, 27.0)
FA_TOSSES = ((2, 8, 'card'), (13, 19, 'shield'))   # (thrown, lands, the face it shows)
FA_GLASS = (75.0, 40.0)


def fortune_coin(c, g, x, y, wscale, face, r=7.5, glow=0.0):
    """Her big coin of fortune: a card on one face (roba 1), a shield on the other (4 de escudo)."""
    w = max(0.8, r * abs(wscale))
    m = (((c.x - x) / w) ** 2 + ((c.y - y) / r) ** 2) <= 1
    c.put(outline_mask(m), K['ink'])
    c.put(m, KR['gold2'])
    c.put(m & (c.x < x - w * 0.3) & (c.y < y), KR['gold3'])
    c.put(m & (c.x > x + w * 0.45), KR['gold1'])
    rim = m & ~erode(m)
    c.put(rim, KR['gold1'])
    if abs(wscale) > 0.6:
        if face == 'card':
            gl = c.rect(int(x) - 2, int(y) - 3, int(x) + 2, int(y) + 3) & ~c.rect(int(x) - 1, int(y) - 2, int(x) + 1, int(y) + 2)
            c.put(gl, KR['gold0'])
            c.put(c.pixel(x, y), K['cyan3'])
            g.add(c.pixel(x, y), L['cyan'], 0.6)
        else:
            gl = c.poly([(x - 3, y - 3), (x + 3, y - 3), (x + 3, y), (x, y + 4), (x - 3, y)])
            c.put(gl, KR['gold0'])
            c.put(gl & (c.x < x), KR['gold1'])
    g.add(m, GOLD, 0.15 + 0.6 * glow)
    return m


def fortune_temple(c, g, t):
    gradient_bg(c, K['night0'], K['night2'], K['night1'], 0.6)
    twinkling_stars(c, g, t, 5901, 16, (2, 2, 86, 30))
    for x in (8, 72):                                                 # two columns of a little temple
        col = c.rect(x, 12, x + 7, 52)
        c.put(outline_mask(col), K['ink'])
        c.put(col, K3['temple2'])
        c.put(col & (c.x < x + 2), K3['temple3'])
        for fy in range(14, 52, 4):
            c.put(c.rect(x + 3, fy, x + 3, fy + 2), K3['temple1'])
    roof = c.poly([(2, 12), (44, 1), (86, 12)])
    c.put(outline_mask(roof), K['ink'])
    c.put(roof, K3['temple1'])
    c.put(roof & ~shift(roof, 0, 1), K3['temple3'])
    c.put(c.rect(0, 52, AW - 1, AH - 1), K3['temple0'])
    c.put(c.rect(0, 52, AW - 1, 52), K3['temple3'])
    altar = c.rect(34, 46, 54, 53)
    c.put(outline_mask(altar), K['ink'])
    c.put(altar, K3['temple2'])
    c.put(altar & (c.y < 47), K3['temple3'])


def fortuna_audaz_frame(f):
    t = f / FRAMES
    c, g = Canvas(AW, AH), Canvas(AW, AH)
    fortune_temple(c, g, t)
    power_ring(c, g, 44, 55, 18, 3, t, 0.8, col=KR['gold2'], light=GOLD)
    # the coin: each turn it is tossed, spins, and lands showing a card or a shield
    face, wscale, y, glow = 'card', 1.0, FA_COIN[1] + math.sin(t * math.pi * 4) * 0.8, 0.0
    for up, down, fc in FA_TOSSES:
        if up <= f < down:
            u = (f - up) / (down - up)
            y = FA_COIN[1] - math.sin(u * math.pi) * 14
            wscale = math.cos(u * math.pi * 5)
            face = 'card' if wscale > 0 else 'shield'
        elif down <= f:
            face = fc
            if f - down <= 2:
                glow = 1.0 - (f - down) / 3.0
    if f >= FA_TOSSES[1][1] or f < FA_TOSSES[0][0]:
        face = 'shield' if f >= FA_TOSSES[1][1] else 'card'
    if FA_TOSSES[0][1] <= f < FA_TOSSES[1][0]:
        face = 'card'
    fortune_coin(c, g, FA_COIN[0], round(y), wscale, face, glow=glow)
    for up, down, fc in FA_TOSSES:
        if f == up:
            star4(c, g, FA_COIN[0], FA_COIN[1] + 8, 3, core=K['white'], col=KR['gold3'], edge=KR['gold2'], light=GOLD)
        if f == down:
            star4(c, g, FA_COIN[0] + 7, FA_COIN[1] - 6, 4, core=K['white'], col=KR['gold3'], edge=KR['gold2'], light=GOLD)
        sparks(c, g, f, down, FA_COIN[0], FA_COIN[1], 5903 + up, 8, (0.6, 1.6), 5, GOLD_SPARKS, GOLD, gravity=0.06)
    # the card face: a card rises (roba 1); the shield face: a ward (4 de escudo)
    rising_card(c, g, f, FA_TOSSES[0][1] + 1, FA_COIN[0] + 16, FA_COIN[1] + 4, FA_COIN[1] - 16)
    if FA_TOSSES[1][1] <= f <= FA_TOSSES[1][1] + 5:
        ward_rings(c, g, FA_COIN[0], FA_COIN[1], f - FA_TOSSES[1][1], size=11, squash=1.0, life=5)
    motes(c, g, t, 5905, 7, (4, 6, 84, 50), KR['gold2'], KB['b2'], GOLD, gain=0.4)
    vignette(c, 0.72)
    return c, g


# ---------------------------------------------------------------------------------------- Maestra de Dagas
MD_C = (48.0, 28.0)            # the mandala of daggers
MD_PILE = (14.0, 52.0)
MD_DRAW = 3                    # a Daga Oculta is drawn
MD_CORE = 8                    # it reaches the mandala and becomes two
MD_HAND = (78.0, 8.0)


def arcane_chamber(c, g, t):
    gradient_bg(c, K['vio0'], K['vio2'], K['vio1'], 0.6)
    for x in range(0, AW, 10):
        c.put(c.rect(x, 0, x, 52), K['vio0'])
    for i, (x, y) in enumerate(((10, 14), (80, 30), (70, 46))):       # floating rune stones
        b = 0.5 + 0.5 * math.sin((t * 2 + i * 0.3) * math.pi * 2)
        st = c.rect(x, y - round(b), x + 3, y + 3 - round(b))
        c.put(st, K3['temple2'])
        c.put(c.pixel(x + 1.5, y + 1.5 - round(b)), K['violet3'])
        g.add(c.pixel(x + 1.5, y + 1.5 - round(b)), L['violet'], 0.5)
    c.put(c.rect(0, 53, AW - 1, AH - 1), K['vio0'])
    c.put(c.rect(0, 53, AW - 1, 53), K['vio3'])


def maestra_de_dagas_frame(f):
    t = f / FRAMES
    c, g = Canvas(AW, AH), Canvas(AW, AH)
    arcane_chamber(c, g, t)
    power_ring(c, g, MD_C[0], 57, 22, 3.5, t, 0.9)
    split = f - MD_CORE
    draw_pile(c, g, MD_PILE[0], MD_PILE[1] - 6, 3, glow=0.8 if split == 5 else 0.0, bump=1 if split == 5 else 0)
    # the mandala: eight daggers, points outwards, turning round a violet core (faster when it acts)
    # (8-fold: a quarter turn per loop plus one eighth when it acts keeps the loop seamless)
    turn = t * math.pi * 2 * 0.25 + (math.pi / 4 * ease_out(min(1.0, max(0.0, split + 1) / 4.0)) if f >= MD_CORE - 1 else 0.0)
    for k in range(8):
        ang = turn + k * math.pi / 4
        ux, uy = dir_of(ang)
        dagger(c, g, MD_C[0] + ux * 5, MD_C[1] + uy * 5, ang, 0.42, t * 2,
               rune_gain=1.0 + (1.0 if 0 <= split <= 2 else 0.0))
    core = c.disc(MD_C[0], MD_C[1], 3.2)
    c.put(outline_mask(core), K['ink'])
    c.put(core, K['violet2'] if not 0 <= split <= 2 else K['violet4'])
    c.put(c.disc(MD_C[0] - 0.8, MD_C[1] - 0.8, 1.4), K['violet4'])
    g.add(core, L['violet'], 0.8 + (1.0 if 0 <= split <= 2 else 0.0))
    # a Daga Oculta is drawn from the pile, flies into the core and comes out as two:
    # one goes on to her hand, the other back into the pile
    a = f - MD_DRAW
    if 0 <= a < MD_CORE - MD_DRAW:
        u = ease_in_out((a + 1) / (MD_CORE - MD_DRAW))
        x = lerp(MD_PILE[0] - 4, MD_C[0], u)
        y = lerp(MD_PILE[1] - 10, MD_C[1], u) - math.sin(u * math.pi) * 10
        paint_card(c, g, x, y, 0.5 + 0.5 * abs(math.cos(u * math.pi)), glow=0.6)
        glyph = c.rect(int(x), int(y) - 3, int(x), int(y) + 2) | c.rect(int(x) - 1, int(y) + 1, int(x) + 1, int(y) + 1)
        c.put(glyph, K['violet3'])
    if split == 0:
        star4(c, g, MD_C[0], MD_C[1], 7, core=K['white'], col=K['violet4'], edge=K['violet3'], light=L['violet'], gain=1.5)
    if 1 <= split <= 5:
        u = ease_out(split / 5.0)
        for (tx, ty), back in ((MD_HAND, False), ((MD_PILE[0] - 4, MD_PILE[1] - 10), True)):
            x, y = lerp(MD_C[0], tx, u), lerp(MD_C[1], ty, u) - (math.sin(u * math.pi) * 8 if back else 0)
            paint_card(c, g, x, y, 0.5 + 0.5 * abs(math.cos(u * math.pi * 1.5)), glow=0.6)
            glyph = c.rect(int(x), int(y) - 3, int(x), int(y) + 2) | c.rect(int(x) - 1, int(y) + 1, int(x) + 1, int(y) + 1)
            c.put(glyph, K['violet3'] if back else K['cyan3'])
            g.add(glyph, L['violet'], 0.6)
    if split == 5:
        star4(c, g, MD_HAND[0], MD_HAND[1], 3, core=K['white'], col=K['cyan3'], edge=K['cyan2'], light=L['cyan'])
    sparks(c, g, f, MD_CORE, MD_C[0], MD_C[1], 6001, 12, (0.8, 2.2), 6, VIOLET_SPARKS, L['violet'], gravity=0.04)
    motes(c, g, t, 6003, 7, (4, 6, 84, 52), K['violet3'], K['cyan3'], L['violet'], gain=0.45)
    vignette(c, 0.72)
    return c, g


# ---------------------------------------------------------------------------------------- Preparación
PR_CARD = (52.0, 30.0)         # the next card, laid on the map
PR_GEM = (45.0, 20.0)          # its mana gem (top left corner)
PR_TAP = 8                     # the dagger's point taps the gem: one of its two mana is gone


def big_card(c, g, x, y, size=1.9):
    """A card lying on the table, seen close: gold border, violet back... face up with an art window."""
    hw, hh = 4.5 * size, 6.5 * size
    dx, dy = np.abs(c.x - x), np.abs(c.y - y)
    m = (dx <= hw) & (dy <= hh) & ~((dx > hw - 1) & (dy > hh - 1))
    inner = erode(erode(m))
    c.put(outline_mask(m), K['ink'])
    c.put(m, KR['gold2'])
    c.put(m & (c.x < x - hw + 2), KR['gold3'])
    c.put(inner, KR['paper3'])
    art = (np.abs(c.x - x) <= hw - 3) & (c.y >= y - hh + 4) & (c.y <= y)
    c.put(art, K['vio2'])
    c.put(art & (c.y > y - 3), K['vio3'])
    for yy in (3, 5, 7):
        c.put((np.abs(c.x - x) <= hw - 4) & (np.abs(c.y - (y + yy)) < 0.5), KR['paper1'])
    return m


def war_table(c, g, t):
    gradient_bg(c, K['night0'], K['night2'], K['night1'], 0.6)
    c.put(c.rect(0, 0, AW - 1, 40), K['night1'])
    win = c.rect(6, 4, 22, 22)
    c.put(win, K['night3'])
    c.put(c.rect(14, 4, 14, 22) | c.rect(6, 13, 22, 13), KR['wood1'])
    c.put(c.disc(18, 8, 2.5), K['moon1'])
    table = c.rect(0, 40, AW - 1, AH - 1)
    c.put(table, KR['wood1'])
    c.put(c.rect(0, 40, AW - 1, 40), KR['wood2'])
    mp = c.poly([(8, 44), (36, 41), (40, 62), (4, 63)])               # a map with routes on it
    c.put(outline_mask(mp), K['ink'])
    c.put(mp, KR['paper2'])
    c.put(mp & (((c.x * 0.7 + c.y) % 7) < 1), KR['paper1'])
    route = c.line(10, 58, 18, 50) | c.line(18, 50, 30, 52) | c.line(30, 52, 34, 45)
    c.put(route & (((c.x + c.y) % 2) < 1), K3['crim3'])
    c.put(c.rect(33, 44, 35, 46), K3['crim4'])
    candle = c.rect(78, 30, 80, 40)
    c.put(candle, K4['bone2'])
    c.put(candle & (c.x < 79), K4['bone3'])
    fl = c.poly([(78.5, 30), (79.5, 25 - math.sin(t * math.pi * 6)), (80.5, 30)])
    c.put(fl, K['window'])
    g.add(fl, L['warm'], 1.0)
    for r, tone in ((9, hexc('#22182a')), (5, hexc('#2e1e2c'))):    # its warm light on the wall, in steps
        c.put(c.disc(79, 27, r) & (c.y < 40) & ~candle & ~fl, tone)


def preparacion_frame(f):
    t = f / FRAMES
    c, g = Canvas(AW, AH), Canvas(AW, AH)
    war_table(c, g, t)
    big_card(c, g, PR_CARD[0], PR_CARD[1])
    # the cost gem: two mana; the tap chips one off, the gem is left smaller; it refills at the end of the loop
    a = f - PR_TAP
    left = 1 if 0 <= a <= 13 else 2
    gem_size = 1.25 if left == 2 else 0.95
    if f == PR_TAP - 1 + 14 + 1:
        gem_size = 1.1
    mana_drop(c, g, PR_GEM[0], PR_GEM[1], gem_size, gain=1.0 + (0.8 if a == 0 else 0.0))
    for k in range(left):                                             # its pips (one per mana)
        m = c.pixel(PR_GEM[0] - 0.5 + (k - (left - 1) / 2) * 2, PR_GEM[1] + 0.5)
        c.put(m, K['white'])
    if 0 <= a <= 5:                                                   # the chipped-off mana floats away and fades
        u = a / 5.0
        sx, sy = PR_GEM[0] - 6 * u - 2, PR_GEM[1] - 2 - 10 * ease_out(u)
        mana_drop(c, g, round(sx), round(sy), 0.7 * (1 - u * 0.5), gain=1 - u)
        minus = c.rect(int(PR_GEM[0]) + 4, int(PR_GEM[1]) - 6, int(PR_GEM[0]) + 6, int(PR_GEM[1]) - 6)
        c.put(minus, KB['b3'], 1 - u * 0.6)
        g.add(minus, LB, 0.8 * (1 - u))
    if a == 0:
        star4(c, g, PR_GEM[0], PR_GEM[1], 5, core=K['white'], col=KR['mana3'], edge=KR['mana2'], light=LR['mana'], gain=1.3)
    sparks(c, g, f, PR_TAP, PR_GEM[0], PR_GEM[1], 6101, 9, (0.6, 1.6), 5, (KR['mana3'], KR['mana2'], KR['mana1']), LR['mana'],
           gravity=0.08)
    if 18 <= f <= 21:                                                 # a new turn: the gem fills again
        converge(c, g, f, PR_GEM[0], PR_GEM[1], 18, 4, 6103, 6, 8, (KR['mana3'], KR['mana2']), LR['mana'])
    # the card glows ready (cheaper) after the tap
    if 1 <= a <= 9:
        m = outline_mask(dilate(c.rect(int(PR_CARD[0] - 8.5), int(PR_CARD[1] - 12.4), int(PR_CARD[0] + 8.5),
                                       int(PR_CARD[1] + 12.4))))
        c.put(m & (((c.x + c.y + f) % 4) < 2), KR['mana2'])
        g.add(m, LR['mana'], 0.5 * (1 - (a - 1) / 9))
    # the dagger pins the map, rises, taps the gem with its point and goes back
    tap = {5: 0.35, 6: 0.75, 7: 0.95, 8: 1.0, 9: 0.8, 10: 0.5, 11: 0.25}.get(f, 0.0)
    rest = (24.0, 30.0, 1.05)                                         # pointing down into the map
    hit = (PR_GEM[0] - 13.0, PR_GEM[1] - 15.0, 0.8)
    gx, gy, ang = (lerp(rest[i], hit[i], ease_in_out(tap)) for i in range(3))
    cut = None if tap > 0.2 else 30.0
    bob = math.sin(t * math.pi * 4) * 0.5 if tap == 0 else 0.0
    dagger(c, g, round(gx), round(gy + bob), ang, 0.58, t * 2, -2 + (f - 16) * 10.0 if 16 <= f <= 20 else None, cut=cut,
           rune_gain=1.5 if 6 <= f <= 9 else 1.0)
    motes(c, g, t, 6105, 6, (4, 6, 84, 40), KR['mana2'], KR['spark1'], LR['mana'], gain=0.35)
    vignette(c, 0.72)
    return c, g


ILLUSTRATIONS = {
    'paso_atras': paso_atras_frame,
    'rodar': rodar_frame,
    'rebuscar': rebuscar_frame,
    'vaciar_bolsillos': vaciar_bolsillos_frame,
    'abanico': abanico_frame,
    'astucia': astucia_frame,
    'danza_de_sombras': danza_de_sombras_frame,
    'estilo_propio': estilo_propio_frame,
    'estocada_oportuna': estocada_oportuna_frame,
    'fortuna_audaz': fortuna_audaz_frame,
    'maestra_de_dagas': maestra_de_dagas_frame,
    'preparacion': preparacion_frame,
}


def render(card_id):
    fn = ILLUSTRATIONS[card_id]
    base, glow = [], []
    for f in range(FRAMES):
        b, gl = fn(f)
        base.append(b)
        glow.append(gl)
    return base, glow
