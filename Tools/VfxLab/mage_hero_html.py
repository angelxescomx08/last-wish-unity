"""El Mago's hero sheet for export_hero_art.py. The drawing lives in Tools/VfxLab/mage_hero.html: its
<script id="mage-art"> draws every frame by code as ONE complete figure (docs/art-style.md section 3.1). This adapter runs
that same script with Node and hands the frames over, so the page you open in a browser and the game sheets always match.

Needs Node.js (node on PATH).
"""
import json
import os
import re
import shutil
import subprocess
import tempfile

import numpy as np

from pixelkit import Canvas

HERE = os.path.dirname(os.path.abspath(__file__))
HTML = os.path.join(HERE, 'mage_hero.html')
GENERATOR = 'Tools/VfxLab/mage_hero.html'
CELL, GROUND, FPS = 96, 93, 12
BORDER_OK = {('attack', 4), ('attack', 5)}        # the bolt leaves the cell

_DUMP = r"""
const A = require(process.argv[2]); const fs = require('fs');
const bufs = []; const meta = { animations: [], strike: A.strikePoint(), cast: A.castPoint(),
  strike_frame: A.STRIKE_FRAME, cast_frame: A.CAST_FRAME, cell: A.CELL, ground: A.GROUND, fps: A.FPS };
for (const [name, n, loop] of A.ANIMATIONS) {
  meta.animations.push([name, n, loop]);
  for (let f = 0; f < n; f++) {
    const [c, g] = A.FRAME[name](f);
    bufs.push(Buffer.from(c.px.buffer), Buffer.from(g.px.buffer), Buffer.from(A.silhouetteOf(name, f).buffer));
  }
}
fs.writeFileSync(process.argv[3] + '.bin', Buffer.concat(bufs)); fs.writeFileSync(process.argv[3] + '.json', JSON.stringify(meta));
"""
_cache = None


def script():
    """The generator script embedded in the page."""
    html = open(HTML, encoding='utf-8').read()
    m = re.search(r'<script id="mage-art">(.*?)</script>', html, re.S)
    if not m:
        raise RuntimeError('mage_hero.html has no <script id="mage-art"> block')
    return m.group(1)


def _frames():
    global _cache
    if _cache is not None:
        return _cache
    node = shutil.which('node') or shutil.which('nodejs')
    if not node:
        raise RuntimeError('Node.js is needed to draw El Mago (Tools/VfxLab/mage_hero.html): install it or put node on PATH')
    with tempfile.TemporaryDirectory() as tmp:
        art, dump, out = os.path.join(tmp, 'mage_art.js'), os.path.join(tmp, 'dump.js'), os.path.join(tmp, 'frames')
        with open(art, 'w', encoding='utf-8') as f:
            f.write(script())
        with open(dump, 'w', encoding='utf-8') as f:
            f.write(_DUMP)
        subprocess.run([node, dump, art, out], check=True)
        meta = json.load(open(out + '.json', encoding='utf-8'))
        raw = open(out + '.bin', 'rb').read()
    n_px = CELL * CELL
    off, frames = 0, {}
    for name, n, _ in meta['animations']:
        fr = []
        for _f in range(n):
            c, g = Canvas(CELL, CELL), Canvas(CELL, CELL)
            c.px[:] = np.frombuffer(raw, np.float32, n_px * 4, off).reshape(CELL, CELL, 4)
            off += n_px * 16
            g.px[:] = np.frombuffer(raw, np.float32, n_px * 4, off).reshape(CELL, CELL, 4)
            off += n_px * 16
            sil = np.frombuffer(raw, np.uint8, n_px, off).reshape(CELL, CELL).astype(bool)
            off += n_px
            fr.append((c, g, sil))
        frames[name] = fr
    _cache = (meta, frames)
    return _cache


def _meta():
    return _frames()[0]


def __getattr__(name):            # ANIMATIONS, STRIKE_FRAME, CAST_FRAME come from the page itself
    meta = _meta()
    if name == 'ANIMATIONS':
        return [(a[0], a[1], a[2]) for a in meta['animations']]
    if name == 'STRIKE_FRAME':
        return meta['strike_frame']
    if name == 'CAST_FRAME':
        return meta['cast_frame']
    raise AttributeError(name)


def durations_ms(n):
    return [round((i + 1) * 1000 / FPS) - round(i * 1000 / FPS) for i in range(n)]


def render(name):
    fr = _frames()[1][name]
    return [c for c, _, _ in fr], [g for _, g, _ in fr]


def attack_strike_point():
    return tuple(_meta()['strike'])


def cast_point():
    return tuple(_meta()['cast'])


def mass(name, f):
    """The whole figure of frame f (body, sleeve, hand and staff): the exporter checks it is one 4-connected region."""
    return _frames()[1][name][f][2]
