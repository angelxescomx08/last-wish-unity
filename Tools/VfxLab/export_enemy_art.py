#!/usr/bin/env python3
"""
Exports the enemies drawn by code in Tools/VfxLab/enemies/<id>.html to the game. Each page's <script id="<id>-art">
draws every frame as ONE complete figure with its own written pose (docs/art-style.md section 3.1, the mandatory method
for characters and enemies). This script runs that same script with Node, so the page you open in a browser and the game
sheet always match.

Writes Assets/Resources/Game/Enemies/ (the format Presentation/Core/Art.cs EnemySheet already reads):
  <id>_sheet.png   native cells (drawn x2 in combat), already lit: paint + emissive x 0.6, hot cores pushed to white
  <id>_halo.png    the emissive layer blurred (sigma 1.4 and 4.0 native px), same cells; added over the sprite (no bloom)
  <id>_sheet.json  cells, anchor (the shadow line under the figure), rows, 12 fps durations, strike events, halo, the
                   top of idle frame 0 (name and intent go above it) and its bounds (the combat-log portrait)

Validation (exit 1 when broken): nothing touches the cell border or goes below the shadow line; every frame is ONE
4-connected mass and nothing came loose from the figure (the dissolving end of the death excepted); attack, hurt and
cast end on idle frame 0 pixel for pixel; the death ends on an empty frame.

Usage (needs Node.js on PATH):
  python Tools/VfxLab/export_enemy_art.py                 # every page in Tools/VfxLab/enemies
  python Tools/VfxLab/export_enemy_art.py --enemy wraith  # one
  python Tools/VfxLab/export_enemy_art.py --check         # compare with the files on disk
  python Tools/VfxLab/export_enemy_art.py --preview DIR   # GIF and strip per animation
  python Tools/VfxLab/export_enemy_art.py --styles        # cue points for EnemyAnimator.Styles (screen px from the anchor)
"""
import argparse
import glob
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
PAGES = os.path.join(HERE, 'enemies')
OUT = os.path.join(ROOT, 'Assets', 'Resources', 'Game', 'Enemies')
EMISSIVE_GAIN, HALO_GAIN, SCALE = 0.6, 0.55, 2
MAX_LOST = 6  # specks the connectivity pass may drop from a frame; more is a lost piece

_DUMP = r"""
const A = require(process.argv[2]); const fs = require('fs');
const bufs = []; const meta = { animations: [], cw: A.CW || A.CELL, ch: A.CH || A.CELL, ground: A.GROUND, fps: A.FPS, root: A.ROOT_X,
  events: A.EVENTS || {}, extra: A.META || {}, points: A.points ? A.points() : null, lost: {}, dissolving: {} };
for (const [name, n, loop] of A.ANIMATIONS) {
  meta.animations.push([name, n, loop]);
  for (let f = 0; f < n; f++) {
    const [c, g] = A.FRAME[name](f);
    bufs.push(Buffer.from(c.px.buffer), Buffer.from(g.px.buffer), Buffer.from(A.silhouetteOf(name, f).buffer));
    (meta.lost[name] = meta.lost[name] || []).push(A.lostOf ? A.lostOf(name, f) : 0);
    (meta.dissolving[name] = meta.dissolving[name] || []).push(A.dissolving ? !!A.dissolving(name, f) : false);
  }
}
fs.writeFileSync(process.argv[3] + '.bin', Buffer.concat(bufs)); fs.writeFileSync(process.argv[3] + '.json', JSON.stringify(meta));
"""


def pages(only=None):
    found = sorted(os.path.splitext(os.path.basename(p))[0] for p in glob.glob(os.path.join(PAGES, '*.html')))
    if only:
        missing = [e for e in only if e not in found]
        if missing:
            raise SystemExit('no page for: ' + ', '.join(missing))
        return only
    return found


def script(enemy):
    html = open(os.path.join(PAGES, enemy + '.html'), encoding='utf-8').read()
    m = re.search(r'<script id="%s-art">(.*?)</script>' % re.escape(enemy), html, re.S)
    if not m:
        raise RuntimeError('%s.html has no <script id="%s-art"> block' % (enemy, enemy))
    return m.group(1)


def frames(enemy):
    node = shutil.which('node') or shutil.which('nodejs')
    if not node:
        raise RuntimeError('Node.js is needed to draw the enemies (Tools/VfxLab/enemies/*.html)')
    with tempfile.TemporaryDirectory() as tmp:
        art, dump, out = os.path.join(tmp, 'art.js'), os.path.join(tmp, 'dump.js'), os.path.join(tmp, 'frames')
        open(art, 'w', encoding='utf-8').write(script(enemy))
        open(dump, 'w', encoding='utf-8').write(_DUMP)
        subprocess.run([node, dump, art, out], check=True)
        meta = json.load(open(out + '.json', encoding='utf-8'))
        raw = open(out + '.bin', 'rb').read()
    cw, ch = meta['cw'], meta['ch']; n_px = cw * ch; off = 0; result = {}
    for name, n, _ in meta['animations']:
        fr = []
        for _ in range(n):
            c = np.frombuffer(raw, np.float32, n_px * 4, off).reshape(ch, cw, 4); off += n_px * 16
            g = np.frombuffer(raw, np.float32, n_px * 4, off).reshape(ch, cw, 4); off += n_px * 16
            sil = np.frombuffer(raw, np.uint8, n_px, off).reshape(ch, cw).astype(bool); off += n_px
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


def build(enemy):
    meta, fr = frames(enemy)
    cw, ch, ground = meta['cw'], meta['ch'], meta['ground']
    problems = []
    cols = max(n for _, n, _ in meta['animations']); rows = len(meta['animations'])
    sheet = np.zeros((rows * ch, cols * cw, 4), np.uint8); light = np.zeros_like(sheet); light[..., 3] = 255
    for r, (name, n, _) in enumerate(meta['animations']):
        for i, (c, g, sil) in enumerate(fr[name]):
            rgba = lit(c, g)
            a = rgba[..., 3] > 0
            last_death = name == 'death' and i == n - 1
            if last_death:
                if a.any():
                    problems.append(f'{name} {i}: the death must end on an empty frame')
            elif not a.any():
                problems.append(f'{name} {i}: empty')
            else:
                ys, xs = np.where(a)
                if xs.min() == 0 or ys.min() == 0 or xs.max() == cw - 1 or ys.max() == ch - 1:
                    problems.append(f'{name} {i}: touches the cell border')
                fy = np.where(sil)[0]
                if fy.size and fy.max() >= ground:
                    problems.append(f'{name} {i}: the figure goes below the shadow line (row {int(fy.max())})')
                if not meta['dissolving'][name][i]:
                    lost = meta['lost'][name][i]
                    if lost > MAX_LOST:
                        problems.append(f'{name} {i}: {lost} pixels came loose from the figure and were dropped')
                    k = components(sil)
                    if k != 1:
                        problems.append(f'{name} {i}: the figure is {k} separate regions, not one mass')
            sheet[r * ch:(r + 1) * ch, i * cw:(i + 1) * cw] = rgba
            light[r * ch:(r + 1) * ch, i * cw:(i + 1) * cw] = halo(g)
    rest = sheet[:ch, :cw]
    for r, (name, n, loop) in enumerate(meta['animations']):
        if name in ('idle', 'death') or loop or name in meta['extra'].get('terminal', []):
            continue
        if not np.array_equal(sheet[r * ch:(r + 1) * ch, (n - 1) * cw:n * cw], rest):
            problems.append(f'{name} {n - 1}: does not end on idle frame 0, combat would jump back to the idle')
    alpha = np.asarray(sheet[:ch, :cw, 3]) > 0
    ys, xs = np.where(alpha)
    box = [int(xs.min()), int(ys.min()), int(xs.max()) + 1, int(ys.max()) + 1]
    fps = meta['fps']
    data = {
        'cell_w': cw, 'cell_h': ch, 'columns': cols, 'anchor': [meta['root'], ground], 'scale': SCALE,
        'sheet': f'{enemy}_sheet.png', 'halo': {'sheet': f'{enemy}_halo.png', 'gain': HALO_GAIN},
        'animations': {name: {'row': r, 'frames': n,
                              'durations_ms': [round((i + 1) * 1000 / fps) - round(i * 1000 / fps) for i in range(n)],
                              'loop': loop}
                       for r, (name, n, loop) in enumerate(meta['animations'])},
    }
    if meta['events']:
        data['events'] = {k: {'strikes': [int(f) for f in v['strikes']]} for k, v in meta['events'].items()}
    data.update(meta['extra'])
    data['top_native'] = box[1]
    data['idle_bbox_native'] = box
    data['style'] = 'docs/art-style.md'
    data['generator'] = f'Tools/VfxLab/enemies/{enemy}.html'
    return problems, Image.fromarray(sheet, 'RGBA'), Image.fromarray(light, 'RGBA'), data, meta


def png(image):
    buf = io.BytesIO(); image.save(buf, 'PNG', optimize=True); return buf.getvalue()


def same_pixels(path, data):
    return os.path.exists(path) and np.array_equal(np.asarray(Image.open(path).convert('RGBA')), np.asarray(Image.open(io.BytesIO(data)).convert('RGBA')))


def styles(enemy, meta):
    """EnemyFxStyle cue points: native coordinates of the page -> combat px from the anchor."""
    p = meta['points']
    if not p:
        return None
    rx, gy = meta['root'], meta['ground']
    pt = lambda v: f'({round((v[0] - rx) * SCALE)}, {round((v[1] - gy) * SCALE)})'
    return f'["{enemy}"]: Chest = {pt(p["chest"])}, Eyes = {pt(p["eyes"])}, Body = {pt(p["body"])}, Claw = {pt(p["claw"])}'


def preview(enemy, sheet, light, data, out):
    os.makedirs(out, exist_ok=True)
    cw, ch = data['cell_w'], data['cell_h']; s = np.asarray(sheet); h = np.asarray(light)
    for name, a in data['animations'].items():
        ims = []
        for i in range(a['frames']):
            cut = s[a['row'] * ch:(a['row'] + 1) * ch, i * cw:(i + 1) * cw].astype(float) / 255
            glow = h[a['row'] * ch:(a['row'] + 1) * ch, i * cw:(i + 1) * cw, :3].astype(float) / 255 * HALO_GAIN
            bg = np.zeros((ch, cw, 3)); bg[:] = (28 / 255, 25 / 255, 50 / 255); bg[data['anchor'][1]:] = (18 / 255, 15 / 255, 34 / 255)
            img = np.clip(np.where(cut[..., 3:4] > 0, cut[..., :3], bg) + glow, 0, 1)
            ims.append(Image.fromarray((img * 255).astype(np.uint8)).resize((cw * 3, ch * 3), Image.NEAREST))
        strip = Image.new('RGB', (cw * 3 * len(ims), ch * 3))
        for k, im in enumerate(ims):
            strip.paste(im, (k * cw * 3, 0))
        strip.save(os.path.join(out, f'{enemy}-{name}-strip.png'))
        ims[0].save(os.path.join(out, f'{enemy}-{name}.gif'), save_all=True, append_images=ims[1:], duration=a['durations_ms'],
                    loop=0)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--enemy', action='append', help='sheet id (the page name); repeatable; default: every page')
    ap.add_argument('--check', action='store_true')
    ap.add_argument('--preview')
    ap.add_argument('--styles', action='store_true')
    ap.add_argument('--out', default=OUT)
    args = ap.parse_args()
    failed = False
    for enemy in pages(args.enemy):
        problems, sheet, light, data, meta = build(enemy)
        for p in problems:
            print(f'PROBLEM {enemy}: {p}')
        failed |= bool(problems)
        files = {f'{enemy}_sheet.png': png(sheet), f'{enemy}_halo.png': png(light),
                 f'{enemy}_sheet.json': (json.dumps(data, indent=1, ensure_ascii=False) + '\n').encode('utf-8')}
        if args.styles:
            print(styles(enemy, meta))
        if args.check:
            bad = [n for n, b in files.items() if not (same_pixels(os.path.join(args.out, n), b) if n.endswith('.png')
                   else os.path.exists(os.path.join(args.out, n)) and open(os.path.join(args.out, n), 'rb').read() == b)]
            print(f'check {enemy}:', 'OK' if not bad and not problems else 'differs: ' + ', '.join(bad))
            failed |= bool(bad)
            continue
        os.makedirs(args.out, exist_ok=True)
        for n, b in files.items():
            open(os.path.join(args.out, n), 'wb').write(b)
        print(f'wrote {", ".join(files)} to {args.out}')
        if args.preview:
            preview(enemy, sheet, light, data, args.preview)
    sys.exit(1 if failed else 0)


if __name__ == '__main__':
    main()
