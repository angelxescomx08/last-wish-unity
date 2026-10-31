#!/usr/bin/env python3
"""Exports the hero sheets drawn in the approved art style (docs/art-style.md) to the game:
El Mago (Tools/VfxLab/mage_hero.html) and La Guerrera (Tools/VfxLab/warrior_hero.html), drawn by code as one figure per
frame and run with Node through mage_hero_html.py / warrior_hero_html.py, and La Pícara (Tools/VfxLab/rogue_hero_art.py,
pending a redraw with the same method).

Writes Assets/Resources/Game/Heroes/, per hero <id>:
  <id>_96.png        native 96 px cells, already lit (paint + emissive x 0.6, hot cores to white)
  <id>_192.png       the same x2 (nearest): the combat size
  <id>_halo_96.png   soft light of the emissive layer (blurred), drawn additively over the sprite (the game has no bloom)
  <id>.json          HeroSheets metadata: rows, 12 fps frames (83/84 ms), strike and cast events, halo
One row per animation (idle, attack, guard, hurt, cast, death), one column per frame.

Validation (exit 1 when broken): every action except death ends on idle frame 0 pixel for pixel, nothing touches the
cell border except the frames a hero allows (`BORDER_OK`: the rogue's strike, the mage's bolt, the warrior's slash), feet on row 93, the
death pose is held and not empty, and the figure of every frame is ONE 4-connected mass (heroes are never assembled
from pieces: docs/art-style.md).

Usage (the mage and the warrior need Node.js on PATH):
  python Tools/VfxLab/export_hero_art.py                       # write every hero
  python Tools/VfxLab/export_hero_art.py --hero mage           # only one
  python Tools/VfxLab/export_hero_art.py --check               # compare with the files on disk
  python Tools/VfxLab/export_hero_art.py --preview DIR         # also GIFs per animation (x3, on the dungeon blue)
"""
import argparse
import io
import json
import os
import sys

from collections import deque

import numpy as np
from PIL import Image, ImageFilter

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)

import mage_hero_html  # noqa: E402
import rogue_hero_art  # noqa: E402
import warrior_hero_html  # noqa: E402

ROOT = os.path.abspath(os.path.join(HERE, '..', '..'))
EMISSIVE_GAIN = 0.6
HALO_GAIN = 0.55
HEROES = {'rogue': rogue_hero_art, 'mage': mage_hero_html, 'warrior': warrior_hero_html}


def columns(R):
    return max(n for _, n, _ in R.ANIMATIONS)


def border_ok(R, name, i):
    return (name, i) in getattr(R, 'BORDER_OK', {('attack', R.STRIKE_FRAME)})


def lit(c, g):
    """RGBA: paint + emissive, values over 1 pushed towards white; alpha from the paint."""
    a = c.px[..., 3:4]
    hdr = c.px[..., :3] + np.clip(g.px[..., :3], 0, 4) * EMISSIVE_GAIN
    hot = np.clip(hdr.max(axis=2, keepdims=True) - 1, 0, None) * 0.5
    rgb = np.clip(np.clip(hdr, 0, 1) + hot, 0, 1)
    rgba = np.concatenate([rgb, (a > 0.01).astype(float)], axis=2)
    rgba[rgba[..., 3] == 0] = 0
    return (rgba * 255 + 0.5).astype(np.uint8)


def halo(g):
    e = np.clip(g.px[..., :3], 0, 2.5) / 2.5
    im = Image.fromarray((e * 255).astype(np.uint8))
    near = np.asarray(im.filter(ImageFilter.GaussianBlur(1.4)), float) / 255
    far = np.asarray(im.filter(ImageFilter.GaussianBlur(4.0)), float) / 255
    v = np.clip(near * 0.9 + far * 1.1, 0, 1)
    return np.concatenate([(v * 255 + 0.5).astype(np.uint8), np.full(v.shape[:2] + (1,), 255, np.uint8)], axis=2)


def render_all(R):
    frames = {}
    for name, n, _ in R.ANIMATIONS:
        cs, gs = R.render(name)
        frames[name] = [(lit(c, g), halo(g)) for c, g in zip(cs, gs)]
    return frames


def components(m):
    """Number of 4-connected regions of a mask."""
    seen = np.zeros_like(m)
    h, w = m.shape
    n = 0
    for y, x in zip(*np.where(m)):
        if seen[y, x]:
            continue
        n += 1
        q = deque([(y, x)])
        seen[y, x] = True
        while q:
            a, b = q.popleft()
            for da, db in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                yy, xx = a + da, b + db
                if 0 <= yy < h and 0 <= xx < w and m[yy, xx] and not seen[yy, xx]:
                    seen[yy, xx] = True
                    q.append((yy, xx))
    return n


def validate(R, frames):
    problems = []
    idle0 = frames['idle'][0][0]
    for name, n, loop in R.ANIMATIONS:
        fr = frames[name]
        if len(fr) != n:
            problems.append(f'{name}: {len(fr)} frames, expected {n}')
        for i, (rgba, _) in enumerate(fr):
            a = rgba[..., 3] > 0
            if not a.any():
                problems.append(f'{name} {i}: empty')
                continue
            ys, xs = np.where(a)
            if (xs.min() == 0 or ys.min() == 0 or xs.max() == R.CELL - 1 or ys.max() == R.CELL - 1) \
                    and not border_ok(R, name, i):
                problems.append(f'{name} {i}: touches the cell border')
            k = components(R.mass(name, i))
            if k != 1:
                problems.append(f'{name} {i}: the figure is {k} separate regions, not one mass')
        if name not in ('idle', 'death') and not np.array_equal(fr[-1][0], idle0):
            problems.append(f'{name}: does not end on idle frame 0')
    feet = np.where(idle0[..., 3] > 0)[0].max()
    if feet != R.GROUND:
        problems.append(f'idle feet on row {feet}, expected {R.GROUND}')
    return problems


def sheets(R, frames):
    rows, cols = len(R.ANIMATIONS), columns(R)
    s96 = np.zeros((rows * R.CELL, cols * R.CELL, 4), np.uint8)
    h96 = np.zeros((rows * R.CELL, cols * R.CELL, 4), np.uint8)
    h96[..., 3] = 255
    for r, (name, n, _) in enumerate(R.ANIMATIONS):
        for i, (rgba, light) in enumerate(frames[name]):
            s96[r * R.CELL:(r + 1) * R.CELL, i * R.CELL:(i + 1) * R.CELL] = rgba
            h96[r * R.CELL:(r + 1) * R.CELL, i * R.CELL:(i + 1) * R.CELL] = light
    s192 = np.repeat(np.repeat(s96, 2, 0), 2, 1)
    return Image.fromarray(s96, 'RGBA'), Image.fromarray(s192, 'RGBA'), Image.fromarray(h96, 'RGBA')


def metadata(hero, R):
    sx, sy = R.attack_strike_point()
    cx, cy = R.cast_point()
    cols = columns(R)
    return {
        'cell': 192,
        'columns': cols,
        'sheets': {'192': f'Heroes/{hero}_192', '96': f'Heroes/{hero}_96'},
        'pack': {'192': {'columns': cols, 'split': 1}, '96': {'columns': cols, 'split': 1}},
        'halo': {'sheet': f'Heroes/{hero}_halo_96', 'cell': R.CELL, 'columns': cols, 'gain': HALO_GAIN},
        'events': {
            'attack': {'strike_frame': R.STRIKE_FRAME, 'strike_point': [round(sx, 3), round(sy, 3)]},
            'cast': {'cast_frame': R.CAST_FRAME, 'cast_point': [round(cx, 3), round(cy, 3)]},
        },
        'animations': {name: {'row': r, 'durations_ms': R.durations_ms(n), 'loop': loop}
                       for r, (name, n, loop) in enumerate(R.ANIMATIONS)},
        'style': 'docs/art-style.md',
        'generator': getattr(R, 'GENERATOR', f'Tools/VfxLab/{R.__name__}.py'),
    }


def png(image):
    buf = io.BytesIO()
    image.save(buf, 'PNG', optimize=True)
    return buf.getvalue()


def build(hero):
    R = HEROES[hero]
    frames = render_all(R)
    problems = [f'{hero} {p}' for p in validate(R, frames)]
    s96, s192, h96 = sheets(R, frames)
    files = {
        f'{hero}_96.png': png(s96), f'{hero}_192.png': png(s192), f'{hero}_halo_96.png': png(h96),
        f'{hero}.json': (json.dumps(metadata(hero, R), indent=2) + '\n').encode('utf-8'),
    }
    return files, frames, problems


def preview(hero, frames, out_dir, scale=3):
    R = HEROES[hero]
    os.makedirs(out_dir, exist_ok=True)
    bg = np.array([42, 46, 74], float) / 255
    for name, n, _ in R.ANIMATIONS:
        imgs = []
        for rgba, light in frames[name]:
            a = rgba[..., 3:4] / 255.0
            rgb = rgba[..., :3] / 255.0 * a + bg * (1 - a) + light[..., :3] / 255.0 * HALO_GAIN
            im = Image.fromarray((np.clip(rgb, 0, 1) * 255).astype(np.uint8))
            imgs.append(im.resize((R.CELL * scale, R.CELL * scale), Image.NEAREST))
        imgs[0].save(os.path.join(out_dir, f'{hero}_{name}.gif'), save_all=True, append_images=imgs[1:],
                     duration=R.durations_ms(n), loop=0)


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument('--root', default=ROOT)
    ap.add_argument('--hero', choices=sorted(HEROES), action='append', help='only these heroes (default: all)')
    ap.add_argument('--check', action='store_true')
    ap.add_argument('--preview', default=None)
    args = ap.parse_args()
    out = os.path.join(args.root, 'Assets', 'Resources', 'Game', 'Heroes')
    bad, problems, built = [], [], []
    for hero in args.hero or list(HEROES):
        files, frames, probs = build(hero)
        problems += probs
        built.append((hero, files, frames))
    for p in problems:
        print('INVALID:', p)
    if args.check:
        for hero, files, _ in built:
            for name, data in files.items():
                path = os.path.join(out, name)
                if not os.path.exists(path):
                    bad.append(name + ' (missing)')
                elif name.endswith('.png'):
                    if not np.array_equal(np.asarray(Image.open(path).convert('RGBA')),
                                          np.asarray(Image.open(io.BytesIO(data)).convert('RGBA'))):
                        bad.append(name)
                elif open(path, 'rb').read().replace(b'\r\n', b'\n') != data:
                    bad.append(name)
        print('OK' if not bad and not problems else 'Differs: ' + ', '.join(bad))
        return 1 if bad or problems else 0
    if problems:
        return 1
    os.makedirs(out, exist_ok=True)
    for hero, files, frames in built:
        for name, data in files.items():
            with open(os.path.join(out, name), 'wb') as f:
                f.write(data)
        if args.preview:
            preview(hero, frames, args.preview)
    print('Hero sheets written to', out)
    return 0


if __name__ == '__main__':
    sys.exit(main())
