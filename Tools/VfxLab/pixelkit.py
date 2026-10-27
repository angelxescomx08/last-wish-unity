"""Tiny pixel-art raster kit used by the VFX Lab art generator.

Everything works on float RGBA numpy arrays (values 0..1, origin top-left, pixel centres at +0.5).
Translucency in the base art is done with ordered (Bayer) dithering so the result stays pure pixel art;
the emissive layers are additive light maps (RGB on black) that Unity draws with an HDR additive shader.
"""
import json
import math

import numpy as np
from PIL import Image, ImageDraw

BAYER4 = (np.array([[0, 8, 2, 10], [12, 4, 14, 6], [3, 11, 1, 9], [15, 7, 13, 5]], float) + 0.5) / 16.0


def hexc(h, a=1.0):
    h = h.lstrip('#')
    return np.array([int(h[0:2], 16) / 255.0, int(h[2:4], 16) / 255.0, int(h[4:6], 16) / 255.0, a], float)


def mix(c0, c1, t):
    return np.asarray(c0, float) * (1 - t) + np.asarray(c1, float) * t


def bayer(w, h, ox=0, oy=0):
    ys = (np.arange(h)[:, None] + oy) % 4
    xs = (np.arange(w)[None, :] + ox) % 4
    return BAYER4[ys, xs]


def dilate(m, diagonal=False):
    o = m.copy()
    o[1:, :] |= m[:-1, :]
    o[:-1, :] |= m[1:, :]
    o[:, 1:] |= m[:, :-1]
    o[:, :-1] |= m[:, 1:]
    if diagonal:
        o[1:, 1:] |= m[:-1, :-1]
        o[1:, :-1] |= m[:-1, 1:]
        o[:-1, 1:] |= m[1:, :-1]
        o[:-1, :-1] |= m[1:, 1:]
    return o


def erode(m):
    return ~dilate(~m)


def shift(m, dx, dy):
    """Shift a mask by whole pixels (content moves by +dx, +dy); outside is False."""
    o = np.zeros_like(m)
    h, w = m.shape
    xs0, xs1 = max(0, -dx), min(w, w - dx)
    ys0, ys1 = max(0, -dy), min(h, h - dy)
    if xs1 > xs0 and ys1 > ys0:
        o[ys0 + dy:ys1 + dy, xs0 + dx:xs1 + dx] = m[ys0:ys1, xs0:xs1]
    return o


class Canvas:
    """RGBA float canvas with a few pixel-art primitives."""

    def __init__(self, w, h, fill=None):
        self.w, self.h = w, h
        self.px = np.zeros((h, w, 4), float)
        if fill is not None:
            self.px[:] = fill
        yy, xx = np.mgrid[0:h, 0:w]
        self.x = xx + 0.5
        self.y = yy + 0.5
        self.B = bayer(w, h)

    # -- masks -------------------------------------------------------------------------
    def poly(self, points):
        img = Image.new('L', (self.w, self.h), 0)
        ImageDraw.Draw(img).polygon([(float(x), float(y)) for x, y in points], fill=1)
        return np.array(img).astype(bool)

    def rect(self, x0, y0, x1, y1):
        """Inclusive pixel rectangle."""
        m = np.zeros((self.h, self.w), bool)
        m[max(0, y0):max(0, y1 + 1), max(0, x0):max(0, x1 + 1)] = True
        return m

    def disc(self, cx, cy, r):
        return (self.x - cx) ** 2 + (self.y - cy) ** 2 <= r * r

    def line(self, x0, y0, x1, y1):
        m = np.zeros((self.h, self.w), bool)
        for x, y in bresenham(int(round(x0)), int(round(y0)), int(round(x1)), int(round(y1))):
            if 0 <= x < self.w and 0 <= y < self.h:
                m[y, x] = True
        return m

    def pixel(self, x, y):
        m = np.zeros((self.h, self.w), bool)
        x, y = int(math.floor(x)), int(math.floor(y))
        if 0 <= x < self.w and 0 <= y < self.h:
            m[y, x] = True
        return m

    # -- painting ----------------------------------------------------------------------
    def put(self, mask, color, alpha=1.0, mode='dither'):
        """Paint `color` where mask. alpha (scalar or array) < 1 is dithered (pixel art) or blended."""
        col = np.asarray(color, float)
        a = np.broadcast_to(np.asarray(alpha, float), (self.h, self.w)) * col[3]
        m = mask.astype(bool)
        if mode == 'dither':
            m = m & (self.B < a)
            self.px[m, :3] = col[:3]
            self.px[m, 3] = 1.0
        else:
            aa = np.where(m, a, 0.0)[..., None]
            self.px[..., :3] = self.px[..., :3] * (1 - aa) + col[:3] * aa
            self.px[..., 3:4] = self.px[..., 3:4] * (1 - aa) + aa

    def add(self, mask, color, alpha=1.0):
        """Additive light (emissive maps)."""
        col = np.asarray(color, float)
        a = np.broadcast_to(np.asarray(alpha, float), (self.h, self.w))
        m = mask.astype(bool)
        self.px[..., :3] += np.where(m[..., None], col[:3] * a[..., None], 0.0)
        self.px[..., 3] = np.maximum(self.px[..., 3], np.where(m, 1.0, 0.0))

    def paste(self, other, ox=0, oy=0):
        """Alpha-over another canvas (opaque pixels only)."""
        h, w = other.h, other.w
        x0, y0 = max(0, ox), max(0, oy)
        x1, y1 = min(self.w, ox + w), min(self.h, oy + h)
        if x1 <= x0 or y1 <= y0:
            return
        src = other.px[y0 - oy:y1 - oy, x0 - ox:x1 - ox]
        dst = self.px[y0:y1, x0:x1]
        a = src[..., 3:4]
        dst[..., :3] = dst[..., :3] * (1 - a) + src[..., :3] * a
        dst[..., 3:4] = np.maximum(dst[..., 3:4], a)

    def mask(self):
        return self.px[..., 3] > 0.5

    def image(self, emissive=False):
        p = self.px.copy()
        if emissive:
            rgb = np.clip(p[..., :3], 0, 1)
            a = (rgb.max(axis=2) > 0.002).astype(float)
            p = np.concatenate([rgb, a[..., None]], axis=2)
        p = np.clip(p, 0, 1)
        return Image.fromarray((p * 255 + 0.5).astype(np.uint8), 'RGBA')


def bresenham(x0, y0, x1, y1):
    pts = []
    dx, dy = abs(x1 - x0), -abs(y1 - y0)
    sx, sy = (1 if x0 < x1 else -1), (1 if y0 < y1 else -1)
    err = dx + dy
    while True:
        pts.append((x0, y0))
        if x0 == x1 and y0 == y1:
            break
        e2 = 2 * err
        if e2 >= dy:
            err += dy
            x0 += sx
        if e2 <= dx:
            err += dx
            y0 += sy
    return pts


class BitmapFont:
    """The game's exported pixel fonts (PNG atlas + JSON metrics: glyph = [x, y, w, h, advance])."""

    def __init__(self, png, meta):
        self.atlas = np.array(Image.open(png).convert('RGBA'))[..., 3] > 127
        with open(meta, encoding='utf-8') as f:
            self.meta = json.load(f)
        self.glyphs = self.meta['glyphs']
        self.height = self.meta['height']

    def width(self, text):
        return sum(self.glyphs.get(str(ord(c)), self.glyphs['32'])[4] for c in text)

    def mask(self, canvas, text, x, y):
        m = np.zeros((canvas.h, canvas.w), bool)
        cx = int(x)
        for ch in text:
            g = self.glyphs.get(str(ord(ch)), self.glyphs['32'])
            gx, gy, gw, gh, adv = g
            sub = self.atlas[gy:gy + gh, gx:gx + gw]
            for yy in range(sub.shape[0]):
                for xx in range(sub.shape[1]):
                    if sub[yy, xx]:
                        px, py = cx + xx, int(y) + yy
                        if 0 <= px < canvas.w and 0 <= py < canvas.h:
                            m[py, px] = True
            cx += adv
        return m

    def draw(self, canvas, text, x, y, color, shadow=None, outline=None):
        m = self.mask(canvas, text, x, y)
        if outline is not None:
            canvas.put(dilate(m, True) & ~m, outline)
        if shadow is not None:
            canvas.put(shift(m, 0, 1) & ~m, shadow)
        canvas.put(m, color)
        return m


def save_sheet(frames, path, emissive=False):
    """Frames side by side, left to right."""
    w, h = frames[0].w, frames[0].h
    sheet = Image.new('RGBA', (w * len(frames), h), (0, 0, 0, 0))
    for i, f in enumerate(frames):
        sheet.paste(f.image(emissive), (i * w, 0))
    sheet.save(path, optimize=True)
    return sheet
