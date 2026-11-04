#!/usr/bin/env python3
"""Exports the animated card illustrations to the game (Last Wish, Unity port): the rogue's basic cards (rogue_art.py,
the VFX Lab art) and all her loot cards (rogue_pool_art.py .. rogue_pool_art_6.py), the mage's (mage_art.py) and the
warrior's (warrior_art.py).

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

import mage_art     # noqa: E402
import rogue_art    # noqa: E402
import rogue_pool_art  # noqa: E402
import rogue_pool_art_2  # noqa: E402
import rogue_pool_art_3  # noqa: E402
import rogue_pool_art_4  # noqa: E402
import rogue_pool_art_5  # noqa: E402
import rogue_pool_art_6  # noqa: E402
import warrior_art  # noqa: E402

ROOT = os.path.abspath(os.path.join(HERE, '..', '..'))
FPS = 12
SCALE = 2          # colour frames are stored x2 (176x128), the size of the other card illustrations
PAD = 2            # edge-extended gutter around every frame (texels of the stored sheet)
COLUMNS = 6
EMISSIVE_GAIN = 0.6

# art module, its id -> game card id, the frame shown on static faces (deal flight, pack reveal…), twinkle and mote
# colours of the live particles
CARDS = [
    (rogue_art, 'punalada', 'punalada_base', 0, (150, 240, 255), (90, 200, 255)),
    (rogue_art, 'esquiva', 'esquiva_base', 0, (200, 170, 255), (120, 220, 255)),
    (rogue_art, 'finta', 'finta_base', 0, (150, 240, 255), (190, 140, 255)),
    (mage_art, 'proyectil', 'proyectil_base', 0, (150, 240, 255), (190, 140, 255)),
    (mage_art, 'egida', 'egida_base', 0, (150, 240, 255), (120, 220, 255)),
    (mage_art, 'chispa_ignea', 'chispa_ignea_base', 0, (255, 214, 150), (255, 140, 60)),
    (warrior_art, 'espadazo', 'espadazo_base', 0, (150, 240, 255), (244, 220, 156)),
    (warrior_art, 'bloqueo', 'bloqueo_base', 0, (255, 224, 176), (120, 220, 255)),
    (warrior_art, 'guardia_ferrea', 'guardia_ferrea_base', 0, (169, 200, 242), (255, 116, 100)),
    (rogue_pool_art, 'pinchazo', 'r_pinchazo', 0, (150, 240, 255), (205, 213, 236)),
    (rogue_pool_art, 'tajo_veloz', 'r_tajo_veloz', 0, (150, 240, 255), (75, 147, 255)),
    (rogue_pool_art, 'lanzar_la_daga', 'r_lanzar_la_daga', 0, (150, 240, 255), (199, 155, 255)),
    (rogue_pool_art, 'lluvia_de_dagas', 'r_lluvia_de_dagas', 0, (199, 155, 255), (150, 240, 255)),
    (rogue_pool_art, 'cuchillada_errante', 'r_cuchillada_errante', 0, (199, 155, 255), (150, 240, 255)),
    (rogue_pool_art, 'marcar_objetivo', 'r_marcar_objetivo', 0, (255, 116, 100), (199, 155, 255)),
    (rogue_pool_art, 'abrir_la_guardia', 'r_abrir_la_guardia', 0, (194, 228, 255), (242, 179, 94)),
    (rogue_pool_art, 'corte_y_guardia', 'r_corte_y_guardia', 0, (150, 240, 255), (124, 196, 255)),
    (rogue_pool_art, 'afilar', 'r_afilar', 0, (255, 224, 176), (150, 240, 255)),
    (rogue_pool_art, 'carterista', 'r_carterista', 0, (244, 220, 156), (199, 155, 255)),
    (rogue_pool_art_2, 'chatarra', 'r_chatarra', 0, (199, 155, 255), (75, 147, 255)),
    (rogue_pool_art_2, 'deshacerse', 'r_deshacerse', 0, (199, 155, 255), (124, 196, 255)),
    (rogue_pool_art_2, 'finta_doble', 'r_finta_doble', 0, (199, 155, 255), (150, 240, 255)),
    (rogue_pool_art_2, 'quiebro', 'r_quiebro', 0, (255, 224, 176), (124, 196, 255)),
    (rogue_pool_art_2, 'tirar_y_cortar', 'r_tirar_y_cortar', 0, (255, 208, 138), (150, 240, 255)),
    (rogue_pool_art_2, 'hoja_envenenada', 'r_hoja_envenenada', 0, (226, 255, 176), (143, 224, 74)),
    (rogue_pool_art_2, 'veneno', 'm_veneno', 0, (226, 255, 176), (143, 224, 74)),
    (rogue_pool_art_2, 'muro_de_humo', 'r_muro_de_humo', 0, (194, 228, 255), (138, 128, 158)),
    (rogue_pool_art_2, 'contraataque', 'r_contraataque', 0, (150, 240, 255), (255, 116, 100)),
    (rogue_pool_art_2, 'golpe_de_gracia', 'r_golpe_de_gracia', 0, (255, 116, 100), (75, 147, 255)),
    (rogue_pool_art_3, 'corte_rapido', 'a_corte_rapido', 0, (150, 240, 255), (176, 144, 76)),
    (rogue_pool_art_3, 'tajo', 'a_tajo', 0, (150, 240, 255), (255, 116, 100)),
    (rogue_pool_art_3, 'punalada_trapera', 'r_punalada_trapera', 0, (199, 155, 255), (255, 116, 100)),
    (rogue_pool_art_3, 'golpe_desesperado', 'r_golpe_desesperado', 0, (255, 116, 100), (255, 208, 138)),
    (rogue_pool_art_3, 'instinto', 'a_instinto', 0, (150, 240, 255), (244, 220, 156)),
    (rogue_pool_art_3, 'juego_de_manos', 'r_juego_de_manos', 0, (199, 155, 255), (244, 220, 156)),
    (rogue_pool_art_3, 'manto_raido', 'r_manto_raido', 0, (194, 228, 255), (240, 130, 42)),
    (rogue_pool_art_3, 'reflejos', 'r_reflejos', 0, (150, 240, 255), (194, 228, 255)),
    (rogue_pool_art_3, 'escudo_reactivo', 'e_escudo_reactivo', 0, (194, 228, 255), (75, 147, 255)),
    (rogue_pool_art_3, 'guardia_evasiva', 'r_guardia_evasiva', 0, (194, 228, 255), (150, 240, 255)),
    (rogue_pool_art_4, 'agilidad', 'e_agilidad', 0, (194, 228, 255), (199, 155, 255)),
    (rogue_pool_art_4, 'senuelo', 'r_senuelo', 0, (199, 155, 255), (255, 116, 100)),
    (rogue_pool_art_4, 'sombra_esquiva', 'r_sombra_esquiva', 0, (199, 155, 255), (242, 179, 94)),
    (rogue_pool_art_4, 'capa_de_sombras', 'r_capa_de_sombras', 0, (194, 228, 255), (199, 155, 255)),
    (rogue_pool_art_4, 'corte_afortunado', 'r_corte_afortunado', 0, (244, 220, 156), (143, 224, 74)),
    (rogue_pool_art_4, 'hoja_unica', 'r_hoja_unica', 0, (240, 224, 255), (140, 196, 230)),
    (rogue_pool_art_4, 'rafaga_de_cortes', 'r_rafaga_de_cortes', 0, (150, 240, 255), (244, 220, 156)),
    (rogue_pool_art_4, 'tirar_los_dados', 'r_tirar_los_dados', 16, (195, 231, 255), (143, 224, 74)),  # still: the 3
    (rogue_pool_art_4, 'torbellino', 'm_torbellino', 0, (150, 240, 255), (255, 116, 100)),
    (rogue_pool_art_4, 'dagas_ocultas', 'r_dagas_ocultas', 0, (199, 155, 255), (150, 240, 255)),
    (rogue_pool_art_5, 'paso_atras', 'r_paso_atras', 0, (194, 228, 255), (150, 240, 255)),
    (rogue_pool_art_5, 'rodar', 'r_rodar', 0, (199, 155, 255), (242, 179, 94)),
    (rogue_pool_art_5, 'rebuscar', 'r_rebuscar', 0, (244, 220, 156), (199, 155, 255)),
    (rogue_pool_art_5, 'vaciar_bolsillos', 'r_vaciar_bolsillos', 0, (199, 155, 255), (244, 220, 156)),
    (rogue_pool_art_5, 'abanico', 'r_abanico_de_cuchillas', 0, (150, 240, 255), (199, 155, 255)),
    (rogue_pool_art_5, 'astucia', 'r_astucia', 8, (240, 224, 255), (166, 155, 230)),        # still: the fox lit
    (rogue_pool_art_5, 'danza_de_sombras', 'r_danza_de_sombras', 0, (200, 255, 232), (199, 155, 255)),
    (rogue_pool_art_5, 'estilo_propio', 'r_estilo_propio', 0, (194, 228, 255), (244, 220, 156)),
    (rogue_pool_art_5, 'estocada_oportuna', 'r_estocada_oportuna', 0, (150, 240, 255), (60, 230, 170)),
    (rogue_pool_art_5, 'fortuna_audaz', 'r_fortuna_audaz', 0, (244, 220, 156), (79, 149, 230)),
    (rogue_pool_art_5, 'maestra_de_dagas', 'r_maestra_de_dagas', 0, (240, 224, 255), (150, 240, 255)),
    (rogue_pool_art_5, 'preparacion', 'r_preparacion', 0, (195, 231, 255), (242, 179, 94)),
    (rogue_pool_art_6, 'rapina', 'r_rapina', 0, (240, 224, 255), (255, 116, 100)),
    (rogue_pool_art_6, 'sombra_gemela', 'r_sombra_gemela', 0, (150, 240, 255), (60, 230, 170)),
    (rogue_pool_art_6, 'asesina', 'r_asesina', 0, (255, 116, 100), (199, 155, 255)),
    (rogue_pool_art_6, 'cadena_perfecta', 'r_cadena_perfecta', 0, (244, 220, 156), (150, 240, 255)),
    (rogue_pool_art_6, 'lluvia_de_golpes', 'ep_lluvia_de_golpes', 5, (150, 240, 255), (255, 116, 100)),  # still: the rain
    (rogue_pool_art_6, 'mazo_impecable', 'ep_mazo_impecable', 0, (244, 220, 156), (75, 147, 255)),
    (rogue_pool_art_6, 'mil_cortes', 'r_mil_cortes', 0, (255, 116, 100), (200, 58, 64)),
    (rogue_pool_art_6, 'nada_que_perder', 'r_nada_que_perder', 0, (255, 224, 176), (199, 155, 255)),
    (rogue_pool_art_6, 'ritmo_letal', 'r_ritmo_letal', 0, (244, 220, 156), (79, 149, 230)),
    (rogue_pool_art_6, 'tormenta_de_acero', 'r_tormenta_de_acero', 6, (150, 240, 255), (199, 155, 255)),  # still: rain
    (rogue_pool_art_6, 'tormenta_de_veneno', 'ep_tormenta_veneno', 12, (226, 255, 176), (143, 224, 74)),  # still: rain
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
    for module, art_id, card_id, still, spark, mote in CARDS:
        frames, glows = module.render(art_id)
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
    for module, art_id, card_id, *_ in CARDS:
        frames, glows = module.render(art_id)
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
