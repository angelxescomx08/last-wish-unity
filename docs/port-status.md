# Migration status

Unity project: standalone Git repository at `C:/Users/USER/Desktop/web/unity/last-wish-unity`.
Python source: separate repository at `C:/Users/USER/Desktop/web/python/last-wish`.
Engine: Unity 6000.3.6f1. This is an ongoing migration, not full Python parity.

## Runtime coverage

| System | Implemented | Remaining |
|---|---|---|
| Startup/menu/settings | Async loading, 2D editor startup, Play shortcut, FPS setting | Full original settings and Pruebas |
| Heroes | Original stats and starter decks for all 3 heroes | Further class keywords/powers |
| Combat | BigInteger damage, mana, block/dexterity, draw/discard, strength, starter Combo/Aguante/Fire | Multiple enemies, target selection, full effects/statuses/reactions |
| Run/map | Persistent HP/gold/deck/relics, orthogonal routes, 9 room kinds, floor scaling/advance | Python PRNG/seed parity and full relic interactions |
| Cards | 33 numeric pool cards, class filtering, starter cards and generic capped upgrades | 132 pool cards with callbacks/keywords/custom upgrades |
| Relics | Combat Amulet, Fire Orb, Broken Totem, Gold Ring, Iron Heart, Vitality Amulet, Blood Potion, Thief Mask | Other 148 relics, chromas and triggers |
| Shop/packs | Seeded 3 pack + 3 relic offers, prices, sold flags, choose 1 of 5 cards | Luck/chroma and relic-altered picks/stock |
| Treasure/events | Take a relic; basic heal/gold event | Original event variations and relic interactions |
| Warlock/purge | Upgrade a selected card, remove one per visit, prices and last-card protection | Special upgrades and related relics |
| Gacha | Rising prices, normal/stellar tier weights, hero luck, accept/decline | Chromas and special free pulls |
| Rewards/death | Card choice/skip, gold, HP, elite/boss relic, floor advance, death/restart | Boss relic choices and all original reward effects |
| Save/inventory | Atomic versioned checkpoint + backup, data validation, paged deck/relic inventory | Mid-combat save; deck-choice screens |
| Enemies | Wraith atlas, single-enemy combat, stronger placeholder elite/boss | Original roster, pairs, bosses, elites, patterns, summons, art |

Current offered cards/relics have executable rules. Unsupported content is retained
in the reference catalog and is not offered as a silently simplified version.
Starter effects covered: Rogue Finta grants Combo block after another card; Warrior
Guardia Férrea grants Strength only if block existed before playing it; Mage
Chispa Ígnea applies persistent Fire that damages after the enemy acts.

## Architecture and art

Domain and Application are plain C# without engine dependencies. Presentation
owns uGUI, async resources, audio, animation, persistence and navigation. RunMap
owns graph generation; RunState owns progression/economy; RunSave maps validated
checkpoints. GameBootstrap's run screens are separated in RunScreens.cs.

Original warrior v3, mage v2, rogue v2 and wraith atlases are unchanged; rows and
per-frame timings are preserved. Point filtering, no mipmaps/compression, native
atlas sizes and UV animation remain. Font/audio attribution is in THIRD_PARTY_NOTICES.md.

## Reference inventory and next ports

`migration/python-catalog.json` records source commit, all 165 pool cards, 156
relics, starter decks, 17 screens and application modules, including callback
source names/lines. It is documentation, not runtime content. Coverage is in
`migration/card-coverage.json`.

Remaining work: port enemy roster/patterns and multi-target combat;
then effect chains, keywords, elements/reactions, powers and card callbacks;
then all relic triggers/chromas, reward/deck choices and Pruebas, with parity tests.
No complete-migration claim is made while these remain.

## Validation

2026-10-18, Unity 6000.3.6f1:

- EditMode: 38/38 passed. Includes 100 generated seeds, map invariants, economy,
  checkpoints, original hero stats/decks, Combo/Aguante/Fire and BigInteger cases.
- PlayMode: 3/3 passed: demo regression; new run > map > combat > card reward >
  checkpoint > inventory; each noncombat room (pack purchase/pick, treasure,
  event, upgrade, purge, gacha accept and return to map).
- Windows x64 development build: succeeded from the independent project path.
- Tools/Validate.ps1: zero compile warnings/errors, 38 NUnit cases and three demo
  combats completed outside Unity.
- Actual Unity renders inspected: menu/combat, map/inventory, shop/gacha.
- Map frame sample in Editor PlayMode, 120 frames: median 16.66 ms, p95 17.17 ms,
  mean 16.82 ms (60 FPS target). Raw measurements are in
  migration/map-performance.json. This is an Editor baseline, not a standalone
  CPU/GPU profile or proof of improvement versus Python.

The original Python suite previously passed 4,152 tests; no Python runtime code
was changed in this migration step. Offline .NET checks additionally compile all
runtime/editor/test C# and run the domain tests plus three complete demo combats.
