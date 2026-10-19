"""Bake the original game's pixel fonts into bitmap atlases (glyphs rendered by pygame/SDL_ttf,
exactly like the Python game). Unity draws text from these atlases at whole multiples of the
base size, which is pixel-identical to pygame at those sizes (verified for every glyph).

    python Tools/ExportPixelFonts.py --source-dir RUTA_PYTHON --output-dir Assets/Resources/Game/Fonts
"""
import argparse, json, os
from pathlib import Path

FONTS = [("LastWish8.ttf", 8, "lw8"), ("LastWish.ttf", 16, "lw16"), ("LastWish8-Bold.ttf", 8, "lw8b"),
         ("LastWish-Bold.ttf", 16, "lw16b"), ("Tiny5-Regular.ttf", 8, "tiny5")]

def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--source-dir", required=True)
    parser.add_argument("--output-dir", default="Assets/Resources/Game/Fonts")
    args = parser.parse_args()
    os.environ.setdefault("SDL_VIDEODRIVER", "dummy")
    import pygame
    from fontTools.ttLib import TTFont
    pygame.init()
    out = Path(args.output_dir); out.mkdir(parents=True, exist_ok=True)
    for file, size, key in FONTS:
        path = Path(args.source_dir) / "assets" / "fonts" / file
        cmap = TTFont(str(path)).getBestCmap()
        font = pygame.font.Font(str(path), size)
        chars = sorted(c for c in cmap if c >= 32 and c != 0xFFFF)
        glyphs, surfaces = [], []
        for code in chars:
            ch = chr(code)
            w = font.size(ch)[0]
            surf = font.render(ch, False, (255, 255, 255))
            glyphs.append((code, w, surf))
        height = font.get_height()
        cols = 32
        cell_w = max(max(1, s.get_width()) for _, _, s in glyphs) + 1
        rows = (len(glyphs) + cols - 1) // cols
        atlas = pygame.Surface((cols * cell_w, rows * (height + 1)), pygame.SRCALPHA)
        meta = {"base": size, "height": height, "linesizes": [pygame.font.Font(str(path), size * k).get_linesize() for k in range(1, 13)], "ascent": font.get_ascent(), "descent": font.get_descent(),
                "glyphs": {}, "kerning": {}}
        for i, (code, w, surf) in enumerate(glyphs):
            x, y = (i % cols) * cell_w, (i // cols) * (height + 1)
            hard = pygame.Surface(surf.get_size(), pygame.SRCALPHA); hard.blit(surf, (0, 0))
            atlas.blit(hard, (x, y))
            meta["glyphs"][str(code)] = [x, y, surf.get_width(), surf.get_height(), w]
        # Kerning: width of a pair minus both widths (only Tiny5 has any).
        printable = [chr(c) for c in chars if c < 0x180]
        for a in printable:
            wa = font.size(a)[0]
            for b in printable:
                d = font.size(a + b)[0] - wa - font.size(b)[0]
                if d:
                    meta["kerning"][str(ord(a)) + "," + str(ord(b))] = d
        pygame.image.save(atlas, str(out / f"{key}.png"))
        (out / f"{key}.json").write_text(json.dumps(meta, separators=(",", ":")), encoding="utf-8")
        print(key, len(glyphs), "glyphs", atlas.get_size(), len(meta["kerning"]), "kern")

if __name__ == "__main__":
    main()
