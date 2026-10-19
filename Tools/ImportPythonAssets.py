"""Copy every runtime asset of the original Python game into Assets/Resources/Game.

Pixels are never edited, except for packing: hero sheets wider than 8192 px are re-laid out
in more rows (the metadata gains a "pack" section), relic icons are packed into one atlas and
booster art is cropped to its visible bounds (the original did that on load).

    python Tools/ImportPythonAssets.py --source-dir RUTA_PYTHON --output-dir Assets/Resources/Game
"""
import argparse, json, shutil
from pathlib import Path
from PIL import Image

MAX_W = 8192

def copy(src: Path, dst: Path):
    dst.parent.mkdir(parents=True, exist_ok=True)
    shutil.copyfile(src, dst)

def repack_hero(src_png: Path, cell: int, columns: int, rows: int, dst_png: Path) -> dict:
    img = Image.open(src_png).convert("RGBA")
    per_row = columns
    split = 1
    while per_row * cell > MAX_W:
        split *= 2
        per_row = (columns + split - 1) // split
    out = Image.new("RGBA", (per_row * cell, rows * split * cell), (0, 0, 0, 0))
    for r in range(rows):
        for c in range(columns):
            box = (c * cell, r * cell, (c + 1) * cell, (r + 1) * cell)
            tile = img.crop(box)
            nr, nc = r * split + c // per_row, c % per_row
            out.paste(tile, (nc * cell, nr * cell))
    dst_png.parent.mkdir(parents=True, exist_ok=True)
    out.save(dst_png)
    return {"columns": per_row, "split": split}

def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--source-dir", required=True)
    parser.add_argument("--output-dir", default="Assets/Resources/Game")
    args = parser.parse_args()
    src = Path(args.source_dir) / "assets"
    out = Path(args.output_dir)

    for name in ["kit.png", "kit.json", "icons.png", "icons.json"]:
        copy(src / "ui" / name, out / "Ui" / name)

    # Relic atlas
    mapping = json.loads((src / "relics" / "relics.json").read_text(encoding="utf-8"))
    files = sorted(set(mapping.values()))
    cols = 16
    atlas = Image.new("RGBA", (cols * 32, ((len(files) + cols - 1) // cols) * 32), (0, 0, 0, 0))
    rects = {}
    for i, f in enumerate(files):
        im = Image.open(src / "relics" / f).convert("RGBA")
        x, y = (i % cols) * 32, (i // cols) * 32
        atlas.paste(im, (x, y))
        rects[f] = [x, y, im.width, im.height]
    (out / "Relics").mkdir(parents=True, exist_ok=True)
    atlas.save(out / "Relics" / "relics.png")
    (out / "Relics" / "relics.json").write_text(json.dumps({n: rects[f] for n, f in mapping.items()}, ensure_ascii=False, indent=1), encoding="utf-8")

    # Cards
    layout = json.loads((src / "cards-v2" / "layout.json").read_text(encoding="utf-8"))
    for info in layout["frames"].values():
        # Frames (half size) and their golden/diamond variants come from Tools/ExportCardFx.py.
        info["file"] = "Cards/Frames/" + Path(info["file"]).stem
    for theme, rel in layout["packs"].items():
        im = Image.open(src / "cards-v2" / rel).convert("RGBA")
        alpha = im.getchannel("A").point(lambda a: 255 if a >= 16 else 0)
        im = im.crop(alpha.getbbox())
        (out / "Cards" / "Packs").mkdir(parents=True, exist_ok=True)
        im.save(out / "Cards" / "Packs" / Path(rel).name)
        layout["packs"][theme] = "Cards/Packs/" + Path(rel).stem
    for art in (src / "cards-v2" / "art").glob("*.png"):
        copy(art, out / "Cards" / "Art" / art.name)
    layout["art"] = sorted(p.stem for p in (src / "cards-v2" / "art").glob("*.png"))
    copy(src / "Card Sprites" / "Card Back" / "crystal (1).png", out / "Cards" / "back.png")
    dcss = src / "dungeon-crawl-stone-soup-full"
    placeholders = {"ATTACK": "item/weapon/long_sword_1_new.png", "SKILL": "item/armor/shields/shield_2_kite.png", "POWER": "item/misc/misc_orb.png"}
    for kind, rel in placeholders.items():
        copy(dcss / rel, out / "Cards" / "Placeholder" / (kind.lower() + ".png"))
    (out / "Cards" / "layout.json").write_text(json.dumps(layout, indent=1), encoding="utf-8")

    for name in ["room_combat.png", "flame.png", "glow.png", "dungeon.json"]:
        copy(src / "dungeon" / name, out / "Dungeon" / name)
    for f in (src / "gacha").iterdir():
        copy(f, out / "Gacha" / f.name)

    # Heroes: original metadata + repacked sheets
    for hero in ["warrior", "mage", "rogue"]:
        meta = json.loads((src / "characters" / f"{hero}_sheet.json").read_text(encoding="utf-8"))
        meta["pack"] = {}
        for size, file in meta["sheets"].items():
            rows = max(a["row"] for a in meta["animations"].values()) + 1
            info = repack_hero(src / "characters" / file, int(size), meta["columns"], rows, out / "Heroes" / f"{hero}_{size}.png")
            meta["pack"][size] = info
            meta["sheets"][size] = f"Heroes/{hero}_{size}"
        (out / "Heroes" / f"{hero}.json").write_text(json.dumps(meta, indent=1), encoding="utf-8")

    for f in (src / "enemies").iterdir():
        if f.suffix == ".png":
            copy(f, out / "Enemies" / f.name)
    for f in (src / "enemies").glob("*_sheet.json"):
        meta = json.loads(f.read_text(encoding="utf-8"))
        sheet = Image.open(src / "enemies" / meta["sheet"]).convert("RGBA")
        row = meta["animations"]["idle"]["row"]
        cell = sheet.crop((0, row * meta["cell_h"], meta["cell_w"], (row + 1) * meta["cell_h"]))
        box = cell.getchannel("A").getbbox()
        meta["top_native"] = box[1] if box else 0
        meta["idle_bbox_native"] = list(box) if box else [0, 0, meta["cell_w"], meta["cell_h"]]   # combat-log portraits
        (out / "Enemies" / f.name).write_text(json.dumps(meta, indent=1), encoding="utf-8")

    for f in (src / "audio").glob("*.wav"):
        if f.stem in ("preview", "music_preview"):
            continue
        copy(f, out / "Audio" / f.name)
    print("done")

if __name__ == "__main__":
    main()
