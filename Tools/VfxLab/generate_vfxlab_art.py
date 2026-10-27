#!/usr/bin/env python3
"""VFX Lab art generator (Last Wish).

Writes the pixel art used by the VFX Lab scene to Assets/VfxLab/Art:
  vfx_atlas.png          particle atlas, 8x8 tiles of 16 px (white shapes, tinted by the particles)
  vfx_trail.png          ribbon texture for TrailRenderer / particle trails
  vfx_light.png          posterized light pool for the fake 2D light (multiply-add blend)
  card_outline.png       hover outline of a card (additive)
  card_<id>_base.png     card frame + baked text (104x144) and card_<id>_base_glow.png (emissive gems)
  card_<id>_art.png      24-frame illustration sheet (88x64 per frame, 12 fps) and card_<id>_art_glow.png

Usage:
  python Tools/VfxLab/generate_vfxlab_art.py                       # write into the project
  python Tools/VfxLab/generate_vfxlab_art.py --preview TestResults/vfxlab-art   # also contact sheets + GIFs
Needs Python 3.10+, numpy and Pillow.
"""
import argparse
import os
import sys

import numpy as np
from PIL import Image, ImageFilter

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)

import card_frame  # noqa: E402
import rogue_art   # noqa: E402
import vfx_atlas   # noqa: E402
from pixelkit import BitmapFont, save_sheet  # noqa: E402

ROOT = os.path.abspath(os.path.join(HERE, '..', '..'))

CARDS = [
    # id, name, rules text lines ([d]..[/d] damage, [b]..[/b] block), mana, attack, block
    ('punalada', 'Puñalada', ['Inflige [d]6 de daño[/d].'], 1, 6, 0),
    ('esquiva', 'Esquiva', ['Gana [b]6 de escudo[/b].'], 1, 0, 6),
    ('finta', 'Finta', ['Inflige [d]4 de daño[/d].', 'Combo: gana', '[b]4 de escudo[/b].'], 1, 4, 4),
]


def load_fonts(root):
    d = os.path.join(root, 'Assets', 'Resources', 'Game', 'Fonts')
    f = lambda n: BitmapFont(os.path.join(d, n + '.png'), os.path.join(d, n + '.json'))
    return {'name': f('lw8'), 'number': f('lw8b'), 'text': f('tiny5')}


def preview_frame(base, glow, scale=4, gain=1.0):
    """Approximation of the Unity composite: base + emissive * 0.6 * gain, hot white, soft bloom."""
    hdr = base.px[..., :3] + np.clip(glow.px[..., :3], 0, 4) * 0.6 * gain
    up = np.repeat(np.repeat(hdr, scale, 0), scale, 1)
    out = np.clip(up, 0, 1) + np.clip(up.max(axis=2, keepdims=True) - 1, 0, None) * 0.5
    bright = np.clip(up - 0.85, 0, 1)
    im = Image.fromarray((bright * 255).astype(np.uint8))
    bloom = sum(np.array(im.filter(ImageFilter.GaussianBlur(r))).astype(float) / 255 * w for r, w in ((3, .6), (8, .6), (18, .5)))
    return Image.fromarray((np.clip(out + bloom * 0.9, 0, 1) * 255).astype(np.uint8))


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument('--root', default=ROOT, help='Unity project root (fonts are read from it)')
    ap.add_argument('--out', default=None, help='output folder (default: <root>/Assets/VfxLab/Art)')
    ap.add_argument('--preview', default=None, help='also write contact sheets and GIF previews here')
    args = ap.parse_args()
    out = args.out or os.path.join(args.root, 'Assets', 'VfxLab', 'Art')
    os.makedirs(out, exist_ok=True)
    fonts = load_fonts(args.root)

    vfx_atlas.build_atlas().save(os.path.join(out, 'vfx_atlas.png'), optimize=True)
    vfx_atlas.build_trail().save(os.path.join(out, 'vfx_trail.png'), optimize=True)
    vfx_atlas.build_light().save(os.path.join(out, 'vfx_light.png'), optimize=True)
    card_frame.outline_sprite().image().save(os.path.join(out, 'card_outline.png'), optimize=True)

    for cid, name, lines, mana, attack, block in CARDS:
        base, glow = card_frame.card_base(fonts, name, lines, mana, attack, block)
        base.image().save(os.path.join(out, f'card_{cid}_base.png'), optimize=True)
        glow.image(emissive=True).save(os.path.join(out, f'card_{cid}_base_glow.png'), optimize=True)
        frames, glows = rogue_art.render(cid)
        save_sheet(frames, os.path.join(out, f'card_{cid}_art.png'))
        save_sheet(glows, os.path.join(out, f'card_{cid}_art_glow.png'), emissive=True)
        if args.preview:
            os.makedirs(args.preview, exist_ok=True)
            imgs = []
            for b, g in zip(frames, glows):
                card = card_frame.Canvas(card_frame.W, card_frame.H)
                card.px[:] = base.px
                cg = card_frame.Canvas(card_frame.W, card_frame.H)
                cg.px[:] = glow.px
                card.px[card_frame.ART_Y:card_frame.ART_Y + rogue_art.AH, card_frame.ART_X:card_frame.ART_X + rogue_art.AW] = b.px
                cg.px[card_frame.ART_Y:card_frame.ART_Y + rogue_art.AH, card_frame.ART_X:card_frame.ART_X + rogue_art.AW, :3] += g.px[..., :3]
                imgs.append(preview_frame(card, cg, 3))
            imgs[0].save(os.path.join(args.preview, f'{cid}.gif'), save_all=True, append_images=imgs[1:], duration=83, loop=0)
            imgs[15].save(os.path.join(args.preview, f'{cid}_f15.png'))
            print('preview', cid)
    print('VfxLab art written to', out)


if __name__ == '__main__':
    main()
