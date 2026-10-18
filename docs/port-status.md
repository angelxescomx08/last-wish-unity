# Migration status

This repository is a Unity foundation, not a complete rewrite of all Python systems.

## Source and art

Source: https://github.com/angelxescomx08/last-wish .
The runtime uses the active warrior v3, mage v2 and rogue v2 96-pixel sheets.
Atlas clip JSON is normalized to Unity-serializable arrays from the original
metadata: row, per-frame duration, loop flag and attack contact time are preserved.
The native atlas is sampled with RawImage.uvRect, accounting for top-origin source
rows and Unity's bottom-origin UV coordinates. The original source images were
copied byte-for-byte, not repainted or warped. The wraith atlas is 128x104 per cell.
The room, common card frame, custom pixel font and original audio are also reused.

## Architecture and behavior

- Domain: immutable card/hero definitions, exact BigInteger arithmetic and seeded
  combat state. Invalid/expensive actions leave state untouched. Huge damage is
  clamped against HP before conversion to int, avoiding overflow.
- Application: battle session command boundary for the presentation layer.
- Presentation: asynchronous startup, uGUI screens, one target, action sequencing
  with input blocked during animations, and event-driven hand/HUD updates.
- Editor: asset import settings, bootstrap scene setup, build and test entry points.
- Demo scope: three heroes, ten-card starter decks, five-card draws, one wraith,
  escalating enemy attacks with a defensive turn every third turn, victory/defeat.

No map, relic, gacha, shop, inventory, savegame or complete effect/reaction parity
is claimed. Those systems should be migrated in later vertical slices with tests.

## Validation

Unity 6000.3.6f1: EditMode passed 16/16 tests; PlayMode passed 1/1 end-to-end test (loading, menu, selection, card play, next turn and return to menu). Actual UI renders were inspected at 1280x720; captures are in docs/screenshots. These checks establish basic functionality, not a comparative FPS benchmark. The original Python suite also passed all 4,152 tests (one optional pytest cache permission warning).
The earlier standalone check compiled all runtime/editor/test C# against local
Unity 6000.3 libraries without warnings/errors, executed 16 NUnit domain cases
outside Unity, and completed a deterministic combat for each of the three heroes.


Windows x64 development build completed successfully with the Mono backend. Tools/Validate.ps1 compiled all C# with zero warnings/errors and passed the 16 domain cases plus three complete hero combats. No standalone FPS comparison has been performed.
