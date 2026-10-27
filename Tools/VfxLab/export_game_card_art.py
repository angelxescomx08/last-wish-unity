#!/usr/bin/env python3
"""Exports the VFX Lab rogue illustrations to the game (Last Wish, Unity port).

The game draws cards on its own canvas without HDR or bloom, so each illustration is baked into two sheets:
  Assets/Resources/Game/Cards/Anim/<card id>.png       colour frames, already lit (base + emissive, hot cores to white), x2
  Assets/Resources/Game/Cards/Anim/<card id>_halo.png  soft light around the magic (blurred emissive), native size, drawn
                                                       additively by the game (its stand-in for bloom)
plus Assets/Resources/Game/Cards/Anim/anim.json with the grid, timing, the frame used by static faces and the colours of
the live twinkles. Frames sit in a grid with an edge-extended gutter, so bilinear sampling never bleeds between frames.

Usage:
  python Tools/VfxLab/export_game_card_art.py                  # write into the project
  python Tools/VfxLab/export_game_card_art.py --check          # exit 1 if the files on disk differ from a fresh export
  python Tools/VfxLab/export_game_card_art.py --preview DIR    # also a GIF per card (art window only)
Needs Python 3.10+, numpy and Pillow.
"""
import argparse
import io
import json
import os
import sys

import numpy as np
from PIL import Image, ImageFilter

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)

import rogue_art  # noqa: E402

ROOT = os.path.abspath(os.path.join(HERE, '..', '..'))
FPS = 12
SCALE = 2          # colour frames are stored x2 (176x128), the size of the other card illustrations
PAD = 2            # edge-extended gutter around every frame (texels of the stored sheet)
COLUMNS = 6
EMISSIVE_GAIN = 0.6

# lab id -> game card id, the frame shown on static faces (deal flight, pack reveal…), twinkle and mote colours
CARDS = [
    ('punalada', 'punalada_base', 0, (150, 240, 255), (90, 200, 255)),
    ('esquiva', 'esquiva_base', 0, (200, 170, 255), (120, 220, 255)),
    ('finta', 'finta_base', 0, (150, 240, 255), (190, 140, 255)),
]

def lit(base, glow):
    """What the lab composite shows without bloom: base + emissive, values over 1 pushed towards white."""
    hdr = base.px[..., :3] + np.clip(glow.px[..., :3], 0, 4) * EMISSIVE_GAIN
    hot = np.clip(hdr.max(axis=2, keepdims=True) - 1, 0, None) * 0.5
    return np.clip(np.clip(hdr, 0, 1) + hot, 0, 1)


def halo(glow):
    """Soft light around the magic: the emissive layer blurred at two radii (drawn additively, no bloom in the game)."""
    e = np.clip(glow.px[..., :3], 0, 2.5) / 2.5
    im = Image.fromarray((e * 255).astype(np.uint8))
    near = np.asarray(im.filter(ImageFilter.GaussianBlur(1.4)), float) / 255
    far = np.asarray(im.filter(ImageFilter.GaussianBlur(4.0)), float) / 255
    return np.clip(near * 0.9 + far * 1.1, 0, 1)


def sheet(frames, scale, pad):
    """Frames (h, w, 3 floats) in a COLUMNS-wide grid; each cell = frame x scale with an edge-extended gutter."""
    up = [np.repeat(np.repeat(f, scale, 0), scale, 1) for f in frames]
    fh, fw = up[0].shape[:2]
    ch, cw = fh + 2 * pad, fw + 2 * pad
    rows = (len(up) + COLUMNS - 1) // COLUMNS
    out = np.zeros((rows * ch, COLUMNS * cw, 4), np.uint8)
    for i, f in enumerate(up):
        cell = np.pad(f, ((pad, pad), (pad, pad), (0, 0)), mode='edge')
        r, c = divmod(i, COLUMNS)
        out[r * ch:(r + 1) * ch, c * cw:(c + 1) * cw, :3] = (cell * 255 + 0.5).astype(np.uint8)
        out[r * ch:(r + 1) * ch, c * cw:(c + 1) * cw, 3] = 255
    return Image.fromarray(out, 'RGBA'), (cw, ch), (fw, fh)


def png_bytes(image):
    buf = io.BytesIO()
    image.save(buf, 'PNG', optimize=True)
    return buf.getvalue()


def build():
    """{relative path: bytes} of every exported file."""
    files = {}
    meta = {'fps': FPS, 'columns': COLUMNS, 'pad': PAD, 'cards': {}}
    for lab_id, card_id, still, spark, mote in CARDS:
        frames, glows = rogue_art.render(lab_id)
        colour, cell, size = sheet([lit(b, g) for b, g in zip(frames, glows)], SCALE, PAD)
        light, hcell, hsize = sheet([halo(g) for g in glows], 1, PAD)
        files[card_id + '.png'] = png_bytes(colour)
        files[card_id + '_halo.png'] = png_bytes(light)
        meta['cell'], meta['frame'] = list(cell), list(size)
        meta['halo_cell'], meta['halo_frame'] = list(hcell), list(hsize)
        meta['cards'][card_id] = {'frames': len(frames), 'still': still, 'spark': list(spark), 'mote': list(mote)}
    files['anim.json'] = (json.dumps(meta, indent=2, ensure_ascii=False) + '\n').encode('utf-8')
    return files


def write_preview(out_dir):
    os.makedirs(out_dir, exist_ok=True)
    for lab_id, card_id, *_ in CARDS:
        frames, glows = rogue_art.render(lab_id)
        imgs = []
        for b, g in zip(frames, glows):
            c = lit(b, g)
            hl = np.asarray(Image.fromarray((halo(g) * 255).astype(np.uint8)).resize((c.shape[1], c.shape[0]), Image.BILINEAR), float) / 255
            px = np.clip(c + hl * 0.55, 0, 1)
            imgs.append(Image.fromarray((px * 255).astype(np.uint8)).resize((c.shape[1] * 4, c.shape[0] * 4), Image.NEAREST))
        imgs[0].save(os.path.join(out_dir, card_id + '.gif'), save_all=True, append_images=imgs[1:], duration=1000 // FPS, loop=0)


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument('--root', default=ROOT, help='Unity project root')
    ap.add_argument('--check', action='store_true', help='compare with the files on disk instead of writing')
    ap.add_argument('--preview', default=None, help='also write GIF previews here')
    args = ap.parse_args()
    out = os.path.join(args.root, 'Assets', 'Resources', 'Game', 'Cards', 'Anim')
    files = build()
    if args.check:
        bad = []
        for name, data in files.items():
            path = os.path.join(out, name)
            if not os.path.exists(path):
                bad.append(name + ' (missing)')
            elif name.endswith('.png'):
                if not np.array_equal(np.asarray(Image.open(path).convert('RGBA')), np.asarray(Image.open(io.BytesIO(data)).convert('RGBA'))):
                    bad.append(name)
            elif open(path, 'rb').read().replace(b'\r\n', b'\n') != data:
                bad.append(name)
        print('OK' if not bad else 'Differs: ' + ', '.join(bad))
        return 1 if bad else 0
    os.makedirs(out, exist_ok=True)
    for name, data in files.items():
        with open(os.path.join(out, name), 'wb') as f:
            f.write(data)
    if args.preview:
        write_preview(args.preview)
    print('Card animations written to', out)
    return 0


if __name__ == '__main__':
    sys.exit(main())
