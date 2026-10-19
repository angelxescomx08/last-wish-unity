"""Card frame derivatives baked with the original game's own functions:

* frames reduced to half size (area filter; the largest card on screen is ~280 px wide),
* golden / diamond frames (chroma_fx.gild_frame: multiply then add, alpha kept),
* per rarity: the card body silhouette mask (alpha) and its white silhouette aura
  (chroma_fx.silhouette_aura with white at full level), tinted at run time.

    python Tools/ExportCardFx.py --source-dir RUTA_PYTHON --output-dir Assets/Resources/Game/Cards
"""
import argparse, json, os, sys
from pathlib import Path

def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--source-dir", required=True)
    parser.add_argument("--output-dir", default="Assets/Resources/Game/Cards")
    args = parser.parse_args()
    os.environ.setdefault("SDL_VIDEODRIVER", "dummy"); os.environ.setdefault("SDL_AUDIODRIVER", "dummy")
    src = Path(args.source_dir).resolve(); sys.path.insert(0, str(src)); os.chdir(src)
    import pygame
    pygame.init(); pygame.display.set_mode((64, 64))
    from src.domain.chroma import Chroma
    from src.domain.card_pool import _POOL, PackTheme
    from src.infrastructure.fonts import FontRegistry
    from src.infrastructure.card_assets import card_layout, _load
    from src.presentation.fx import chroma_fx
    from src.presentation.ui.card_widget import render_card_surface, CARD_W, CARD_H
    out = Path(args.output_dir); (out / "Frames").mkdir(parents=True, exist_ok=True); (out / "Fx").mkdir(parents=True, exist_ok=True)
    layout = card_layout()
    for rarity, info in layout["frames"].items():
        frame = _load(src / "assets" / "cards-v2" / info["file"])
        w, h = frame.get_width() // 2, frame.get_height() // 2
        half = pygame.transform.smoothscale(frame, (w, h))
        name = Path(info["file"]).stem
        pygame.image.save(half, str(out / "Frames" / f"{name}.png"))
        for chroma in (Chroma.GOLDEN, Chroma.DIAMOND):
            pygame.image.save(chroma_fx.gild_frame(half, chroma), str(out / "Frames" / f"{name}_{chroma.value}.png"))
    fonts = FontRegistry()
    seen = set()
    for theme in PackTheme:
        for f in _POOL[theme]:
            card = f()
            r = card.rarity.name
            if r in seen:
                continue
            seen.add(r)
            body = render_card_surface(card, fonts, w=CARD_W, h=CARD_H)
            mask = pygame.Surface(body.get_size(), pygame.SRCALPHA)
            mask.fill((255, 255, 255, 0))
            mask.blit(body, (0, 0))
            mask.fill((255, 255, 255, 0), special_flags=pygame.BLEND_RGBA_MAX)
            pygame.image.save(mask, str(out / "Fx" / f"mask_{r.lower()}.png"))
            aura, pad = chroma_fx.silhouette_aura(body, (255, 255, 255), 4, ("export", r))
            pygame.image.save(aura, str(out / "Fx" / f"aura_{r.lower()}.png"))
    print("exported", sorted(seen))

if __name__ == "__main__":
    main()
