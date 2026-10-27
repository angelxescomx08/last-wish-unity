"""Pixel-art card base for the VFX Lab rogue cards (104×144): frame, plates, gems, baked text.

Returns the base canvas and its emissive map (gems), plus the hover outline sprite.
"""
import numpy as np

from pixelkit import Canvas, hexc, mix, dilate, erode, shift

W, H = 104, 144
ART_X, ART_Y, ART_W, ART_H = 8, 21, 88, 64      # illustration window (top-left, size)

P = {
    'ink': hexc('#08060d'),
    'leather0': hexc('#1d1529'), 'leather1': hexc('#271c37'), 'leather2': hexc('#33264a'),
    'steel0': hexc('#2b2d3f'), 'steel1': hexc('#4b5069'), 'steel2': hexc('#7d86a3'),
    'steel3': hexc('#b7c0d8'), 'steel4': hexc('#eef2ff'),
    'plate0': hexc('#120d1c'), 'plate1': hexc('#1b1428'), 'plate2': hexc('#2c2142'),
    'cyan0': hexc('#0f3b54'), 'cyan1': hexc('#1784a6'), 'cyan2': hexc('#33c8e8'), 'cyan3': hexc('#9ff4ff'),
    'violet1': hexc('#4a2470'), 'violet2': hexc('#7b3fb3'), 'violet3': hexc('#b98af0'),
    'text': hexc('#f3eeff'), 'textdim': hexc('#b8a9d9'), 'shadow': hexc('#0b0712'),
    'mana0': hexc('#0d2459'), 'mana1': hexc('#1e4fc0'), 'mana2': hexc('#4b93ff'), 'mana3': hexc('#c3e7ff'),
    'red0': hexc('#4a0b16'), 'red1': hexc('#9c1d31'), 'red2': hexc('#e2434f'), 'red3': hexc('#ffb09a'),
    'blue0': hexc('#0f2550'), 'blue1': hexc('#2756a8'), 'blue2': hexc('#4f95e6'), 'blue3': hexc('#c2e4ff'),
    'dmg': hexc('#ff6a5c'), 'blk': hexc('#7cc4ff'),
}


def silhouette(c):
    m = c.rect(0, 0, W - 1, H - 1)
    for (x, y) in [(0, 0), (1, 0), (2, 0), (0, 1), (0, 2), (1, 1)]:
        for (px, py) in [(x, y), (W - 1 - x, y), (x, H - 1 - y), (W - 1 - x, H - 1 - y)]:
            m[py, px] = False
    return m


def bevel_ring(c, m, light, dark, mid=None):
    """1px ring just inside mask m: light on top/left edges, dark on bottom/right."""
    ring = m & ~erode(m)
    top_left = ring & (~shift(m, 0, 1) | ~shift(m, 1, 0))
    c.put(ring, mid if mid is not None else dark)
    c.put(ring & (~shift(m, 0, 1) | ~shift(m, 1, 0)), light)
    c.put(ring & (~shift(m, 0, -1) | ~shift(m, -1, 0)), dark)
    return ring


def gem(c, glow, cx, cy, r, cols, glow_col, glow_amt=0.5):
    """Round faceted gem with posterized radial light from the top-left."""
    body = c.disc(cx, cy, r)
    d = np.hypot(c.x - (cx - r * 0.35), c.y - (cy - r * 0.35)) / (r * 1.5)
    band = np.clip(np.floor((1 - d) * 4 + c.B * 0.9), 0, 3).astype(int)
    for i, col in enumerate(cols):
        c.put(body & (band == i), col)
    c.put(c.disc(cx - r * 0.4, cy - r * 0.45, max(0.6, r * 0.22)), P['steel4'])
    glow.add(body, glow_col, glow_amt * np.clip(1.2 - d, 0.2, 1))
    return body


def stud(c, glow, x, y, col, glow_col):
    c.put(c.rect(x - 1, y - 1, x + 1, y + 1), P['ink'])
    c.put(c.pixel(x + 0.5, y + 0.5), col)
    glow.add(c.pixel(x + 0.5, y + 0.5), glow_col, 0.9)


def card_base(fonts, name, desc_lines, mana, attack, block, label='Pícara'):
    c = Canvas(W, H)
    glow = Canvas(W, H)
    sil = silhouette(c)
    # outline + leather body with a subtle dithered grain
    c.put(sil, P['ink'])
    body = erode(sil)
    c.put(body, P['leather1'])
    rng = np.random.default_rng(42)
    grain = rng.random((H, W))
    c.put(body & (grain < 0.18), P['leather0'])
    c.put(body & (grain > 0.86), P['leather2'])
    # vertical sheen on the leather (dithered)
    sheen = np.clip(1 - np.abs(c.x - W * 0.3) / 30.0, 0, 1) * 0.35
    c.put(body, P['leather2'], sheen)
    # outer steel trim (2px)
    bevel_ring(c, body, P['steel3'], P['steel1'], P['steel2'])
    inner = erode(body)
    bevel_ring(c, inner, P['steel2'], P['steel0'], P['steel1'])
    c.put(erode(inner) & ~erode(erode(inner)), P['ink'])

    # violet inlay running down both sides
    for x in (4, W - 5):
        c.put(c.rect(x, 24, x, 120), P['violet1'])
        c.put(c.rect(x, 24, x, 120) & ((c.y.astype(int) % 6) == 0), P['violet2'])
    # corner brackets with a cyan stud (bottom corners; the top-left holds the mana gem)
    for (cx, cy, sx, sy) in [(W - 1, 0, -1, 1), (0, H - 1, 1, -1), (W - 1, H - 1, -1, -1)]:
        pts = [(cx + sx * 1, cy + sy * 1), (cx + sx * 11, cy + sy * 1), (cx + sx * 11, cy + sy * 3), (cx + sx * 3, cy + sy * 3),
               (cx + sx * 3, cy + sy * 11), (cx + sx * 1, cy + sy * 11)]
        br = c.poly(pts)
        c.put(dilate(br) & sil, P['ink'])
        c.put(br, P['steel2'])
        c.put(br & (shift(br, 0, 1) == False), P['steel3'])
        stud(c, glow, int(cx + sx * 5.5), int(cy + sy * 5.5), P['cyan3'], P['cyan2'])
    # side gems on the leather between frame and art window
    for x in (3, W - 4):
        g_ = c.poly([(x, 49), (x + 2, 52), (x, 55), (x - 2, 52)])
        c.put(dilate(g_), P['ink'])
        c.put(g_, P['violet2'])
        c.put(c.pixel(x + 0.5, 51.5), P['violet3'])
        glow.add(g_, P['violet2'], 0.5)

    # art window recess: ink rim + steel bevel (inset: dark top/left, light bottom/right)
    win_outer = c.rect(ART_X - 3, ART_Y - 3, ART_X + ART_W + 2, ART_Y + ART_H + 2)
    c.put(win_outer, P['ink'])
    win_bevel = c.rect(ART_X - 2, ART_Y - 2, ART_X + ART_W + 1, ART_Y + ART_H + 1)
    bevel_ring(c, win_bevel, P['steel1'], P['steel3'], P['steel2'])
    win_in = c.rect(ART_X - 1, ART_Y - 1, ART_X + ART_W, ART_Y + ART_H)
    c.put(win_in, P['ink'])
    # corner studs on the window (emissive cyan)
    for (x, y) in [(ART_X - 2, ART_Y - 2), (ART_X + ART_W + 1, ART_Y - 2), (ART_X - 2, ART_Y + ART_H + 1), (ART_X + ART_W + 1, ART_Y + ART_H + 1)]:
        stud(c, glow, x, y, P['cyan3'], P['cyan2'])

    # name plate
    plate = c.rect(22, 4, 98, 17)
    c.put(plate, P['ink'])
    pin = c.rect(23, 5, 97, 16)
    c.put(pin, P['plate1'])
    bevel_ring(c, pin, P['steel3'], P['steel1'], P['steel2'])
    c.put(c.rect(25, 7, 95, 7), P['plate2'])
    for x in (25, 94):
        c.put(c.rect(x, 10, x + 1, 11), P['steel1'])
        c.put(c.pixel(x + 0.5, 10.5), P['steel4'])
    nf = fonts['name']
    nw = nf.width(name)
    nf.draw(c, name, 22 + (77 - nw) // 2 + 1, 7, P['text'], shadow=P['shadow'])

    # mana gem (top-left, overlapping the frame)
    c.put(c.disc(11.5, 11.5, 10.2), P['ink'])
    ring = c.disc(11.5, 11.5, 9.4) & ~c.disc(11.5, 11.5, 7.6)
    c.put(ring, P['steel2'])
    c.put(ring & (c.y < 11.5) & (c.x < 14), P['steel3'])
    c.put(ring & (c.y > 13) & (c.x > 9), P['steel1'])
    gem(c, glow, 11.5, 11.5, 7.0, [P['mana0'], P['mana1'], P['mana2'], P['mana3']], hexc('#3f8cff'), 0.55)
    mf = fonts['number']
    t = str(mana)
    mf.draw(c, t, 12 - mf.width(t) // 2, 8, P['text'], outline=P['shadow'])

    # divider gem under the art window
    dg = c.poly([(52, 84), (57, 89), (52, 94), (47, 89)])
    c.put(dilate(dg), P['ink'])
    c.put(dg, P['steel2'])
    gi = c.poly([(52, 86), (55, 89), (52, 92), (49, 89)])
    c.put(gi, P['cyan2'])
    c.put(gi & (c.y < 89) & (c.x < 52.5), P['cyan3'])
    c.put(gi & (c.y > 89.5), P['cyan1'])
    glow.add(gi, P['cyan2'], 0.8)

    # rules text box
    box = c.rect(9, 94, 94, 125)
    c.put(box, P['ink'])
    bin_ = c.rect(10, 95, 93, 124)
    c.put(bin_, P['plate0'])
    c.put(c.rect(10, 124, 93, 124) | c.rect(93, 95, 93, 124), P['plate2'])
    tf = fonts['text']
    total = len(desc_lines) * 10
    y = 95 + (30 - total) // 2 + 1
    for line in desc_lines:
        draw_rich(c, tf, line, 52, y)
        y += 10

    # stat badges
    badge(c, glow, fonts, 13.5, 134.5, attack, 'red')
    badge(c, glow, fonts, 90.5, 134.5, block, 'blue')
    # class label
    lf = fonts['text']
    lw = lf.width(label)
    lf.draw(c, label, 52 - lw // 2, 130, P['textdim'], shadow=P['shadow'])
    for x in (52 - lw // 2 - 5, 52 + (lw + 1) // 2 + 3):
        c.put(c.poly([(x, 132), (x + 2, 134), (x, 136), (x - 2, 134)]), P['violet2'])
        c.put(c.pixel(x + 0.5, 134.5), P['violet3'])
    return c, glow


def draw_rich(c, font, line, cx, y):
    """Centred line where tokens wrapped in [d]..[/d] are damage red and [b]..[/b] block blue."""
    parts, col, buf, i = [], 'text', '', 0
    while i < len(line):
        if line.startswith('[d]', i):
            parts.append((buf, col)); buf, col = '', 'dmg'; i += 3; continue
        if line.startswith('[b]', i):
            parts.append((buf, col)); buf, col = '', 'blk'; i += 3; continue
        if line.startswith('[/d]', i) or line.startswith('[/b]', i):
            parts.append((buf, col)); buf, col = '', 'text'; i += 4; continue
        buf += line[i]; i += 1
    parts.append((buf, col))
    width = sum(font.width(t) for t, _ in parts)
    x = cx - width // 2
    for t, colname in parts:
        if t:
            font.draw(c, t, x, y, P[colname], shadow=P['shadow'])
            x += font.width(t)


def badge(c, glow, fonts, cx, cy, value, kind):
    c.put(c.disc(cx, cy, 8.6), P['ink'])
    ring = c.disc(cx, cy, 7.8) & ~c.disc(cx, cy, 6.2)
    c.put(ring, P['steel2'])
    c.put(ring & (c.y < cy), P['steel3'])
    c.put(ring & (c.y > cy + 2), P['steel1'])
    if value <= 0:
        c.put(c.disc(cx, cy, 6.0), P['plate0'])
        c.put(c.disc(cx, cy, 6.0) & (c.y > cy + 3), P['plate1'])
        return
    cols = [P[kind + '0'], P[kind + '1'], P[kind + '2'], P[kind + '3']]
    gem(c, glow, cx, cy, 5.8, cols, P['dmg'] if kind == 'red' else P['blk'], 0.25)
    f = fonts['number']
    t = str(value)
    f.draw(c, t, int(cx) - f.width(t) // 2 + 1, int(cy) - 4, P['text'], outline=P['shadow'])


def outline_sprite():
    """Hover outline: two rings around the card silhouette (2px margin), drawn additively in Unity."""
    c = Canvas(W + 4, H + 4)
    inner = Canvas(W, H)
    sil = silhouette(inner)
    big = np.zeros((H + 4, W + 4), bool)
    big[2:H + 2, 2:W + 2] = sil
    r1 = dilate(big) & ~big
    r2 = dilate(dilate(big), True) & ~dilate(big)
    c.put(r1, hexc('#ffffff'))
    c.put(r2, hexc('#ffffff', 0.5))
    return c
