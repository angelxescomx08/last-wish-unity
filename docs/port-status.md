# Migration status — 2026-10-19

Unity: standalone repository `C:/Users/USER/Desktop/web/unity/last-wish-unity`.
Python: separate repository `C:/Users/USER/Desktop/web/python/last-wish`.
Engine: Unity 6000.3.6f1, Built-in renderer, Input System 1.17.

The goal is the original game, screen by screen and rule by rule. Every module of
the Python `src/presentation`, `src/application` and `src/domain` packages and the
`main.py` scene manager now has a C# counterpart.

## Coverage

| Python | C# | Notes |
|---|---|---|
| `domain/*`, `application/*` (rules) | `Assets/Scripts/Domain` | Combat, 165 cards, 7 keywords, elements/reactions, 156 relic tags, chromas, 19 enemies + pairs/trios, elites, bosses, map, run economy, luck, gacha, warlock, purge, deck choices, Pruebas (`Tuning`). |
| `application/combat_log.py`, `play_preview.py` | `Presentation/Combat/CombatLog.cs`, `ElementPreview.cs` | Pure; the preview plays the card on `CombatState.Clone()` with the same random stream. |
| `main.py` `SceneManager` | `Presentation/SceneManager.cs` | Every transition, pause menu, hero sheet (C / button), gold counter with interest notes, deck-choice auto-open, Dado Cargado reroll. |
| `scenes/*` (17 screens) | `Presentation/Scenes/*` | Menu, settings, Pruebas, character select, map, combat, rewards, treasure, shop, packs, event, boss reward, warlock, gacha, purge, deck choice, death. |
| `ui/*` | `Presentation/Ui/*`, `Presentation/Combat/*`, `Presentation/Cards/*` | Card faces, tooltips, glossary/rich text, HUD kit, mana orb, entity widgets, intents, targeting arrow, action banners, history strip + detail, inspect panel, element wheel/preview, hero sheet, viewers, pause menu, gold HUD, luck badge, loading screen. |
| `fx/*` | `Presentation/Fx/*` | Particles/bursts, chroma, ready keywords, card deal, card burn, enemy animator (styles, floating swords), hero fx, reaction show, dungeon backdrop. |
| `infrastructure/*` | `Presentation/Core/*`, `Runtime/*` | Asset metadata, fonts (pixel bitmap faces), sounds, preferences, dev settings, viewport. |

Removed on purpose: the earlier Unity-only checkpoint save and "Continuar" (the
original has no save; its main menu never offers it), the uGUI screens, and the
`Application` assembly (its battle session is now the combat scene).

## Architecture

```text
Domain (pure C#)  ←  Presentation (pure C#, draws through IRenderBackend)  ←  Runtime (Unity)
```

* `Presentation` reproduces pygame drawing on a virtual 1280×720 `Canvas`: sprites,
  rects, circles, polygons, text, clips, transforms, tints and the blend modes the
  original uses (alpha, `BLEND_RGB_ADD`, multiply, masked add). pygame quirks are kept
  where they show on screen (draw calls ignore alpha on the display surface,
  `BLEND_RGB_ADD` ignores alpha, SRCALPHA overwrite, `Rect` centre rounding,
  banker's rounding, wheel = MOUSEWHEEL + buttons 4/5).
* `Runtime/UnityRenderBackend` appends every quad/triangle of a frame to one dynamic
  mesh (sub-meshes split only by texture/blend) executed by one command buffer;
  clipping is done on the CPU. Offscreen targets compose static card faces once
  (LRU cache) so a card in hand is one quad. Steady state allocates nothing.
* `Runtime/GameBootstrap` is `main.py`: loading screen between startup steps, then
  input → `SceneManager.Update` → draw into a 1280×720 `RenderTexture` presented
  aspect-fit with black bars (bilinear like `smoothscale`, nearest at native size).
  Mouse positions are converted like `viewport.to_virtual`.
* Art keeps the original PNGs: hero sheets (96/192 px), the 19 enemy sheets, kit,
  icons, relics, card frames/art, packs, gacha, dungeon room. `Editor/PixelArtImporter`
  imports everything under `Resources/Game` with Point filtering, no mipmaps, NPOT
  None, uncompressed, max 8192 (card faces/packs/back: bilinear, as the original
  smoothscales them).

Small domain additions for presentation (rules unchanged, tests green):

* `SeededRandom` reproduces `new System.Random(seed)` exactly with a copyable state,
  so `CombatState.Clone()` previews use the stream the real play will use.
* Each card of a fight is its own object (`CardDefinition.Copy`, like the original's
  `copy.copy`), so the hand animation tracks cards by identity.
* Hero Fuerza is also mirrored into the hero statuses (its badge), Cadena Rota makes
  enemy junk ethereal, enemy sheets carry the idle bounds used by history portraits.
* The combat scene keeps the original's enemy list semantics (the dead are dropped
  after each enemy turn) for slots, targeting, history and victory gold.

## Verification

* EditMode (domain): **100/100** with the offline NUnit runner (parity fixtures against
  the Python engine for cards, powers, enemies, relics, RNG and runs, plus combat-view
  tests: random stream copy, preview = real play, card identity, Fuerza badge,
  Cadena Rota).
* All C# (Domain, Presentation, Runtime, Editor, tests) compiles against the Unity
  6000.3.6f1 assemblies with no errors.
* Offline renderer: the presentation's draw commands rasterised and compared side
  by side with pygame screenshots of the same scenes (menu, cards, map, shop, reward,
  treasure, pack opening, gacha phases, combat idle/hover/aim/play/enemy turn/new turn).
* Autoplay smoke: the real `SceneManager` driven from the menu through a run (combats,
  rewards, shop, purge, gacha, treasure, event, death) drawing every frame, no errors.
* PlayMode tests (`Assets/Tests/PlayMode`) boot `Bootstrap.unity`, drive the game with
  injected input (menu → selection → map → combat → play → end turn → pause → hero
  sheet), mouse aiming, and fight every enemy and pair with its sheet, saving canvas
  captures to `TestResults/`. **They must be run in Unity** (not run in this session).

## Known differences

* Python's `hash()`-salted event text is replaced by CRC32 of the room id (stable).
* Resizing the window behaves like the original viewport (aspect-fit, letterbox).
* `docs/performance/README.md` measured the previous uGUI version and is kept as history.

Screenshots in `docs/screenshots/` come from the offline renderer of the C# drawing
code (the same commands Unity executes), not from the Unity editor.
