#!/usr/bin/env python3
"""The room icons of the floor map, drawn by code in the art style (docs/art-style.md): one ink outline, ramps lit from
the upper left, Bayer dithering, small emissive bits (eyes, gems, flames, runes) that the game also uses for a halo.

Writes, in Assets/Resources/Game/Map: map_icons.png (native pixels, one 20x20 cell per icon; Scenes/MapScene.cs draws
them x2), map_icons_halo.png (the same cells, black but for the emissive bits, added over the icons of the rooms you can
enter) and map_icons.json ({"cell": 20, "icons": {"Combat": [x, y, w, h], ...}, "glow": {"Combat": [r, g, b], ...}}).
Unchanged PNGs are not rewritten. --check compares instead of writing (exit 1 if they differ).

Usage: python Tools/VfxLab/map_icons.py [--check] [--preview FILE]
"""
import argparse
import io
import json
import os
import sys

import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, '..', '..'))
OUT = os.path.join(ROOT, 'Assets', 'Resources', 'Game', 'Map')
CELL = 20
BAY = (np.array([[0, 8, 2, 10], [12, 4, 14, 6], [3, 11, 1, 9], [15, 7, 13, 5]], float) + 0.5) / 16.0


def hexc(h):
    return np.array([int(h[1:3], 16), int(h[3:5], 16), int(h[5:7], 16)], float) / 255.0


def ramp(*hs):
    return [hexc(h) for h in hs]


INK = hexc('#07050c')
STEEL = ramp('#27304c', '#4c5a84', '#8193c0', '#c3d0f0', '#f2f6ff')
GOLD = ramp('#5e4626', '#8a6a3a', '#d9b26a', '#f4dc9c')
BONE = ramp('#4a4250', '#7d7486', '#b3a9b6', '#e2dbe0')
WOOD = ramp('#2a1610', '#4a2a1a', '#6e4328', '#94603a')
LEATHER = ramp('#2a1620', '#46263a', '#6a3c55', '#8e5a74')
STONE = ramp('#1d1a2c', '#2f2a44', '#463f63', '#635a86')
IRON = ramp('#141220', '#232036', '#36324f', '#4d4870')
CRIMSON = ramp('#3d0f14', '#7a1a22', '#c8322e', '#ff7a4a', '#ffe0b0')
CYAN = ramp('#0d3550', '#137ea3', '#2fc4e6', '#8ff1ff', '#e6feff')
VIOLET = ramp('#2d1346', '#55248a', '#8a47cf', '#c79bff', '#f0e0ff')
GREEN = ramp('#123020', '#1f5a32', '#3c9a4c', '#8be07a', '#e6ffd0')
FIRE = ramp('#3d0f14', '#a8301e', '#f07a2a', '#ffd25a', '#fff6dc')
PINK = ramp('#3a0e2a', '#7a1f55', '#c8408a', '#ff8ac4')


class Icon:
    """One icon: paint (RGBA) and emissive (RGB) on a w x h grid of pixel centres."""

    def __init__(self, w=16, h=16):
        self.w, self.h = w, h
        self.px = np.zeros((h, w, 4))
        self.glow = np.zeros((h, w, 3))
        yy, xx = np.mgrid[0:h, 0:w]
        self.x, self.y = xx + 0.5, yy + 0.5
        self.B = BAY[yy % 4, xx % 4]
        self.shape = np.zeros((h, w), bool)

    # masks
    def rect(self, x0, y0, x1, y1):
        return (self.x >= x0) & (self.x <= x1) & (self.y >= y0) & (self.y <= y1)

    def ell(self, cx, cy, rx, ry):
        return ((self.x - cx) / rx) ** 2 + ((self.y - cy) / ry) ** 2 <= 1

    def seg(self, x0, y0, x1, y1, r):
        dx, dy = x1 - x0, y1 - y0
        L = max(1e-6, dx * dx + dy * dy)
        t = np.clip(((self.x - x0) * dx + (self.y - y0) * dy) / L, 0, 1)
        return (self.x - (x0 + t * dx)) ** 2 + (self.y - (y0 + t * dy)) ** 2 <= r * r

    def poly(self, pts):
        m = np.zeros((self.h, self.w), bool)
        n = len(pts)
        for j in range(self.h):
            for i in range(self.w):
                x, y, inside = i + 0.5, j + 0.5, False
                for k in range(n):
                    (x0, y0), (x1, y1) = pts[k], pts[(k + 1) % n]
                    if (y0 > y) != (y1 > y) and x < x0 + (y - y0) * (x1 - x0) / (y1 - y0):
                        inside = not inside
                m[j, i] = inside
        return m

    # painting
    def zone(self, m, rp, light=None, amp=0.55):
        """Paint mask m with ramp rp; light (array or float) 0..1, default lit from the upper left inside the mask."""
        if light is None:
            ys, xs = np.where(m)
            if len(xs) == 0:
                return
            x0, x1, y0, y1 = xs.min(), xs.max() + 1, ys.min(), ys.max() + 1
            light = 0.78 - 0.45 * ((self.x - x0) / max(1, x1 - x0) * 0.55 + (self.y - y0) / max(1, y1 - y0) * 0.45)
        light = np.broadcast_to(np.asarray(light, float), m.shape)
        n = len(rp)
        idx = np.clip(np.floor(light * (n - 1) + 0.5 + (self.B - 0.5) * amp), 0, n - 1).astype(int)
        for k in range(n):
            mm = m & (idx == k)
            self.px[mm, :3] = rp[k]
            self.px[mm, 3] = 1
        self.shape |= m

    def put(self, m, col):
        self.px[m, :3] = col
        self.px[m, 3] = 1
        self.shape |= m

    def emit(self, m, col, k=1.0):
        self.glow[m] += np.asarray(col) * k

    def pixel(self, x, y):
        return (np.floor(self.x) == x) & (np.floor(self.y) == y)

    def finish(self):
        """One ink outline around the whole icon."""
        s = self.shape
        d = s.copy()
        d[1:, :] |= s[:-1, :]; d[:-1, :] |= s[1:, :]; d[:, 1:] |= s[:, :-1]; d[:, :-1] |= s[:, 1:]
        o = d & ~s
        self.px[o, :3] = INK
        self.px[o, 3] = 1
        return self


def combat():
    ic = Icon()
    for (x0, y0, x1, y1) in ((3.2, 12.8, 12.6, 3.4), (12.8, 12.8, 3.4, 3.4)):
        blade = ic.seg(x0 + (x1 - x0) * 0.28, y0 + (y1 - y0) * 0.28, x1, y1, 1.05)
        ic.zone(blade, STEEL, light=0.62)
        lit = ic.seg(x0 + (x1 - x0) * 0.32, y0 + (y1 - y0) * 0.32, x1, y1, 0.45) & blade
        ic.put(lit, STEEL[4] if x1 > x0 else STEEL[3])
    for (x0, y0, x1, y1) in ((3.2, 12.8, 12.6, 3.4), (12.8, 12.8, 3.4, 3.4)):
        gx, gy = x0 + (x1 - x0) * 0.26, y0 + (y1 - y0) * 0.26
        nx, ny = (y1 - y0), -(x1 - x0)
        L = (nx * nx + ny * ny) ** 0.5
        nx, ny = nx / L * 2.6, ny / L * 2.6
        ic.zone(ic.seg(gx - nx, gy - ny, gx + nx, gy + ny, 0.75), GOLD, light=0.7)
        ic.zone(ic.seg(x0, y0, gx, gy, 0.7), LEATHER, light=0.55)
        ic.put(ic.ell(x0, y0, 1.1, 1.1), GOLD[2])
    return ic.finish()


def elite():
    ic = Icon()
    horns = ic.seg(4.6, 6.2, 1.6, 1.8, 1.05) | ic.seg(11.4, 6.2, 14.4, 1.8, 1.05)
    ic.zone(horns, BONE, light=0.35)
    ic.put(ic.pixel(1, 1) | ic.pixel(14, 1), BONE[3])
    skull = ic.ell(8, 7.6, 4.8, 4.4) | ic.rect(5.2, 9.5, 10.8, 13.4)
    ic.zone(skull, BONE)
    sockets = ic.ell(6.1, 8.2, 1.25, 1.25) | ic.ell(9.9, 8.2, 1.25, 1.25)
    ic.put(sockets, INK)
    eyes = ic.pixel(6, 8) | ic.pixel(9, 8)
    ic.put(eyes, CRIMSON[3]); ic.emit(eyes, (1.0, 0.25, 0.15), 1.4)
    teeth = ic.rect(6, 12, 10, 13) & ((np.floor(ic.x) % 2) == 1)
    ic.put(teeth, INK)
    ic.put(ic.pixel(8, 10), INK)
    return ic.finish()


def boss():
    ic = Icon(20, 20)
    crown = ic.poly([(4.2, 6.4), (5.2, 1.6), (7.6, 4.6), (10, 0.8), (12.4, 4.6), (14.8, 1.6), (15.8, 6.4)])
    ic.zone(crown, GOLD)
    gem = ic.pixel(9, 4) | ic.pixel(10, 4)
    ic.put(gem, CRIMSON[3]); ic.emit(gem, (1.0, 0.25, 0.15), 1.0)
    skull = ic.ell(10, 10.5, 6.3, 5.3) | ic.rect(6.2, 12.5, 13.8, 17.6)
    ic.zone(skull & ~crown, BONE)
    sockets = ic.ell(7.6, 11.0, 1.7, 1.6) | ic.ell(12.4, 11.0, 1.7, 1.6)
    ic.put(sockets, INK)
    eyes = ic.pixel(7, 11) | ic.pixel(12, 11) | ic.pixel(8, 11) | ic.pixel(11, 11)
    ic.put(eyes & (ic.pixel(7, 11) | ic.pixel(12, 11)), CRIMSON[4]); ic.put(eyes & (ic.pixel(8, 11) | ic.pixel(11, 11)), CRIMSON[2])
    ic.emit(eyes, (1.0, 0.25, 0.15), 1.5)
    ic.put(ic.pixel(9, 14) | ic.pixel(10, 14), INK)
    teeth = ic.rect(7, 16, 13, 17) & ((np.floor(ic.x) % 2) == 0)
    ic.put(teeth, INK)
    return ic.finish()


def treasure():
    ic = Icon()
    lid = ic.ell(8, 7.0, 6.4, 3.4) & (ic.y <= 7.2) | ic.rect(1.6, 6.0, 14.4, 7.4)
    body = ic.rect(1.6, 7.6, 14.4, 13.6)
    ic.zone(lid, WOOD, light=0.75 - 0.03 * ic.x)
    ic.zone(body, WOOD, light=0.55 - 0.03 * ic.x)
    bands = (ic.rect(3.6, 3.4, 4.6, 13.6) | ic.rect(11.4, 3.4, 12.4, 13.6)) & (lid | body)
    ic.zone(bands, GOLD, light=0.65)
    rim = ic.rect(1.6, 7.4, 14.4, 7.6) | (ic.rect(1.6, 7.0, 14.4, 7.9) & ~lid)
    ic.put(ic.rect(1.6, 7.5, 14.4, 8.4), GOLD[1])
    lock = ic.rect(7, 8, 9, 10.4)
    ic.zone(lock, GOLD, light=0.8)
    ic.put(ic.pixel(7, 9) | ic.pixel(8, 9), INK)
    shine = ic.pixel(12, 2) | ic.pixel(13, 1) | ic.pixel(14, 2) | ic.pixel(13, 3)
    ic.put(ic.pixel(13, 2), GOLD[3]); ic.emit(ic.pixel(13, 2), (1.0, 0.85, 0.5), 0.8)
    return ic.finish()


def shop():
    ic = Icon()
    bag = ic.ell(7.2, 10.2, 5.4, 4.6) | ic.poly([(4.4, 6.2), (10.0, 6.2), (8.6, 8.4), (5.8, 8.4)])
    ic.zone(bag, LEATHER)
    neck = ic.ell(7.2, 4.4, 2.6, 1.5)
    ic.zone(neck, LEATHER, light=0.7)
    tie = ic.rect(4.6, 6.0, 9.8, 6.9)
    ic.put(tie, GOLD[1]); ic.put(ic.pixel(5, 6) | ic.pixel(6, 6), GOLD[2])
    coin = ic.ell(11.6, 11.6, 3.2, 3.2)
    ic.zone(coin, GOLD, light=0.75 - 0.05 * (ic.x - 9) - 0.04 * (ic.y - 9))
    ic.put(ic.ell(11.6, 11.6, 1.6, 1.6) & ~ic.ell(11.6, 11.6, 0.9, 0.9), GOLD[1])
    ic.put(ic.pixel(10, 10), GOLD[3]); ic.emit(ic.pixel(10, 10), (1.0, 0.85, 0.5), 0.6)
    return ic.finish()


def event():
    ic = Icon()
    stone = ic.poly([(3, 15), (3, 5), (5, 2), (11, 2), (13, 5), (13, 15)])
    ic.zone(stone, STONE)
    q = (ic.ell(8, 6.6, 2.6, 2.4) & ~ic.ell(8, 6.8, 1.15, 1.0) & ~((ic.x < 8) & (ic.y > 6.8))) | ic.rect(7.4, 8.4, 8.6, 10.6) | ic.rect(7.4, 11.8, 8.6, 12.8)
    q &= stone
    ic.put(q, VIOLET[3]); ic.put(q & (ic.y < 6), VIOLET[4])
    ic.emit(q, (0.6, 0.3, 1.0), 1.2)
    return ic.finish()


def warlock():
    ic = Icon()
    pot = ic.ell(8, 10.6, 6.2, 4.4) & (ic.y >= 7.4)
    ic.zone(pot, IRON)
    rim = ic.ell(8, 7.6, 6.6, 1.6)
    ic.zone(rim, IRON, light=0.85)
    brew = ic.ell(8, 7.5, 5.0, 0.9)
    ic.put(brew, GREEN[2]); ic.emit(brew, (0.4, 1.0, 0.3), 0.7)
    for (bx, by, r) in ((6, 5.0, 1.0), (9.5, 3.6, 1.2), (8, 1.6, 0.8)):
        b = ic.ell(bx, by, r, r)
        ic.put(b, GREEN[3]); ic.put(b & ic.pixel(int(bx - 0.5), int(by - 0.5)), GREEN[4]); ic.emit(b, (0.4, 1.0, 0.3), 0.9)
    legs = ic.rect(3.4, 13.4, 4.6, 15.0) | ic.rect(11.4, 13.4, 12.6, 15.0)
    ic.zone(legs, IRON, light=0.3)
    return ic.finish()


def gacha():
    ic = Icon()
    globe = ic.ell(8, 6.2, 5.4, 5.0)
    ic.zone(globe, CYAN, light=0.35 - 0.02 * (ic.x - 3))
    for (cx, cy, rp) in ((6.2, 7.6, PINK), (9.6, 8.0, GOLD), (8.0, 4.8, VIOLET), (10.6, 5.2, GREEN), (5.4, 4.8, CRIMSON)):
        cap = ic.ell(cx, cy, 1.25, 1.25) & globe
        ic.zone(cap, rp, light=0.75)
    ic.put(ic.pixel(5, 3) | ic.pixel(4, 4), CYAN[4]); ic.emit(ic.pixel(5, 3) | ic.pixel(4, 4), (0.25, 0.85, 1.0), 0.6)
    base = ic.rect(3.4, 10.8, 12.6, 15.0)
    ic.zone(base, CRIMSON[:4], light=0.62 - 0.03 * ic.x)
    slot = ic.rect(7, 12, 9, 13)
    ic.put(slot, INK)
    ic.put(ic.pixel(10, 13) | ic.pixel(11, 13), GOLD[2])
    return ic.finish()


def purge():
    ic = Icon()
    altar = ic.rect(3.0, 10.4, 13.0, 15.0)
    ic.zone(altar, STONE)
    top = ic.rect(2.0, 9.2, 14.0, 10.6)
    ic.zone(top, STONE, light=0.85)
    flame = ic.ell(8, 6.6, 3.4, 3.4) | ic.poly([(5.0, 6.4), (8, -0.4), (11.0, 6.4)])
    flame &= ic.y < 9.4
    rr = np.hypot(ic.x - 8, (ic.y - 7.4) * 0.8)
    ic.zone(flame, FIRE, light=np.clip(1.15 - rr / 4.2, 0, 1), amp=0.35)
    ic.emit(flame, (1.0, 0.55, 0.2), 0.9)
    ic.emit(flame & (rr < 1.6), (1.0, 0.9, 0.6), 0.6)
    return ic.finish()


def marker():
    ic = Icon(11, 13)
    gem = ic.poly([(5.5, 0.4), (10.4, 5.2), (5.5, 12.6), (0.6, 5.2)])
    ic.zone(gem, GOLD)
    core = ic.poly([(5.5, 2.4), (8.4, 5.4), (5.5, 9.8), (2.6, 5.4)])
    ic.zone(core, CYAN, light=0.85 - 0.04 * ic.x - 0.03 * ic.y, amp=0.3)
    ic.put(ic.pixel(4, 4), CYAN[4])
    ic.emit(core, (0.25, 0.85, 1.0), 1.2)
    return ic.finish()


ICONS = {'Combat': combat, 'Elite': elite, 'Boss': boss, 'Treasure': treasure, 'Shop': shop, 'Event': event,
         'Warlock': warlock, 'Gacha': gacha, 'Purge': purge, 'Marker': marker}
GLOW = {'Combat': [140, 170, 230], 'Elite': [230, 60, 50], 'Boss': [235, 50, 40], 'Treasure': [235, 190, 90],
        'Shop': [230, 190, 90], 'Event': [170, 100, 255], 'Warlock': [110, 230, 90], 'Gacha': [80, 210, 255],
        'Purge': [255, 140, 50], 'Marker': [80, 210, 255]}


def build():
    names = list(ICONS)
    sheet = np.zeros((CELL, CELL * len(names), 4), np.uint8)
    halo = np.zeros((CELL, CELL * len(names), 4), np.uint8)
    halo[..., 3] = 255
    meta = {'cell': CELL, 'icons': {}, 'glow': GLOW, 'style': 'docs/art-style.md', 'generator': 'Tools/VfxLab/map_icons.py'}
    for k, name in enumerate(names):
        ic = ICONS[name]()
        ox, oy = k * CELL + (CELL - ic.w) // 2, (CELL - ic.h) // 2
        lit = np.clip(ic.px[..., :3] + np.clip(ic.glow, 0, 2) * 0.35, 0, 1)
        rgba = np.concatenate([lit, ic.px[..., 3:4]], axis=2)
        rgba[ic.px[..., 3] == 0] = 0
        sheet[oy:oy + ic.h, ox:ox + ic.w] = (rgba * 255 + 0.5).astype(np.uint8)
        halo[oy:oy + ic.h, ox:ox + ic.w, :3] = (np.clip(ic.glow / 2.0, 0, 1) * 255 + 0.5).astype(np.uint8)
        meta['icons'][name] = [ox, oy, ic.w, ic.h]
    return Image.fromarray(sheet, 'RGBA'), Image.fromarray(halo, 'RGBA'), meta


def png(im):
    b = io.BytesIO()
    im.save(b, 'PNG', optimize=True)
    return b.getvalue()


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument('--out', default=OUT)
    ap.add_argument('--check', action='store_true')
    ap.add_argument('--preview', default=None)
    a = ap.parse_args()
    sheet, halo, meta = build()
    files = {'map_icons.png': sheet, 'map_icons_halo.png': halo}
    js = (json.dumps(meta, indent=1) + '\n').encode('utf-8')
    if a.preview:
        bg = Image.new('RGBA', (sheet.width * 4, CELL * 4), (24, 20, 36, 255))
        bg.alpha_composite(sheet.resize((sheet.width * 4, CELL * 4), Image.NEAREST))
        bg.save(a.preview)
    if a.check:
        bad = [n for n, im in files.items() if not os.path.exists(os.path.join(a.out, n)) or
               not np.array_equal(np.asarray(Image.open(os.path.join(a.out, n)).convert('RGBA')), np.asarray(im))]
        jp = os.path.join(a.out, 'map_icons.json')
        if not os.path.exists(jp) or open(jp, 'rb').read().replace(b'\r\n', b'\n') != js:
            bad.append('map_icons.json')
        print('OK' if not bad else 'Differs: ' + ', '.join(bad))
        return 1 if bad else 0
    os.makedirs(a.out, exist_ok=True)
    for n, im in files.items():
        p = os.path.join(a.out, n)
        if os.path.exists(p) and np.array_equal(np.asarray(Image.open(p).convert('RGBA')), np.asarray(im)):
            continue
        open(p, 'wb').write(png(im))
    open(os.path.join(a.out, 'map_icons.json'), 'wb').write(js)
    print('Map icons written to', a.out)
    return 0


if __name__ == '__main__':
    sys.exit(main())
