"""Copy original enemy pixels and normalize atlas metadata for the Unity UV player."""
import argparse
import hashlib
import json
from pathlib import Path
import shutil

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--source-dir', required=True, type=Path)
parser.add_argument('--output-dir', required=True, type=Path)
args = parser.parse_args()
art = args.output_dir / 'Assets/Resources/Game/Art'
art.mkdir(parents=True, exist_ok=True)
count = 0
for metadata in sorted((args.source_dir / 'assets/enemies').glob('*_sheet.json')):
    source = json.loads(metadata.read_text(encoding='utf-8-sig'))
    name = metadata.stem.removesuffix('_sheet')
    original = metadata.parent / source['sheet']
    destination = art / (name + 'Atlas.png')
    shutil.copyfile(original, destination)
    assert hashlib.sha256(original.read_bytes()).digest() == hashlib.sha256(destination.read_bytes()).digest()
    normalized = {
        'cellWidth': source['cell_w'], 'cellHeight': source['cell_h'],
        'anchorX': source['anchor'][0], 'anchorY': source['anchor'][1],
        'attackContactSeconds': .3,
        'clips': [{'name': name, 'row': clip['row'], 'durations': [ms / 1000 for ms in clip['durations_ms']],
                   'loop': clip.get('loop', name == 'idle')} for name, clip in source['animations'].items()],
        'moves': [{'id': name, 'clip': clip} for name, clip in source.get('moves', {}).items()],
    }
    (art / (name + 'Clips.json')).write_text(json.dumps(normalized, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    count += 1
print(f'{count} enemy atlases copied unchanged; original timing, anchors and moves retained')
