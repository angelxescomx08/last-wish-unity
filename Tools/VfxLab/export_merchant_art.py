#!/usr/bin/env python3
"""Exports El Mercader, the shopkeeper of the Tienda del Viajero, to the game. The drawing lives in
Tools/VfxLab/merchant.html: its <script id="merchant-art"> draws every frame by code as ONE complete figure (docs/art-style.md
section 3.1, the mandatory method for characters). This script runs that same script with Node, so the page you open in a
browser and the game sheet always match.

Writes Assets/Resources/Game/Shop/:
  merchant_96.png       native 96 px cells, already lit (paint + emissive x 0.6, hot cores to white); one row per animation
  merchant_halo_96.png  soft light of the emissive layer (blurred), drawn additively over the sprite (the game has no bloom)
  merchant.json         rows, 12 fps frame durations, the anchors the shop uses (mouth for the speech bubble, lantern,
                        ground: the line under the soles and the width of the feet)

Validation (exit 1 when broken): feet on row 93, nothing touches the cell border, every frame is ONE 4-connected mass and
no part of the drawing came loose from the figure (the drawing drops loose parts; more than a few specks is a lost piece,
like the lantern when its hook slants), and every other animation starts (and, if it does not loop, ends) on the idle's
first frame, pixel for pixel, so the shop switches between them without a jump.

Usage (needs Node.js on PATH):
  python Tools/VfxLab/export_merchant_art.py            # write the files
  python Tools/VfxLab/export_merchant_art.py --check    # compare with the files on disk
  python Tools/VfxLab/export_merchant_art.py --preview DIR
"""
import argparse
import io
import json
import os
import re
import shutil
import subprocess
import sys
import tempfile
from collections import deque

import numpy as np
from PIL import Image, ImageFilter

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, '..', '..'))
HTML = os.path.join(HERE, 'merchant.html')
OUT = os.path.join(ROOT, 'Assets', 'Resources', 'Game', 'Shop')
EMISSIVE_GAIN, HALO_GAIN = 0.6, 0.55
MAX_LOST = 6  # specks the connectivity pass may drop from a frame (a stray feather-tip pixel); more is a lost piece

_DUMP = r"""
const A = require(process.argv[2]); const fs = require('fs');
const bufs = []; const meta = { animations: [], cell: A.CELL, ground: A.GROUND, fps: A.FPS, anchors: A.anchors() };
for (const [name, n, loop] of A.ANIMATIONS) {
  meta.animations.push([name, n, loop]);
  for (let f = 0; f < n; f++) {
    const [c, g] = A.FRAME[name](f);
    bufs.push(Buffer.from(c.px.buffer), Buffer.from(g.px.buffer), Buffer.from(A.silhouetteOf(name, f).buffer));
    meta.lost = meta.lost || {}; (meta.lost[name] = meta.lost[name] || []).push(A.lostOf ? A.lostOf(name, f) : 0);
  }
}
fs.writeFileSync(process.argv[3] + '.bin', Buffer.concat(bufs)); fs.writeFileSync(process.argv[3] + '.json', JSON.stringify(meta));
"""


def script():
    html = open(HTML, encoding='utf-8').read()
    m = re.search(r'<script id="merchant-art">(.*?)</script>', html, re.S)
    if not m:
        raise RuntimeError('merchant.html has no <script id="merchant-art"> block')
    return m.group(1)


def frames():
    node = shutil.which('node') or shutil.which('nodejs')
    if not node:
        raise RuntimeError('Node.js is needed to draw El Mercader (Tools/VfxLab/merchant.html)')
    with tempfile.TemporaryDirectory() as tmp:
        art, dump, out = os.path.join(tmp, 'merchant_art.js'), os.path.join(tmp, 'dump.js'), os.path.join(tmp, 'frames')
        open(art, 'w', encoding='utf-8').write(script())
        open(dump, 'w', encoding='utf-8').write(_DUMP)
        subprocess.run([node, dump, art, out], check=True)
        meta = json.load(open(out + '.json', encoding='utf-8'))
        raw = open(out + '.bin', 'rb').read()
    cell = meta['cell']; n_px = cell * cell; off = 0; result = {}
    for name, n, _ in meta['animations']:
        fr = []
        for _ in range(n):
            c = np.frombuffer(raw, np.float32, n_px * 4, off).reshape(cell, cell, 4); off += n_px * 16
            g = np.frombuffer(raw, np.float32, n_px * 4, off).reshape(cell, cell, 4); off += n_px * 16
            sil = np.frombuffer(raw, np.uint8, n_px, off).reshape(cell, cell).astype(bool); off += n_px
            fr.append((c, g, sil))
        result[name] = fr
    return meta, result


def lit(c, g):
    hdr = c[..., :3] + np.clip(g[..., :3], 0, 4) * EMISSIVE_GAIN
    hot = np.clip(hdr.max(axis=2, keepdims=True) - 1, 0, None) * 0.5
    rgb = np.clip(np.clip(hdr, 0, 1) + hot, 0, 1)
    rgba = np.concatenate([rgb, (c[..., 3:4] > 0.01).astype(float)], axis=2)
    rgba[rgba[..., 3] == 0] = 0
    return (rgba * 255 + 0.5).astype(np.uint8)


def halo(g):
    e = np.clip(g[..., :3], 0, 2.5) / 2.5
    im = Image.fromarray((e * 255).astype(np.uint8))
    near = np.asarray(im.filter(ImageFilter.GaussianBlur(1.4)), float) / 255
    far = np.asarray(im.filter(ImageFilter.GaussianBlur(4.0)), float) / 255
    v = np.clip(near * 0.9 + far * 1.1, 0, 1)
    return np.concatenate([(v * 255 + 0.5).astype(np.uint8), np.full(v.shape[:2] + (1,), 255, np.uint8)], axis=2)


def components(m):
    seen = np.zeros_like(m); h, w = m.shape; n = 0
    for y, x in zip(*np.where(m)):
        if seen[y, x]:
            continue
        n += 1; q = deque([(y, x)]); seen[y, x] = True
        while q:
            a, b = q.popleft()
            for da, db in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                yy, xx = a + da, b + db
                if 0 <= yy < h and 0 <= xx < w and m[yy, xx] and not seen[yy, xx]:
                    seen[yy, xx] = True; q.append((yy, xx))
    return n


def build():
    meta, fr = frames()
    cell, ground = meta['cell'], meta['ground']
    problems = []
    cols = max(n for _, n, _ in meta['animations']); rows = len(meta['animations'])
    sheet = np.zeros((rows * cell, cols * cell, 4), np.uint8); light = np.zeros_like(sheet); light[..., 3] = 255
    feet = {}
    for r, (name, n, _) in enumerate(meta['animations']):
        feet[name] = []
        for i, (c, g, sil) in enumerate(fr[name]):
            rgba = lit(c, g)
            a = rgba[..., 3] > 0
            ys, xs = np.where(a)
            if not a.any():
                problems.append(f'{name} {i}: empty'); continue
            if xs.min() == 0 or ys.min() == 0 or xs.max() == cell - 1 or ys.max() == cell - 1:
                problems.append(f'{name} {i}: touches the cell border')
            k = components(sil)
            lost = meta.get('lost', {}).get(name, [0] * n)[i]
            if lost > MAX_LOST:
                problems.append(f'{name} {i}: {lost} pixels came loose from the figure and were dropped')
            if k != 1:
                problems.append(f'{name} {i}: the figure is {k} separate regions, not one mass')
            if int(ys.max()) != ground:
                problems.append(f'{name} {i}: feet (with their outline) on row {int(ys.max())}, expected {ground}')
            low = int(np.where(sil.any(axis=1))[0].max())
            fx = np.where(sil[max(0, low - 3):low + 1].any(axis=0))[0]
            feet[name].append([round((int(fx.min()) + int(fx.max()) + 1) / 2, 1), int(fx.max()) + 1 - int(fx.min())])
            sheet[r * cell:(r + 1) * cell, i * cell:(i + 1) * cell] = rgba
            light[r * cell:(r + 1) * cell, i * cell:(i + 1) * cell] = halo(g)
    rest = sheet[:cell, :cell]
    for r, (name, n, loop) in enumerate(meta['animations']):
        if name == 'idle':
            continue
        for i in ([0] if loop else [0, n - 1]):
            if not np.array_equal(sheet[r * cell:(r + 1) * cell, i * cell:(i + 1) * cell], rest):
                problems.append(f'{name} {i}: does not match the rest pose (idle frame 0), the shop would jump')
    fps = meta['fps']
    top = int(np.where(np.asarray(sheet[:cell, :cell, 3]) > 0)[0].min())
    data = {
        'cell': cell, 'columns': cols, 'scale': 2,
        'sheet': 'Shop/merchant_96', 'halo': {'sheet': 'Shop/merchant_halo_96', 'gain': HALO_GAIN},
        'animations': {name: {'row': r, 'frames': n, 'loop': loop,
                              'durations_ms': [round((i + 1) * 1000 / fps) - round(i * 1000 / fps) for i in range(n)]}
                       for r, (name, n, loop) in enumerate(meta['animations'])},
        'anchors': dict({k: [round(v[0], 2), round(v[1], 2)] for k, v in meta['anchors'].items() if isinstance(v, list)}, top=top),
        'ground': {'y': ground + 1, 'frames': feet},
        'style': 'docs/art-style.md', 'generator': 'Tools/VfxLab/merchant.html',
    }
    return problems, Image.fromarray(sheet, 'RGBA'), Image.fromarray(light, 'RGBA'), data, fr


def png(image):
    buf = io.BytesIO(); image.save(buf, 'PNG', optimize=True); return buf.getvalue()


def same_pixels(path, data):
    return os.path.exists(path) and np.array_equal(np.asarray(Image.open(path).convert('RGBA')), np.asarray(Image.open(io.BytesIO(data)).convert('RGBA')))


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--check', action='store_true')
    ap.add_argument('--preview')
    ap.add_argument('--out', default=OUT)
    args = ap.parse_args()
    problems, sheet, light, data, fr = build()
    for p in problems:
        print('PROBLEM', p)
    files = {'merchant_96.png': png(sheet), 'merchant_halo_96.png': png(light),
             'merchant.json': (json.dumps(data, indent=2, ensure_ascii=False) + '\n').encode('utf-8')}
    if args.check:
        bad = [n for n, b in files.items() if not (same_pixels(os.path.join(args.out, n), b) if n.endswith('.png')
               else os.path.exists(os.path.join(args.out, n)) and open(os.path.join(args.out, n), 'rb').read() == b)]
        print('check:', 'OK' if not bad and not problems else 'differs: ' + ', '.join(bad))
        sys.exit(1 if bad or problems else 0)
    os.makedirs(args.out, exist_ok=True)
    for n, b in files.items():
        open(os.path.join(args.out, n), 'wb').write(b)
    print('wrote', ', '.join(files), 'to', args.out)
    if args.preview:
        os.makedirs(args.preview, exist_ok=True)
        cell = data['cell']; s = np.asarray(sheet); h = np.asarray(light)
        for name, a in data['animations'].items():
            ims = []
            for i in range(a['frames']):
                cut = s[a['row'] * cell:(a['row'] + 1) * cell, i * cell:(i + 1) * cell].astype(float) / 255
                glow = h[a['row'] * cell:(a['row'] + 1) * cell, i * cell:(i + 1) * cell, :3].astype(float) / 255 * HALO_GAIN
                bg = np.zeros((cell, cell, 3)); bg[:] = (28 / 255, 25 / 255, 50 / 255)
                img = np.clip(np.where(cut[..., 3:4] > 0, cut[..., :3], bg) + glow, 0, 1)
                ims.append(Image.fromarray((img * 255).astype(np.uint8)).resize((cell * 3, cell * 3), Image.NEAREST))
            ims[0].save(os.path.join(args.preview, f'merchant-{name}.gif' if len(ims) > 1 else f'merchant-{name}.png'),
                        **({'save_all': True, 'append_images': ims[1:], 'duration': a['durations_ms'], 'loop': 0} if len(ims) > 1 else {}))
    sys.exit(1 if problems else 0)


if __name__ == '__main__':
    main()
