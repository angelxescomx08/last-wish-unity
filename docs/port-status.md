# Migration status — 2026-10-18

Unity: standalone repository `C:/Users/USER/Desktop/web/unity/last-wish-unity`.
Python: separate repository `C:/Users/USER/Desktop/web/python/last-wish`.
Engine: Unity 6000.3.6f1. Full Python parity is still in progress.

## Coverage

| System | Implemented | Remaining |
|---|---|---|
| Startup/input | Async loading/progress, 2D/Bootstrap Play shortcut, Input System 1.17 actions + uGUI input module, keyboard/mouse target selection | Full settings/Pruebas; physical gamepad validation |
| Heroes | Original stats, starter decks and original artwork for all 3 | Full interaction with remaining relics/chromas |
| Combat | Multiple enemies, targeting, intents, BigInteger printed damage, block, weak/vulnerable/frail, poison, entangled, fire/water/earth/air, 6 reactions, status cards | Full original presentation/preview/log; all relic hooks |
| Enemies | All 11 normal, 5 elite, 3 boss AI patterns; pairs/trios, ally support, vengeance, enrage, multihits, fuse, heal/buffs, status-card spawns; all original atlases | Exact Python PRNG sequence; final visual tuning for all formations |
| Cards | All 165 pool cards implemented with numeric layers + native commands; all 7 keywords; powers, random/discard effects, daggers, healing/HP costs, generic upgrades | Chromas, card merging/breaking, exhaustive combinations with all relics |
| Run/map | HP/gold/deck/relic persistence, orthogonal routes, 9 room kinds, floor scaling/advance | Python PRNG/seed parity and complete economy/relic interactions |
| Relics | Combat Amulet, Fire Orb, Broken Totem, Gold Ring, Iron Heart, Vitality Amulet, Blood Potion, Thief Mask (8/156) | Other 148 relic rules, chromas and trigger combinations |
| Shop/packs | Seeded offers, prices/sold flags, class filtering, choose 1 of 5 cards from full pool | Luck/chroma and relic-altered picks/stock |
| Treasure/events | Take a relic; heal/gold event | Original event variations and relic-dependent choices |
| Warlock/purge | Generic capped upgrade, selection, prices and last-card protection | Original special/deck modification workflows, relic effects |
| Gacha | Rising prices, normal/stellar weights, hero luck, accept/decline | Chromas and special/free pulls |
| Rewards/death | Card choice/skip, gold, HP, elite/boss relic, floor advance, restart | Boss relic choices and all original reward effects |
| Save/inventory | Atomic versioned checkpoint/backup, validation, paged inventory; effect layers/commands/power kind retained | Mid-combat saves and specialized deck-choice screens |

Current offered pool cards have executable native rules; unsupported relics are
kept in the reference catalog and are not offered as simplified placeholders.
All 165 being ported does not imply every card/relic combination is verified.

## Architecture and art

Domain/Application are engine-independent C#. `CombatState`, `EffectLayer` and
`CardCommands` implement the battle; `EnemyCatalog` and generated pattern data
implement encounters. Callback closures become validated command data, never
Python code/reflection at runtime. `RunSave` round-trips commands and effects.
`RunMap`/`RunState` own progression. Runtime owns uGUI, input, async assets, audio,
UV animation, navigation and persistence; `BattleActors` owns enemy views.

`CardView` pools controls and invalidates content on combat context changes.
The FPS label and hover tooltip have separate canvases; atlas metadata is cached;
encounter resources load asynchronously. See [performance](performance/README.md)
for measured scope, raw before/after data and official Unity documentation.

Original warrior v3, mage v2, rogue v2 and all 19 enemy atlases retain their PNG
pixels, row ordering, timing and anchors. Point filtering, no mipmaps/compression,
native sizes and UV animation remain. Attribution: `THIRD_PARTY_NOTICES.md`.

## Reproduction and validation

`migration/python-catalog.json` (relative to this docs folder) records the original
165 cards, 156 relics, starter decks, 17 screens and callback source references.
`migration/card-coverage.json` lists the full implemented card pool and the two
random-result cards omitted from exact one-cast replay fixtures. Random outcomes
have native bounded tests, but Python/C# random sequences are not identical.

Optional `Tools/ExportNumericCards.py`, `ExportEnemyPatterns.py` and
`ImportEnemyArt.py` accept `--source-dir` and `--output-dir`. Run with the source
repository's Python environment; they generate C#/fixtures and copy unchanged art.
They are not runtime dependencies. Commands are in the root README.

Validated in Unity 6000.3.6f1:

- EditMode: **72/72**, including 326 differential card casts, 92 power snapshots
  through three following turns, 1,368 enemy intents, 100 map seeds, economy/save,
  keywords/reactions, power combinations and arbitrary-precision arithmetic.
- PlayMode: **7/7**. Startup/demo and run-to-reward/checkpoint/inventory; every
  noncombat room; all 19 enemy atlases; pair targeting and pooled button identity;
  actual queued keyboard/mouse input through the new input module, hover tooltip;
  JSON round-trip preserving the native effects/commands of all 165 cards.
- Windows x64 development build succeeded. Final player benchmark: 300 idle frames
  + 30 unchanged refreshes, valid Unity GC counter. Refresh median 3.0443 →
  0.0301 ms; refresh-frame GC 635,323.6 → 1,639.6 bytes. Idle remains capped at ~60
  FPS, not a claim of higher maximum FPS or all-scenario speedup.
- `Tools/Validate.ps1`: zero compile errors/warnings, 72 offline NUnit cases and
  three completed demo combats; additional to real Unity tests.
- Inspected actual Unity renders: pair, original knight boss, tooltip, map,
  inventory/shop/gacha from the run tests. All original hero artwork is preserved.

Remaining migration order: relic triggers/chromas and their economy/reward/deck
workflows; full events and Pruebas; exhaustive cross-system parity and visual QA.
