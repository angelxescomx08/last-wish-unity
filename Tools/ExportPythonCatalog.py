"""Export migration references from the separate Python source (never used at runtime)."""
import argparse
import dataclasses
import enum
import inspect
import json
from pathlib import Path
import subprocess
import sys

parser = argparse.ArgumentParser()
parser.add_argument('--source-dir', required=True, type=Path)
parser.add_argument('--output', required=True, type=Path)
args = parser.parse_args()
source = args.source_dir.resolve()
sys.path.insert(0, str(source))
from src.domain.card_pool import PackTheme, card_factories_for_theme, starter_deck
from src.domain.character import ALL_CHARACTERS
from src.application.run_manager import _all_relic_defs
from src.domain.numbers import BigValue

def encode(value):
    if value is None or isinstance(value, (str, int, float, bool)):
        return value
    if isinstance(value, BigValue):
        return {'resolved': str(value.resolve())}
    if isinstance(value, enum.Enum):
        return value.name
    if dataclasses.is_dataclass(value):
        return {field.name: encode(getattr(value, field.name)) for field in dataclasses.fields(value)}
    if isinstance(value, (tuple, list, set, frozenset)):
        return [encode(v) for v in value]
    if callable(value):
        file = inspect.getsourcefile(value)
        return {'callback': value.__qualname__, 'source': str(Path(file).relative_to(source)).replace('\\', '/') if file else None,
                'line': value.__code__.co_firstlineno if hasattr(value, '__code__') else None}
    raise TypeError(type(value).__name__)

cards = [dict(theme=theme.value, **encode(factory())) for theme in PackTheme for factory in card_factories_for_theme(theme)]
relics = [encode(r) for r in _all_relic_defs()]
result = {'schemaVersion': 1, 'sourceCommit': subprocess.check_output(['git', '-c', f'safe.directory={source.as_posix()}', '-C', str(source), 'rev-parse', 'HEAD'], text=True).strip(),
          'notice': 'Reference inventory only; callbacks are not executable Unity implementations.',
          'characters': [dict(**encode(hero), starterDeck=encode(starter_deck(hero.id))) for hero in ALL_CHARACTERS],
          'cards': cards, 'relics': relics,
          'screens': sorted(p.stem for p in (source/'src/presentation/scenes').glob('*.py') if p.stem != '__init__'),
          'applicationModules': sorted(p.stem for p in (source/'src/application').glob('*.py') if p.stem != '__init__')}
args.output.parent.mkdir(parents=True, exist_ok=True)
args.output.write_text(json.dumps(result, ensure_ascii=False, indent=2)+'\n', encoding='utf-8')
print(f'Exported {len(cards)} cards, {len(relics)} relics, {len(result["screens"])} screens')
