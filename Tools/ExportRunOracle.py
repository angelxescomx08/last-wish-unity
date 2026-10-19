"""Record what the original Python game generates for a set of seeds (maps, encounters,
rewards, packs, treasure/elite/boss relics, shop stock, gachapón pulls, event gold).

Run with the original repository's environment:
    python Tools/ExportRunOracle.py --source-dir RUTA_PYTHON --output Assets/Tests/EditMode/Fixtures/run-oracle.json
"""
import argparse, json, sys
from pathlib import Path

def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--source-dir", required=True)
    parser.add_argument("--output", default="Assets/Tests/EditMode/Fixtures/run-oracle.json")
    args = parser.parse_args()
    sys.path.insert(0, str(Path(args.source_dir).resolve()))
    from src.application import run_manager as rm, card_rewards as cr, gacha
    from src.domain.card_pool import PackTheme
    from src.domain.character import ALL_CHARACTERS
    from src.domain.gacha import PullKind
    from src.domain.map_node import RoomType
    from src.domain.tuning import TUNING

    def relic_ids(relics):
        return [[r.tag.value, r.chroma.value if r.chroma else ""] for r in relics]

    def cards(cs):
        return [[c.id, c.chroma.value if c.chroma else "", bool(getattr(c, "lucky_drop", False))] for c in cs]

    def enemies(es):
        return [[e.id, e.ai or "", e.name, e.max_hp, e.partner_id or "", e.intent.move_id or e.intent.intent_type.name] for e in es]

    relic_sets = [[], ["seven_leaf_clover"], ["master_key", "collector_seal", "antiquary_seal", "appraiser_lens"],
                  ["thief_mask", "two_faced_coin", "rabbit_foot"]]
    defs = {r.tag.value: r for r in rm._all_relic_defs()}
    cases = []
    for seed in [0, 1, 42, 7, 123456789, 9223372036854775807, 31337]:
        for hero_index, hero in enumerate(ALL_CHARACTERS):
            for set_index, tags in enumerate(relic_sets):
                if set_index and (seed not in (42, 7) or hero_index == 1):
                    continue
                TUNING.reset()
                run = rm.create_run(hero, seed)
                for t in tags:
                    rm.acquire_relic(run, defs[t].__class__(**{**defs[t].__dict__}))
                case = {"seed": seed, "hero": hero.id.value, "relics": tags, "floors": []}
                for floor in range(1, 4):
                    m = run.current_map
                    nodes = [[nid, n.room_type.name, sorted(n.connections), n.available] for nid, n in m.nodes.items()]
                    fl = {"floor": run.floor, "rows": m.rows, "cols": m.cols, "boss": m.boss_id, "nodes": nodes, "rooms": {}}
                    for nid, n in m.nodes.items():
                        run.current_room_id = nid
                        room = {}
                        if n.room_type is RoomType.COMBAT:
                            room["enemies"] = enemies(rm.generate_enemies(run, nid))
                            room["reward"] = cards(cr.pick_reward_cards(run, nid))
                        elif n.room_type is RoomType.ELITE:
                            room["enemies"] = enemies(rm.generate_elite(run, nid))
                            room["relic"] = relic_ids([rm.pick_elite_relic(run, nid)])
                            room["reward"] = cards(cr.pick_reward_cards(run, nid, luck_bonus=5))
                        elif n.room_type is RoomType.BOSS:
                            room["enemies"] = enemies(rm.generate_boss(run))
                            room["relics"] = relic_ids(rm.pick_boss_relics(run))
                        elif n.room_type is RoomType.TREASURE:
                            room["relics"] = relic_ids(rm.pick_treasure_relics(run, nid))
                        elif n.room_type is RoomType.EVENT:
                            room["gold"] = rm.generate_event_gold(run, nid)
                        elif n.room_type is RoomType.SHOP:
                            packs, relics = rm.pick_shop_stock(run)
                            room["packs"] = [[p.theme.value, p.chroma.value if p.chroma else ""] for p in packs]
                            room["relics"] = relic_ids(relics)
                        if room:
                            fl["rooms"][nid] = room
                    fl["packs"] = {t.value: cards(cr.pick_pack_cards(run, t)) for t in PackTheme}
                    fl["reroll"] = cards(cr.pick_pack_cards(run, PackTheme.ACERO, reroll=1))
                    case["floors"].append(fl)
                    run.current_room_id = m.boss_id
                    rm.advance_floor(run)
                # Gachapón: alternate pulls with plenty of gold, keeping every relic.
                run.gold = 10 ** 9
                pulls = []
                for i in range(8):
                    kind = PullKind.STELLAR if i % 3 == 2 else PullKind.NORMAL
                    price = gacha.gacha_price(run, kind)
                    result = gacha.pull(run, kind)
                    pulls.append([kind.value, price, result.relic.tag.value, result.relic.chroma.value if result.relic.chroma else "", result.duplicate])
                    if i % 2 == 0:
                        gacha.accept(run, result)
                case["gacha"] = pulls
                case["luck"] = rm.run_luck(run)
                cases.append(case)
    Path(args.output).write_text(json.dumps({"cases": cases}, ensure_ascii=False, separators=(",", ":")), encoding="utf-8")
    print(len(cases), "cases")

if __name__ == "__main__":
    main()
