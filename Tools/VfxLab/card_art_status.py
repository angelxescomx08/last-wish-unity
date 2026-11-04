#!/usr/bin/env python3
"""Writes docs/card-art.md: every card of the game with whether it already has an animated illustration.

The list is generated, so it never goes stale: the cards come from Assets/Resources/Game/Data/cards.json (with the kind
changes of Assets/Scripts/Domain/CardDesign.cs) and "animated" means the card has an entry in
Assets/Resources/Game/Cards/Anim/anim.json (written by export_game_card_art.py). Run it after exporting new card
animations.

Usage:
  python Tools/VfxLab/card_art_status.py            # write docs/card-art.md
  python Tools/VfxLab/card_art_status.py --check    # exit 1 if docs/card-art.md is out of date
Needs Python 3.8+ only.
"""
import argparse
import json
import os
import re
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, '..', '..'))

CLASSES = [('warrior', 'Guerrera'), ('mage', 'Mago'), ('rogue', 'Pícara'), ('neutral', 'Neutral')]
TYPES = {'ATTACK': 'Ataque', 'SKILL': 'Habilidad', 'POWER': 'Poder', 'STATUS': 'Estado'}
RARITY = {1: 'Común', 2: 'Poco común', 3: 'Rara', 4: 'Épica', 5: 'Legendaria'}
THEMES = {'acero': 'Acero', 'escudo': 'Escudo', 'magia': 'Magia', 'epico': 'Épico'}
KIND_NAMES = {'Attack': 'ATTACK', 'Skill': 'SKILL', 'Power': 'POWER', 'Status': 'STATUS'}


def load(root):
    with open(os.path.join(root, 'Assets', 'Resources', 'Game', 'Data', 'cards.json'), encoding='utf-8') as f:
        cards = json.load(f)
    with open(os.path.join(root, 'Assets', 'Resources', 'Game', 'Cards', 'Anim', 'anim.json'), encoding='utf-8') as f:
        animated = set(json.load(f)['cards'])
    design = os.path.join(root, 'Assets', 'Scripts', 'Domain', 'CardDesign.cs')
    with open(design, encoding='utf-8') as f:
        src = f.read()
    for card_id, kind in re.findall(r'\["(\w+)"\]\s*=\s*CardKind\.(\w+)', src):
        if card_id in cards and kind in KIND_NAMES:
            cards[card_id] = dict(cards[card_id], type=KIND_NAMES[kind])
    art_dir = os.path.join(root, 'Assets', 'Resources', 'Game', 'Cards', 'Art')
    still = {c for c in cards if os.path.exists(os.path.join(art_dir, c + '.png'))}
    return cards, animated, still


def group_of(card_id, card):
    """'starter', 'pool' or 'other' (status cards enemies add and tokens cards create)."""
    if card['type'] == 'STATUS' or card_id.startswith('t_'):
        return 'other'
    return 'starter' if card.get('theme') is None else 'pool'


def sort_key(card_id, card):
    return (0 if group_of(card_id, card) == 'starter' else 1, card['rarity'], card['name'])


def row(i, card_id, card, animated):
    origin = 'Inicial' if group_of(card_id, card) == 'starter' else THEMES.get(card.get('theme'), '—')
    mark = '✅' if card_id in animated else '—'
    return '| {} | {} | `{}` | {} | {} | {} | {} |'.format(i, card['name'], card_id, TYPES.get(card['type'], card['type']),
                                                       RARITY.get(card['rarity'], card['rarity']), origin, mark)


def render(cards, animated, still):
    lines = ['# Ilustraciones animadas de las cartas', '',
             '> Generado por `Tools/VfxLab/card_art_status.py` a partir de `Assets/Resources/Game/Data/cards.json` y '
             '`Assets/Resources/Game/Cards/Anim/anim.json`. No editar a mano: tras exportar animaciones nuevas con '
             '`python Tools/VfxLab/export_game_card_art.py`, ejecutar `python Tools/VfxLab/card_art_status.py` '
             '(`--check` comprueba que está al día).', '',
             'Cada ilustración animada mide 88×64 a 12 fps (24 fotogramas, bucle sin costura) con capa emisiva y sigue '
             '`docs/art-style.md`. Se hacen por orden: primero las cartas iniciales de cada héroe, luego las del '
             'botín. Las que no tienen animación muestran su ilustración fija (`Cards/Art`).', '']
    total = len(cards)
    done = sum(1 for c in cards if c in animated)
    lines += ['## Resumen', '', '| Clase | Animadas | Iniciales animadas |', '|---|---|---|']
    for cls, label in CLASSES:
        ids = [c for c, v in cards.items() if v['class'] == cls and group_of(c, v) != 'other']
        starters = [c for c in ids if group_of(c, cards[c]) == 'starter']
        lines.append('| {} | {} / {} | {} |'.format(label, sum(1 for c in ids if c in animated), len(ids),
                                                    '{} / {}'.format(sum(1 for c in starters if c in animated),
                                                                     len(starters)) if starters else '—'))
    others = [c for c, v in cards.items() if group_of(c, v) == 'other']
    lines.append('| Estados y fichas | {} / {} | — |'.format(sum(1 for c in others if c in animated), len(others)))
    lines.append('| **Total** | **{} / {}** | |'.format(done, total))
    missing = sorted(c for c in cards if c not in still)
    lines += ['', 'Ilustración fija: {} / {} cartas.{}'.format(
        total - len(missing), total, ' Sin ninguna: ' + ', '.join('`{}`'.format(m) for m in missing) if missing else ''),
        '']
    header = ['| # | Carta | Id | Tipo | Rareza | Origen | Animada |', '|---|---|---|---|---|---|---|']
    for cls, label in CLASSES:
        ids = sorted((c for c, v in cards.items() if v['class'] == cls and group_of(c, v) != 'other'),
                     key=lambda c: sort_key(c, cards[c]))
        lines += ['## {} ({} / {} animadas)'.format(label, sum(1 for c in ids if c in animated), len(ids)), ''] + header
        lines += [row(i + 1, c, cards[c], animated) for i, c in enumerate(ids)]
        lines.append('')
    others = sorted(others, key=lambda c: sort_key(c, cards[c]))
    lines += ['## Estados y fichas ({} / {} animadas)'.format(sum(1 for c in others if c in animated), len(others)), '',
              'Cartas que meten los enemigos (Espora, Moho, Herida) o que crean otras cartas (Daga Oculta).', ''] + header
    lines += [row(i + 1, c, cards[c], animated) for i, c in enumerate(others)]
    return '\n'.join(lines) + '\n'


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument('--root', default=ROOT, help='Unity project root')
    ap.add_argument('--check', action='store_true', help='compare with docs/card-art.md instead of writing')
    args = ap.parse_args()
    text = render(*load(args.root))
    path = os.path.join(args.root, 'docs', 'card-art.md')
    if args.check:
        ok = os.path.exists(path) and open(path, encoding='utf-8').read().replace('\r\n', '\n') == text
        print('OK' if ok else 'Differs: docs/card-art.md (run Tools/VfxLab/card_art_status.py)')
        return 0 if ok else 1
    with open(path, 'w', encoding='utf-8', newline='\n') as f:
        f.write(text)
    print('Written', path)
    return 0


if __name__ == '__main__':
    sys.exit(main())
