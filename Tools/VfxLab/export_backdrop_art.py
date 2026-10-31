"""Export the floor-1 combat backdrop (La Cripta) drawn by code in Tools/VfxLab/floor1_backdrop.html.

The page's <script id="crypt-art"> draws everything (docs/art-style.md): this script runs that same code with Node and
writes what the game loads, so the page you open in a browser and the game always match.

    python Tools/VfxLab/export_backdrop_art.py                 # writes Assets/Resources/Game/Dungeon/Floor1/
    python Tools/VfxLab/export_backdrop_art.py --check         # fails if the files differ from the drawing
    python Tools/VfxLab/export_backdrop_art.py --preview DIR   # also a still of the whole backdrop (DIR/floor1.png)

Writes room.png (640x360, the lit room with the window hole), sky.png (what shows through the window), halo.png
(320x180, the emissive light blurred, added at x4), atlas.png (flames, glows, fog, clouds, bats, ripple) and
backdrop.json (where everything goes, what moves and the particle emitters). Needs Node.js (node on PATH).
"""
import argparse
import json
import os
import re
import shutil
import subprocess
import sys
import tempfile
import uuid

import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, '..', '..'))
HTML = os.path.join(HERE, 'floor1_backdrop.html')
OUT = os.path.join(ROOT, 'Assets', 'Resources', 'Game', 'Dungeon', 'Floor1')
META_PNG = os.path.join(ROOT, 'Assets', 'Resources', 'Game', 'Dungeon', 'room_combat.png.meta')
META_JSON = os.path.join(ROOT, 'Assets', 'Resources', 'Game', 'Dungeon', 'dungeon.json.meta')
IMAGES = ('room', 'sky', 'halo', 'atlas')

_DUMP = r"""
require(process.argv[2]); const fs = require('fs');
const S = globalThis.CryptArt.exportSet(); const sizes = {};
for (const [name, img] of Object.entries(S.images)) {
  const b = Buffer.alloc(img.w * img.h * 4);
  for (let i = 0; i < b.length; i++) b[i] = Math.max(0, Math.min(255, Math.round(img.px[i] * 255)));
  fs.writeFileSync(process.argv[3] + '/' + name + '.rgba', b); sizes[name] = [img.w, img.h];
}
fs.writeFileSync(process.argv[3] + '/set.json', JSON.stringify({ sizes, json: S.json }));
"""


def script():
    html = open(HTML, encoding='utf-8').read()
    m = re.search(r'<script id="crypt-art">(.*?)</script>', html, re.S)
    if not m:
        raise RuntimeError('floor1_backdrop.html has no <script id="crypt-art"> block')
    return m.group(1)


def draw():
    """(images {name: uint8 HxWx4}, backdrop json) from the page's generator."""
    node = shutil.which('node') or shutil.which('nodejs')
    if not node:
        raise RuntimeError('Node.js is needed to draw the crypt (Tools/VfxLab/floor1_backdrop.html): install it or put node on PATH')
    with tempfile.TemporaryDirectory() as tmp:
        art, dump = os.path.join(tmp, 'crypt_art.js'), os.path.join(tmp, 'dump.js')
        with open(art, 'w', encoding='utf-8') as f:
            f.write(script())
        with open(dump, 'w', encoding='utf-8') as f:
            f.write(_DUMP)
        subprocess.run([node, dump, art, tmp], check=True)
        meta = json.load(open(os.path.join(tmp, 'set.json'), encoding='utf-8'))
        images = {}
        for name, (w, h) in meta['sizes'].items():
            raw = open(os.path.join(tmp, name + '.rgba'), 'rb').read()
            images[name] = np.frombuffer(raw, np.uint8).reshape(h, w, 4).copy()
    return images, meta['json']


def validate(images, js):
    W, H = js['size']
    assert images['room'].shape[:2] == (H, W), 'room is 640x360'
    assert images['halo'].shape[:2] == (H // 2, W // 2), 'halo is half resolution'
    sx, sy = js['sky']['at']
    sh, sw = images['sky'].shape[:2]
    hole = images['room'][sy:sy + sh, sx:sx + sw, 3] == 0
    assert hole.any() and (images['room'][..., 3] == 0).sum() == hole.sum(), 'the only hole in the room is the window, over the sky'
    assert (images['sky'][..., 3] == 255).all(), 'the sky is opaque'
    ah, aw = images['atlas'].shape[:2]
    rects = [r for k in js['flame_kinds'].values() for r in k['frames']] + [r for k in js['glow_kinds'].values() for r in k['levels']]
    rects += js['bats']['frames'] + js['ripple']['frames'] + [f['rect'] for f in js['fog']] + [js['clouds']['rect']]
    for x, y, w, h in rects:
        assert x >= 0 and y >= 0 and x + w <= aw and y + h <= ah, 'atlas rect inside the atlas'
    for f in js['flames']:
        assert f['kind'] in js['flame_kinds']
    for g in js['glows']:
        assert g['kind'] in js['glow_kinds']
    assert len(js['eyes']) % 2 == 0, 'eyes come in pairs (one skull each)'


def png_bytes_equal(path, arr):
    if not os.path.exists(path):
        return False
    have = np.array(Image.open(path).convert('RGBA'))
    return have.shape == arr.shape and np.array_equal(have, arr)


def ensure_meta(path, template):
    meta = path + '.meta'
    if os.path.exists(meta):
        return
    text = open(template, encoding='utf-8').read()
    text = re.sub(r'guid: [0-9a-f]{32}', 'guid: ' + uuid.uuid4().hex, text, count=1)
    with open(meta, 'w', encoding='utf-8', newline='\n') as f:
        f.write(text)


def ensure_folder_meta(folder):
    meta = folder.rstrip('/\\') + '.meta'
    if os.path.exists(meta):
        return
    with open(meta, 'w', encoding='utf-8', newline='\n') as f:
        f.write('fileFormatVersion: 2\nguid: %s\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n  userData: \n'
                '  assetBundleName: \n  assetBundleVariant: \n' % uuid.uuid4().hex)


def still(images, js, t_cloud=30):
    """One frame of the backdrop at x2 without the moving parts' timing (for --preview)."""
    W, H = js['size']
    img = np.zeros((H, W, 3), float)
    sx, sy = js['sky']['at']
    sky = images['sky'].astype(float) / 255
    sh, sw = sky.shape[:2]
    img[sy:sy + sh, sx:sx + sw] = sky[..., :3]
    room = images['room'].astype(float) / 255
    a = room[..., 3:4]
    img = img * (1 - a) + room[..., :3] * a
    halo = np.kron(images['halo'][..., :3].astype(float) / 255, np.ones((2, 2, 1)))
    img = np.clip(img + halo * js['halo']['gain'], 0, 1)
    atlas = images['atlas'].astype(float) / 255
    for f in js['flames']:
        x, y, w, h = js['flame_kinds'][f['kind']]['frames'][0]
        spr = atlas[y:y + h, x:x + w]
        m = spr[..., 3:4]
        img[f['y']:f['y'] + h, f['x']:f['x'] + w] = img[f['y']:f['y'] + h, f['x']:f['x'] + w] * (1 - m) + spr[..., :3] * m
    out = (img * 255).astype(np.uint8)
    return Image.fromarray(out).resize((W * 2, H * 2), Image.NEAREST)


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument('--check', action='store_true', help='compare with the files in Assets and fail if they differ')
    ap.add_argument('--preview', metavar='DIR', help='also write a still of the backdrop to DIR/floor1.png')
    args = ap.parse_args()
    images, js = draw()
    validate(images, js)
    text = json.dumps(js, ensure_ascii=False, indent=1) + '\n'
    if args.check:
        bad = [n for n in IMAGES if not png_bytes_equal(os.path.join(OUT, n + '.png'), images[n])]
        jpath = os.path.join(OUT, 'backdrop.json')
        if not os.path.exists(jpath) or json.load(open(jpath, encoding='utf-8')) != js:
            bad.append('backdrop.json')
        if bad:
            print('Floor1 backdrop differs from the drawing: ' + ', '.join(bad))
            return 1
        print('Floor1 backdrop OK (room, sky, halo, atlas, backdrop.json match Tools/VfxLab/floor1_backdrop.html)')
        return 0
    os.makedirs(OUT, exist_ok=True)
    ensure_folder_meta(OUT)
    for n in IMAGES:
        path = os.path.join(OUT, n + '.png')
        if not png_bytes_equal(path, images[n]):
            Image.fromarray(images[n]).save(path, optimize=True)
        ensure_meta(path, META_PNG)
    jpath = os.path.join(OUT, 'backdrop.json')
    with open(jpath, 'w', encoding='utf-8', newline='\n') as f:
        f.write(text)
    ensure_meta(jpath, META_JSON)
    print('wrote', ', '.join(n + '.png' for n in IMAGES), 'and backdrop.json to', os.path.relpath(OUT, ROOT))
    if args.preview:
        os.makedirs(args.preview, exist_ok=True)
        still(images, js).save(os.path.join(args.preview, 'floor1.png'))
        print('preview', os.path.join(args.preview, 'floor1.png'))
    return 0


if __name__ == '__main__':
    sys.exit(main())
