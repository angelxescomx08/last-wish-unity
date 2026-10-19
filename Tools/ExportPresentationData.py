"""Export the original game's presentation text and tables (glossary, rich-text rules, card faces,
relic art names, enemy identities, hero stats texts) into Assets/Resources/Game/Data/*.json.

Everything is read from the Python game at export time; the Unity game never runs Python.
    python Tools/ExportPresentationData.py --source-dir RUTA_PYTHON --output-dir Assets/Resources/Game/Data
"""
import argparse, json, os, sys
from pathlib import Path

def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--source-dir", required=True)
    parser.add_argument("--output-dir", default="Assets/Resources/Game/Data")
    args = parser.parse_args()
    os.environ.setdefault("SDL_VIDEODRIVER", "dummy"); os.environ.setdefault("SDL_AUDIODRIVER", "dummy")
    sys.path.insert(0, str(Path(args.source_dir).resolve()))
    from src.presentation.ui import glossary as gl, rich_text
    from src.domain import entities, keywords as kw
    from src.domain.card_pool import _POOL, PackTheme, starter_deck, hidden_dagger
    from src.domain.character import ALL_CHARACTERS
    from src.domain.status_cards import STATUS_CARD_FACTORIES
    from src.domain.elements import REACTIONS, REACTION_TEXT, REACTION_RULE, Reaction
    from src.application import enemy_roster, elites, enemy_ai
    from src.application.run_manager import _all_relic_defs
    from src.infrastructure.sprite_loader import own_relic_sprites
    from src.application.play_card import target_kind
    out = Path(args.output_dir); out.mkdir(parents=True, exist_ok=True)

    def term(t):
        return {"name": t.name, "rule": t.rule, "icon": t.icon, "color": list(t.color), "pattern": t.pattern.pattern, "lines": list(t.lines)}
    glossary = {
        "colors": {k: list(getattr(gl, k)) for k in ("KEYWORD", "DAMAGE", "BLOCK", "POISON_INK", "MANA", "BUFF_TITLE", "DEBUFF_TITLE",
                                                     "FIRE_INK", "WATER_INK", "EARTH_INK", "AIR_INK", "ICE_INK", "REACTION_INK", "DIM", "BODY")},
        "all_terms": [term(t) for t in gl.ALL_TERMS],
        "shield": term(gl.SHIELD),
        "status_terms": {k: term(v) for k, v in gl.STATUS_TERMS.items()},
        "keyword_terms": {k.value: term(v) for k, v in gl.KEYWORD_TERMS.items()},
        "reaction_terms": {k.name: term(v) for k, v in gl.REACTION_TERMS.items()},
        "elements_rule": term(gl.ELEMENTS_RULE), "exhaust": term(gl.EXHAUST), "ethereal": term(gl.ETHEREAL), "unplayable": term(gl.UNPLAYABLE),
        "status_kind": dict(gl._STATUS_KIND), "status_icon": dict(gl._STATUS_ICON), "status_short": dict(gl._STATUS_SHORT),
        "status_text": dict(entities.STATUS_TEXT),
        "reaction_icon": {k.name: v for k, v in gl.REACTION_ICON.items()}, "reaction_color": {k.name: list(v) for k, v in gl.REACTION_COLOR.items()},
        "reaction_names": {k.name: k.value for k in Reaction}, "reaction_text": {k.name: v for k, v in REACTION_TEXT.items()}, "reaction_rule": REACTION_RULE,
        "reactions": [[a.value, b.value, r.name] for (a, b), r in ((tuple(p), r) for p, r in REACTIONS.items())],
        "element_color": {k: list(v) for k, v in gl.ELEMENT_COLOR.items()},
        "keyword_defs": {k.value: {"name": v.name, "rule": v.rule} for k, v in kw.KEYWORD_DEFS.items()},
        "rich_rules": [[p.pattern, list(c)] for p, c in rich_text._RULES],
        "status_names": {n: getattr(entities, n) for n in ("POISON", "MARKED", "WEAK", "VULNERABLE", "FRAIL", "ENTANGLED", "STRENGTH", "BLADES", "FUSE", "FIRE", "WATER", "EARTH", "AIR", "FROZEN")},
    }
    (out / "glossary.json").write_text(json.dumps(glossary, ensure_ascii=False, indent=1), encoding="utf-8")

    def layers(effects):
        return [kw.layer_text([fx]) for fx in effects]
    def card_info(card, theme=None):
        effects = card.all_effects()
        tk = target_kind(card)
        info = {
            "id": card.id, "name": card.name, "type": card.card_type.name, "rarity": card.rarity.value, "cost": card.cost,
            "class": card.card_class.value, "theme": theme,
            "texts": [fx.text for fx in effects],
            "combo": kw.combo_text(card) if card.combo_effects() else None,
            "singular": kw.singular_text(card) if card.singular_effects() else None,
            "void": kw.void_text(card) if card.void_effects() else None,
            "spoil": kw.spoil_text(card) if card.spoil_effects() else None,
            "reaction": kw.reaction_text(card) if card.reaction_effects() else None,
            "fury": kw.fury_text(card) if card.fury_effects() else None,
            "endure": kw.endure_text(card) if card.endure_effects() else None,
            "badges": [e.value for e in card.element_badges()],
            "chips": [[fx.element.value, fx.element_stacks, fx.element_all] for fx in effects if fx.element is not None and fx.element_stacks > 0],
            "needs_target": any(fx.needs_target for fx in effects), "hits_all": any(fx.hits_all_enemies for fx in effects),
            "target": tk.name,
            "unplayable": card.unplayable, "exhaust": card.exhaust, "ethereal": card.ethereal, "play_on_draw": card.play_on_draw,
            "power_turn": any(fx.on_turn_start is not None for fx in effects),
            "keywords": sorted(k.value for k in card.keywords()),
        }
        return info
    cards = {}
    for theme in PackTheme:
        for f in _POOL[theme]:
            c = f(); cards[c.id] = card_info(c, theme.value)
    for ch in ALL_CHARACTERS:
        for c in starter_deck(ch.id):
            c.card_class = __import__("src.domain.card", fromlist=["CardClass"]).CardClass(ch.id.value)
            cards.setdefault(c.id, card_info(c))
    for factory in STATUS_CARD_FACTORIES.values():
        c = factory(); cards[c.id] = card_info(c)
    d = hidden_dagger(); cards[d.id] = card_info(d)
    (out / "cards.json").write_text(json.dumps(cards, ensure_ascii=False, indent=1), encoding="utf-8")

    sprites = own_relic_sprites()
    relics = {r.tag.value: {"id": r.id, "name": r.name, "description": r.description, "rarity": r.rarity.value,
                            "class": r.relic_class.value, "sprite": r.name if r.name in sprites else None} for r in _all_relic_defs()}
    (out / "relics.json").write_text(json.dumps(relics, ensure_ascii=False, indent=1), encoding="utf-8")

    enemies = {}
    for d in enemy_roster.ENEMIES.values():
        enemies[d.ai] = {"name": d.name, "identity": d.identity, "title": d.title, "kind": "regular"}
    for d in elites.ELITES.values():
        enemies[d.ai] = {"name": d.name, "identity": getattr(d, "identity", ""), "title": getattr(d, "title", ""), "kind": "elite"}
    for d in enemy_ai.BOSSES.values():
        enemies[d.ai] = {"name": d.name, "identity": getattr(d, "identity", ""), "title": getattr(d, "title", ""), "kind": "boss"}
    pairs = {p.key: {"name": p.name, "synergy": p.synergy, "members": list(p.members)} for p in enemy_roster.PAIRS.values()}
    (out / "enemies.json").write_text(json.dumps({"enemies": enemies, "pairs": pairs}, ensure_ascii=False, indent=1), encoding="utf-8")
    print(len(cards), "cards", len(relics), "relics", len(enemies), "enemies")

if __name__ == "__main__":
    main()
