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
| — (new content, not in Python) | `Domain/EnemyRoster.cs`, `EnemyCatalog.Floor1.cs`, `EnemyPatterns.Floor1.cs`, `CombatState.Expansion.cs` | Floor-1 expansion: 17 enemies (7 normals, 3 minions, 3 elites, 4 bosses in 3 fights incl. the linked pair Los Hermanos Lápida), 6 groups, summons with an on-screen cap, 7 statuses, Invocar/Aturdido intents. Bestiary: `docs/enemies.md`. |
| — (new content, not in Python) | `Domain/EnemyCatalog.Floor2.cs`, `EnemyPatterns.Floor2.cs`, `CombatState.Floor2.cs` | Floor-2 expansion: 15 enemies (6 normals, 3 minions, 3 elites, 3 bosses that replace the Señor de la Cripta on floor 2), 6 groups, 7 enemy statuses and 2 hero debuffs (Sangrado, Necrosis), bound minions, Ofrenda Oscura, Espejo Maldito. Bestiary: `docs/enemies.md`. |
| — (new content, not in Python) | `Domain/EnemyCatalog.Floor3.cs`, `EnemyPatterns.Floor3.cs`, `CombatState.Floor3.cs` | Floor-3 expansion: 15 enemies (6 normals, 3 minions, 3 elites, 3 bosses that replace the Señor de la Cripta on floor 3), 6 groups, the original trio roll, 5 enemy statuses (Pendón/Inspiración aura, Cólera, Inmortal, Cuerda) and 2 hero debuffs (Agotamiento, Congelación), Perforante attacks, Rebobinar. Bestiary: `docs/enemies.md`. |
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
* Art keeps the original PNGs: hero sheets (96/192 px), the 19 original enemy sheets (plus the 17 floor-1, 15 floor-2 and 15 floor-3 generated expansion sheets), kit,
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

## Floor-1 enemy expansion (beyond the original)

* `EnemyRoster { Original, Expanded }` on `RunState`. `Original` is the default and is
  what every parity test builds (`new RunState(hero, seed)`), so the Python behaviour is
  untouched; the menu run (`SceneManager.TCharSelect`) uses `Expanded`, which only
  changes the floor-1 enemy, elite and boss rolls (same random calls, so maps,
  rewards, shops, packs and gacha are identical). Floor 2 has its own expansion (below); floors 3+ run the original code.
* The generated files (`EnemyDefinitions/EnemyPatterns.Generated.cs`) are not edited:
  the expansion is hand-written partial classes merged by `EnemyCatalog.AllDefinitions`
  and routed in `EnemyPatterns.Next` / `ReactToDeaths` only for expansion ais.
* Summons: `EnemyField` caps the field at 3 alive (a large enemy counts as 2); arrivals
  join at the end of the enemy turn, are planned at step 0, do not act that turn and
  minions pay no victory gold. Each summoner has a per-combat budget and minion deaths
  do not feed Carroña.
* Presentation: `Combat/EnemyFormation` (3 small, 1 large, 1 large + 2 small, 2 large;
  original fights keep their coordinates), summon arrival animation
  (`EnemyAnimator.Appear`), new intent kinds, status entries and glossary terms
  (`Data/GlossaryExpansion.cs`, the exported JSON is not edited), banners, history
  entries and callouts (¡ATURDIDO!, ¡CAVA SU TUMBA!, ¡ALMA EN PENA!, ¡SE VA A DIVIDIR!).
* Art: the 17 sheets are generated by `Tools/GenerateEnemyArt.py` (deterministic,
  Pillow) in the exact JSON format of the original sheets; `--check` verifies them.
* Status cards without art draw no placeholder icon instead of looking up a missing
  `Cards/Placeholder/status` texture.

## Floor-2 enemy expansion (beyond the original)

* Same pattern as floor 1, in three hand-written partial files: `EnemyCatalog.Floor2.cs` (definitions, profiles with
  `MinFloor = MaxFloor = 2`, groups, boss fights, `Floor2Rules`), `EnemyPatterns.Floor2.cs` (intents; routed from
  `ExpansionNext` by `EnemyCatalog.IsFloor2`) and `CombatState.Floor2.cs` (rules). `EnemyCatalog.Profiles` holds both floors;
  `ExpansionDefinitions`/`Groups`/`BossEncounters` stay floor-1 only and `AllGroups`/`AllBossEncounters`/`AllDefinitions` add floor 2.
* `Expanded` rolls floor 2 from the originals plus the floor-2 content (same random calls); the floor-2 boss is a seeded
  choice among its 3 fights (`FloorBossKeys(2)`, salt `0xB0552`). Floors 3+ and the `Original` roster are unchanged.
  Pruebas indices of the expansion (encounters 18-42, elites 6-11, bosses 4-9) work on floors 1 and 2.
* `StatusKind` gained `Ward, Ritual, Grudge, Regen, Heads, Reflect, Thirst, Bleed, Necrosis` (appended). Amparo and the
  Reflejo/Cabezas bookkeeping live in `EnemyState.Damage`; Rito and Cicatrización run at the end of an enemy's action;
  Amparo is cleared at the start of the enemy phase; Sangrado/Necrosis tick with the hero's timed debuffs; Necrosis stops
  `HealHero`; Rencor and bound Ilusiones are handled in `HandleEnemyDeaths` (which now also refreshes the floor-2 dynamic
  intents after counting the dead); Ansia de Sangre feeds once per hero turn after cards and at the turn start.
* An enemy with Desvío and no Réplica (Ilusión) parries without answering (no zero-damage "Réplica" hit).
* Presentation: `Art.EnemySheetIds` and `EnemyAnimator.Styles` cover the 15 sheets, `GlossaryExpansion` registers the new
  statuses (Sangrado and Necrosis are glossary terms), tooltips/banners/history describe the new intents and actions,
  hero hits from Reflejo, Sangrado and Rencor are shown like Pinchos, death effects after the enemy turn are now shown too.
* Art: `Tools/GenerateEnemyArtFloor2.py` imports the floor-1 generator (primitives, validation, JSON format) and writes the
  15 sheets; `--check` verifies them byte for byte.

## Floor-3 enemy expansion (beyond the original)

* Same pattern again: `EnemyCatalog.Floor3.cs` (definitions, profiles with `MinFloor = MaxFloor = 3`, groups, boss fights,
  `Floor3Rules`, `LastExpandedFloor = 3`), `EnemyPatterns.Floor3.cs` (intents, routed by `EnemyCatalog.IsFloor3`, and the
  auras) and `CombatState.Floor3.cs` (rules). `Profiles`, `AllGroups`, `AllBossEncounters` and `AllDefinitions` add floor 3.
* `Expanded` rolls floor 3 from the originals plus the floor-3 content with the original call sequence of floor 3, trio roll
  included (pair chance 0.6, trio chance 0.25; the third enemy comes from the original regulars and the floor-3 normals that
  are not members). The floor-3 boss is a seeded choice among its 3 fights (salt `0xB0553`). Floors 4+ and the `Original`
  roster are unchanged. Pruebas indices of the expansion (encounters 18-54, elites 6-14, bosses 4-12) work on floors 1-3.
* `CreateBoss` supports a boss with escorts that are not linked (El Rey Liche + Filacteria: ids `boss_f3`, `boss_f3_e1`).
* `StatusKind` gained `Banner, Inspired, Rage, Undying, Spring, Exhaustion, Frostbite` (appended). `EnemyPatterns.Floor3Auras`
  recomputes Inspiración (from the Pendón of the other living enemies; `HitDamage` adds it) and Inmortal (`LoseHp` keeps 1 HP)
  at combat start, after every card, death, reaction and arrival and before each enemy acts; it is a no-op without those
  statuses, so the original fights are untouched. Cólera's Strength is added in `EnemyState.Damage` and removed after the
  enemy's next action; Perforante passes `pierce` to `HitHero`; Rebobinar heals up to `HpAtTurnStart`; Congelación costs HP
  before the hand is discarded; Agotamiento lowers the mana of the turn start; Autómatas wind up their Relojero.
* Presentation: sheets and styles of the 15 enemies, the 7 statuses in `GlossaryExpansion` (Agotamiento and Congelación are
  glossary terms), tooltips (Perforante, Rebobinar with its current amount, Inspiración and Cólera in the damage breakdown,
  "Perderá su Blindaje"), the incoming-loss preview and lethal glow count Perforante (`Tooltip.LossFrom`), banners for the new
  action fields, floating texts and history for Cólera, Congelación, Agotamiento, Rebobinar and Cuerda.
* Art: `Tools/GenerateEnemyArtFloor3.py` (same kit and validation) writes the 15 sheets; `--check` verifies them.

## Per-card illustrations (beyond the original)

* The original gives every card without art the icon of its type (sword, kite shield, orb)
  or, for elemental cards, a generated element backdrop. Now every card has its own picture
  in the frame's art window: `Cards/Art/<id>.png`, listed in `Cards/layout.json` "art", so
  `CardWidget.DrawIllustration` takes its existing own-art path (cover-fit, clipped); element
  chips are still drawn over it. The frame, text and stat zones are unchanged.
* `Tools/GenerateCardArt.py` (deterministic, Pillow) writes the 176 pictures: a 44x32 recipe
  per card (the window's aspect) saved x4 like the two original status pictures (Espora, Moho,
  untouched). Recipes combine a dithered backdrop, Dungeon Crawl Stone Soup tiles (CC0, read
  from the Python repository; spell icons are cropped from their bevel and screened) and pixel
  effects. `--check` verifies the files and that no two pictures are identical.
* The type placeholders and the element backdrop stay as fallbacks for cards without art.

## Rogue hero sheet (beyond the original)

* La Pícara's combat sheet is redrawn in `docs/art-style.md` by `Tools/VfxLab/rogue_hero_art.py` (one row-scanned
  mass, never assembled from pieces) and baked by `Tools/VfxLab/export_hero_art.py`: 96 px cells (x2 sheet for combat),
  24 columns, 12 fps (idle 24 = 2 s, attack 10, guard 9, hurt 7, cast 12, death 17 held), strike frame 4, cast frame 6,
  plus an additive halo sheet (`HeroSheets.DrawHalo`, the stand-in for bloom) drawn in combat, hero select and menus.
* The combat hero idle runs on its own cycle (`heroIdleTime`), and hero select wraps each hero's idle separately, so a
  2 s loop no longer gets cut at the warrior's 1.6 s. HeroFx uses the arcane palettes and the authored guard/kneel cues.
* El Mago's sheet (2026-10-30) is drawn by code in `Tools/VfxLab/mage_hero.html`: its `<script id="mage-art">`
  computes every frame as one complete figure (body, sleeve, hand and staff in one label map with one outline,
  one written pose per frame, nothing tweened or rotated) and the page shows it animated in a browser.
  `Tools/VfxLab/mage_hero_html.py` runs that script with Node for `export_hero_art.py` (same layout, frame counts
  and events as the rogue: bolt on frame 4, cast frame 6; halo sheet). The exporter bakes every styled hero
  (`--hero` picks one; `BORDER_OK` lets the mage's bolt leave the cell) and checks the whole figure is one region.
  CombatScene gives the mage the authored guard/kneel cues. This HTML method is mandatory for every character and
  enemy (docs/art-style.md 3.1); the rogue's sheet still draws her arm and dagger as pieces and must be redone.
* La Guerrera's sheet (2026-10-31) uses the same method: `Tools/VfxLab/warrior_hero.html` (`<script id="warrior-art">`:
  closed helmet with a T visor, plume, braid, armour, tabard, heater shield on the far arm and long sword, no cape, all
  one label map) through `Tools/VfxLab/warrior_hero_html.py` (`--hero warrior`; `BORDER_OK` for the slash on attack
  frames 3-4). Same layout, counts and events as the mage (slash on frame 4, Furia on frame 6) plus a halo sheet; it
  replaces the old 72-column sheet. CombatScene's UI/enemy idle clock no longer reads the warrior's idle length: it is
  the original's fixed 1.6 s (`IdleCycle`). HeroFx keeps her warm palette with the authored guard/kneel cues.

## Contact shadows (beyond the original)

* Heroes and enemies stand on a pixel-art contact shadow (`Presentation/Fx/GroundShadow.cs`, 2026-10-31): a flat
  dithered ellipse in three tones of a cold near-black, in art pixels (x2 in combat), centred on the line the soles stand
  on. The original's shadows (`HeroFx.Shadow`, the enemy animator's concentric ellipses) peaked at alpha ~40 and the
  menu's was a fixed ellipse 18 px above the feet; the dungeon floors are almost black where the figures stand, so they
  looked like floating.
* Hero sheets carry `ground` metadata (`export_hero_art.ground`: per frame, the centre and width of the figure's last
  four rows of silhouette, native px, plus the ground line y = 94). `HeroSheets.GroundAt` / `DrawShadow` put the shadow
  under the feet of the current frame, mirrored with the sprite, `ShadowMargin` (26 px) wider than the feet, over a faint
  cold floor light (`GroundShadow.FloorLight`, additive). Used by the combat hero and `Looks.Hero(shadow: true)` (main
  menu). The exporter now keeps PNGs whose pixels did not change, so re-exporting only touches the JSON.
* `EnemyAnimator` draws its coloured floor glow first and the shadow on top (`ShadowW` x 0.8 wide). Enemy HP bars and
  statuses go `EntityWidget.EnemyHudDrop` (10 px) lower (`DrawEnemy(hudDrop:)`; the element preview bubble follows), so
  the feet no longer stand on the bar and the shadow shows, the same gap as the hero's. Tests: `GroundShadowTests`.

## Floor map (beyond the original)

* The map is no longer the original's flat coloured boxes on a grid (user, 2026-10-31: it did not fit the art).
  `Presentation/Map/MapArt.cs` builds the plan of the crypt seen from above (`MapPlan`: per art pixel rock / floor /
  corridor / stairs / hall / rug, the room that owns it, the north-wall faces) and paints it in the art style
  (`MapArt.FloorPlan`). `MapScene` draws it x2 inside a kit panel, cached per layout (`MapPlan.Signature` keys the
  `Art.Procedural` textures, so a floor is painted once), then per room its fog (`MapArt.Fog`, rooms out of reach) or
  its rune circle (`MapArt.RuneRing`, rooms you can enter), additive lights (cyan pulse, crimson boss hall, lit torches
  in the rooms you went through), the room icons and a "you are here" gem (`Map/MapIcons.cs`, atlas and halo from
  `Tools/VfxLab/map_icons.py`), a legend of the floor's room kinds and a tooltip per room (name, what it holds, whether
  you can enter). Cells are 40-72 px, multiples of 8; the plan fits 680x614 under the header for every grid up to 12x8.
* Hover and clicks use the chambers' rects; only reachable rooms take a click (`SelectedNode`, unchanged).
  `MapScene.Labels` now reads "Élite", "Jefe", "Gachapón" (nothing else used it); `FillAvail` is gone. Tests:
  `MapArtTests` (EditMode).

## Animated card illustrations (beyond the original)

* The rogue's basic cards (Puñalada, Esquiva, Finta) show the animated illustrations of the VFX Lab
  in the normal card frame. `Tools/VfxLab/export_game_card_art.py` bakes them into
  `Resources/Game/Cards/Anim`: a 6x4 grid of lit colour frames (x2, 176x128, 2 px edge gutter
  against bilinear bleed), a native-size halo sheet (blurred emissive, the stand-in for bloom)
  and `anim.json` (fps, grid, still frame, twinkle colours). `--check` verifies them.
* `CardArtAnimations.For(art, id)` (Presentation/Cards/CardArtAnimation.cs) loads that metadata
  once per `Art` (`Art.ReadText`). `CardWidget.Draw`/`DrawAt` compose a *hollow* face (a
  `FaceKey.Hollow` variant whose art window is left transparent) and draw, before it and inside
  the same tilt transform, the current frame, the additive halo and a few deterministic
  twinkles and motes; the opaque frame hides the window margin, so nothing is clipped. Cached
  faces used directly (deal flight, pack reveal, power bar, burn) show the still frame.
* The clock is `GameContext.Time` plus a phase from the card id (FNV-1a), so copies animate in
  step and cards rebuilt every frame (upgrade previews) never jump. Additive light ignores the
  canvas alpha and tint, so it is scaled by `Canvas.Alpha` and dimmed for unplayable cards.
* The mage's basic cards (Proyectil Arcano, Égida, Chispa Ígnea) are animated the same way since 2026-10-30,
  drawn by `Tools/VfxLab/mage_art.py` (the spell itself, no character; fire ramp for Chispa Ígnea) and listed in
  the same exporter; `MageCardArtTests` (PlayMode) captures the mage's deck and hand.
* The warrior's basic cards (Espadazo, Bloqueo, Guardia Férrea) followed on 2026-10-31, drawn by
  `Tools/VfxLab/warrior_art.py` (her runed longsword and crimson heater shield, no character); `WarriorCardArtTests`
  (PlayMode) captures her deck and hand. `docs/card-art.md` (generated by `Tools/VfxLab/card_art_status.py`, `--check`)
  lists every card and whether it is animated yet.
* Ten of the rogue's common loot cards followed on 2026-11-03 (Pinchazo, Tajo Veloz, Lanzar la Daga, Lluvia de Dagas,
  Cuchillada Errante, Marcar Objetivo, Abrir la Guardia, Corte y Guardia, Afilar, Carterista), drawn by
  `Tools/VfxLab/rogue_pool_art.py` (her runed daggers and what they touch, no character); `RogueLootCardArtTests`
  (PlayMode) adds them to a rogue deck and captures the deck viewer and a hand (`TestResults/rogue-loot-*.png`).
* Ten more the same day, drawn by `Tools/VfxLab/rogue_pool_art_2.py`: the five commons left that read without a
  character (Chatarra, Deshacerse, Finta Doble, Quiebro, Tirar y Cortar) and five uncommons (Hoja Envenenada, Veneno,
  Muro de Humo, Contraataque, Golpe de Gracia). The rogue has 23 of 66 cards animated; `docs/card-art.md` keeps the
  list. Paso Atrás and Rodar (commons) are left for later: they need her figure.
* A third ten followed, all uncommons, drawn by `Tools/VfxLab/rogue_pool_art_3.py`: Corte Rápido, Tajo, Puñalada
  Trapera, Golpe Desesperado, Instinto, Juego de Manos, Manto Raído, Reflejos, Escudo Reactivo and Guardia Evasiva
  (33 of 66 rogue cards animated; Agilidad, Señuelo and Sombra Esquiva are the uncommons left). With 39 animated cards
  two ids landed on the same phase (997 steps), so `CardArtAnimation.Phase` now uses 1000003 steps: all 178 cards of
  the game get distinct phases (`PhasesComeFromTheIdAndDiffer`).
* A fourth ten, drawn by `Tools/VfxLab/rogue_pool_art_4.py`: the three uncommons left (Agilidad, Señuelo, Sombra
  Esquiva) and seven rares (Capa de Sombras, Corte Afortunado, Hoja Única, Ráfaga de Cortes, Tirar los Dados,
  Torbellino, Dagas Ocultas). 43 of 66 rogue cards animated, 49 of 178 in all; left for her: Paso Atrás and Rodar
  (need her figure), and the rares Rebuscar and Vaciar Bolsillos, then the epics and legendaries. Tirar los Dados is the
  first card whose static faces use a frame other than 0 (`still` 16 in `anim.json`: the die showing 3 and the mana).
  `CardArtAnimationTests.FortyRogueLootCardsAreAnimated` now allows rares; `RogueLootCardArtTests` (PlayMode) adds the
  ten to the deck and takes one more scrolled capture (`rogue-loot-deck-scrolled-last`).
* The rest of her cards followed the same evening, 23 in two modules: `Tools/VfxLab/rogue_pool_art_5.py` (Paso Atrás and
  Rodar drawn with her boots, no figure; Rebuscar, Vaciar Bolsillos and eight epics) and `rogue_pool_art_6.py` (the last
  two epics and the nine legendaries). All 66 rogue cards are animated now (72 of 178 cards). Static faces of Astucia,
  Lluvia de Golpes, Tormenta de Acero and Tormenta de Veneno use a later `still` frame (8, 5, 6, 12) because frame 0 is
  their quiet start. `CardArtAnimationTests.EveryRogueCardIsAnimated` checks every rogue card (loot from
  `RunContent.Cards` plus her starter deck); `RogueLootCardArtTests` (PlayMode) puts the 63 loot cards in her deck and
  takes three more scrolled captures (`rogue-loot-deck-scrolled-rares-1..3`).
* Cards without an entry keep `Cards/Art` (unchanged, `GenerateCardArt.py --check` still passes).

## Gachapón sounds (beyond the original)

The original machine borrowed sounds from other screens (the menu tick for the crank and
the capsule rocking, the card swish for the KA-CHUNK and the bounces, the pack tear for
the capsule, confirm/cancel for keeping and declining). Now every beat has its own cue,
synthesized by `Tools/GenerateGachaAudio.py` (stdlib only, deterministic; `--check`
verifies the files, `--preview DIR` mixes whole pulls at the scene's timing) into
`Resources/Game/Audio/gacha_*.wav` with the same import settings as the other effects.
Levels are set by loudness against the existing effects (nav −34 dB … win −21 dB).

Second pass (user: "brillante tipo gacha", more addictive): FM bells, glockenspiel bars,
a Freeverb-style hall, risers and a 50 ms hush before the KA-CHUNK, and celebrations that
grow with the rarity (Común ding-ding → Legendaria timpani roll, brass short-short-long,
choir, bell chord and a 2 s shower of coins and sparkles; −24 dB → −18 dB). Notes are
"open" (D E G A B, no third) so they shine over the D minor theme; only the Legendaria
resolves to D major. The crank bell climbs D E G A B D with the ticks, the capsule rocks
climb D E G A D'.

| Moment (`GachaScene`) | Cue (variants) | Notes |
|---|---|---|
| Pay (`StartPull`) | `gacha_coin_toss` (3) | flick + spinning coin ring; replaces `purchase` |
| Coin in the slot | `gacha_coin_slot` (3) | clink, rattle down inside, landing, bulbs chime |
| Crank starts | `gacha_churn` (2) | tumbling capsules + riser, exactly as long as the crank until the KA-CHUNK; `Advance` stops it with a 40 ms fade |
| 12 crank ticks | `gacha_crank` (6, by step) | ratchet tooth, pitch climbs 7.5 semitones over the turn |
| Electric arcs | `gacha_zap` (3) | garnish: normal voice limit, 110 ms apart, small arcs at 55 % gain |
| Épica+ tease | `gacha_tease` (2) | reverse swell sucked into the glass |
| KA-CHUNK | `gacha_thunk` (3) | latch + heavy drop |
| Out of the chute | `gacha_pop` (3) | flap opens, capsule plops, flap swings back |
| Two bounces | `gacha_bounce` (2, by bounce) | hollow plastic, something rattles inside; the second is higher and softer |
| Hop to the stage | `gacha_hop` (2) | whoosh; with a sparkle run from Poco común |
| Capsule rocks (once per tier) | `gacha_wobble` (5, by rock) | two knocks + a chime D5 E5 F5 G5 A5: the rarer, the higher it climbs |
| Capsule opens | `gacha_open` (3) + `gacha_tier` (5, by rarity) | snap and sparkles, then one stinger per rarity (Legendaria ends on a D major bloom with confetti) |
| Golden relic | `gacha_golden` (1) | shimmering ring after the reveal |
| Quedármela / lands in the counter | `gacha_keep` (3) / `gacha_stash` (3) | rising swoosh, then click-into-place + ding |
| Rechazar | `gacha_dust` (3) | soft crumble, not a failure buzz |
| Hover a button you can press | `nav` | like the shop; silent during the show |

`SoundPlayer` gained ordered variants (`StepVariant`), per-cue gain, `Stop` (new
`IAudioBackend.Stop`; `UnityAudio` fades the voice out in `Tick`), a single effect list
(`SoundPlayer.Effects`, loaded by `UnityAudio` up to the first missing variant), and up to
`GachaVoices` = 6 voices for the show (everything else keeps the original limit of 3).
Pending important cues keep their variant (`gacha_tier`, `gacha_stash` wait for a voice).

## Gachapón interface (beyond the original, 2026-11-03)

User: the relic name spilled out of its box, the buttons did not look like the game, the odds table read poorly and some
texts got lost over the rays and the room. `GachaScene` now draws its whole interface with the HUD kit and outlined text;
the machine, capsules, show timing, sounds, rules and hit areas of the pull buttons are unchanged.

* **Every label is outlined** in ink one font pixel thick (`Text`, 2 px on the ×2 faces), so nothing is lost over the
  rays, the glass or the window.
* **Top bar:** bold pink "Gachapón" with a drop shadow, the rule on a crimson ribbon, and an iron plate with the bag and
  the number of relics left of the pause buttons (`RelicCounter`, where kept relics fly; the number ticks up and the plate
  flashes in the tier colour when the relic lands).
* **Pull cards** (`PullButton(kind)`, same rects for clicks): iron panel (violet with twinkling stars for the stellar one),
  the machine's capsule (silver / gold), name + key cap, note, and the price *is* the button: bronze (normal) or gold
  (stellar) kit button with the coin icon and "405 oro". Short of gold: grey button with "faltan N" in red. Free: "¡Gratis!".
  Big prices shrink (`PriceLine`: drop "oro", then the small face) and never leave the button (`PriceRoom`). Hover lifts the
  card and draws a 2 px gold frame; during the show the cards sink into the dark instead of turning see-through.
* **Odds panel** (`OddsBox`): iron panel, gold title, clover + "Suerte N ▲", one row per rarity with a small code-drawn
  capsule in the machine's tier colours (`gacha.json` `tier_colors` / `tier_light`), the name in its colour, and per
  column the percentage over a 60×8 pixel bar (ink frame, dark track, fill with a light top row); 0 shows a dim "0%",
  luck raises show a green ▲; "Tiradas hechas: N · precio ×1.5" in the footer.
* **Reveal panel** (`RevealLayout(relic, duplicate)` → `RevealCard`): opaque iron panel sized to the name (348–412 px,
  always clear of the machine and the right column), tier-tinted header with the name in the chunky face, tags for rarity
  (with its capsule), chroma ("Dorada · efectos ×2") and "¡Repetida!", then the description wrapped to at most four lines.
  Golden/diamond relics keep their halo and get a pulsing frame with stars *on the frame* (the old sheen and twinkles
  crossed the text). Buttons: gold "Quedármela" + Enter, bronze "Rechazar" + R, "No se devuelve el oro" under it.
* "Haz clic para abrir" + Espacio key cap and the "¡No tienes oro suficiente!" plate are outlined too; `Salir` is a bronze
  kit button.
* Tests: `GachaLayoutTests` (EditMode, 14): every relic × chroma × repeat fits its panel (whole name in the chunky face,
  every word, ≤ 4 lines, clear of the machine, the column and the bottom), the room's boxes never overlap, prices from 0
  to `int.MaxValue` fit their button, every phase of 24 pulls draws (null renderer) and a hero without gold sees what is
  missing. Unity: EditMode 348/348. The README capture was re-rendered with the offline renderer.

## Card design changes (beyond the original)

`Domain/CardDesign.cs` holds the Unity-only design changes; the exported
`NumericCards.Generated.cs` and `Data/cards.json` stay as exported (the parity tests keep
checking them), and `RunContent.Cards` / `GameData` apply the changes on top.

* A power must keep acting between turns: **Poder Oculto** (draw 4) and **Concentración**
  (draw 2) only acted once, so they are skills (`KindChanges`).
* **Sombra Gemela** multiplies: each copy in play (a golden one counts twice) doubles the
  Combo of the first Combo card of the turn again: 1 copy ×2, golden ×4, golden + normal ×8
  (`CombatState.ComboMultiplier`, capped at 2^`MaxComboDoublings`). `PlayResult.ComboTimes`
  feeds "¡COMBO ×4!" and the history. Face text: "Tu primer Combo de cada turno se duplica."

## Active powers on screen (beyond the original)

The "PODERES ACTIVOS" row of tiny cards is replaced by Slay the Spire-style badges under
the hero's statuses (`Combat/PowerBar.cs`): one badge per card with its art, "×N" for copies
(golden 2, diamond 3), a gold rim for chromas, a pop with "+N" when a power or a copy
arrives, and a flash when it acts (turn-start powers when your turn begins, Piel de Acero at
the end of the turn, Sombra Gemela when it multiplies). Hovering a badge shows
`Tooltip.Power` beside it with the card at full size: what it does with the copies in play
and live numbers (`Ui/PowerText.cs`, one line per power), how copies stack (add, multiply,
or "no suman"), and its state this turn (Sombra Gemela ready/used, Ritmo Letal, Cadena
Perfecta count). The hero quick look lists the powers, the inspect panel has one panel per
power, and power cards say they stay next to the hero.

## Power telemetry (beyond the original)

Tracks the power of cards, relics and encounters from real runs and stores it in Supabase
(`docs/telemetry.md`, in Spanish).

* `Domain/PowerTracker` + `PowerRecords`: pure C#. Only adds integers during play. It records each fight
  (room, encounter key, enemies, turns, HP lost, deck and relics at the start), each card played
  (plays, mana paid, HP removed by the play, block gained), card/relic offers and picks, and relic origins.
  Runs changed by Pruebas are flagged `tuned`. `ContentHash` fingerprints card/relic/enemy numbers.
* `Presentation/Telemetry`: `PowerJson` (compact schema-1 JSON), `ITelemetrySink`, and
  `GameContext.Track` (a telemetry error switches it off for the run, never breaks the game).
  `SceneManager` starts/ends the tracker and checkpoints the run when back on the map; `CombatScene.Turns`
  reports plays; `ShopScene` and `GachaScene` report offers and purchases.
* `Runtime/SupabaseTelemetry`: config (`Resources/Telemetry/telemetry.json`, overridden by
  `persistentDataPath/telemetry_config.json`), a disk queue written on a background task
  (`TelemetryOutbox`: current/outbox/rejected), and uploads with `UnityWebRequest` to
  `rest/v1/rpc/ingest_run` from a coroutine that only runs while files are waiting. PlayMode tests set
  `SupabaseTelemetry.Suppressed`.
* `supabase/`: local stack config, migration (raw `telemetry` schema, idempotent `public.ingest_run`,
  `analytics` power views with Wilson intervals and per hero/floor/room baselines) and pgTAP tests.
  Neither schema is exposed through the Data API. `Tools/Telemetry/LocalSupabase.ps1` starts it and
  connects the game. `ProjectSettings` allows HTTP for the local stack; the code only uses plain HTTP for loopback.

## Verification

* EditMode (domain): **132/132** in Unity batchmode and with the offline NUnit runner
  (`Tools/Validate.ps1`): the 100 original cases (parity fixtures against the Python
  engine for cards, powers, enemies, relics, RNG and runs, plus combat-view tests) and
  32 floor-1 expansion cases (`Floor1PatternTests`, `Floor1MechanicTests`: every new
  pattern and phase, summons + cap + budget, minion gold, Pinchos, Desvío, Blindaje,
  Carroña, Buche, stolen Strength/block, Muda, Carga/Aturdido, Vigilancia, División,
  the paired bosses, the expanded roster and the sheets).
* `CardDesignTests` (8) and `PowerUiTests` (10, a real `CombatScene` driven headless with the
  real assets: badges, hover tooltip, card preview, pop, every power's text in the pixel font).
* `GachaSoundTests` (21 EditMode cases; the EditMode assembly and `Tools/DomainSmoke` now also
  compile `LastWish.Presentation`): clips on disk (variants, mono 16-bit, silent ends, no DC,
  unique .meta guids, the churn ends on the KA-CHUNK, rarer stingers longer and louder),
  `SoundPlayer` rules (step variants, voice limits, arc spacing and gain, a waiting stinger keeps
  its rarity, stop, mute/missing/no backend) and a real `GachaScene` with a null renderer and a
  recording mixer, frame by frame: every beat of 42 pulls (tiers 1–5, golden) at its time,
  keep/decline, skipping, clicking the capsule, hover ticks, no gold, 6000 random steps.
* `GachaLayoutTests` (14 EditMode cases): the gachapón interface — reveal panel of every relic, non-overlapping boxes,
  prices that fit their button, every phase drawn, the shortfall shown.
* `Tools/DomainSmoke` also plays 975 floor-1 fights, 1002 floor-2 fights and 1305 floor-3 fights with the expanded roster
  (summons, splits, trios, cap never above 3).
* Floor-2 expansion: 41 EditMode cases (`Floor2PatternTests`, `Floor2MechanicTests`, `Floor2PresentationTests`: every
  pattern, phase and Hydra stage, Pruebas lists, sheets and moves, name collisions, Amparo, Rito, Rencor, Sangrado, robo de
  vida, Cicatrización, bound Ilusiones, Cabezas, Reflejo, Espejo Maldito, Ansia de Sangre, Necrosis, Cosecha de Almas,
  Ofrenda Oscura, summons at the cap, previews, the floor-2 rolls, and a headless `CombatScene` fighting every floor-2
  enemy, group and boss with hover/inspect). Run with the offline NUnit runner (223/223 with the rest of the suite);
  `Floor2EnemyTests` (PlayMode, captures `f2-*.png`) still has to be run in Unity.
* Floor-3 expansion: 37 EditMode cases (`Floor3PatternTests`, `Floor3MechanicTests`, `Floor3PresentationTests`: every
  pattern and phase, the Lich's two phase moves, Pruebas lists, sheets and moves, name collisions, Pendón and its arrivals,
  Cólera, Perforante, Agotamiento, Congelación, Rebobinar, Inmortal, Cuerda, the cap, previews, the floor-3 rolls with trios,
  floors 1-2 untouched, and a headless `CombatScene` fighting every floor-3 enemy, group and boss). Offline runner: 260/260
  with the rest of the suite; the EditMode tests also compile against netstandard2.1. `Floor3EnemyTests` (PlayMode, captures
  `f3-*.png`) still has to be run in Unity.
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
  captures to `TestResults/`. `ExpansionEnemyTests` fights every new enemy, group and
  boss fight, fills the field with summons (Madre Larva, Rey Rata), splits the Cieno,
  revives the Plañidera and checks that formation HUDs never overlap
  (captures `x-*.png`). Run in Unity 6000.3.6f1 batchmode.

## Known differences

* Python's `hash()`-salted event text is replaced by CRC32 of the room id (stable).
* Resizing the window behaves like the original viewport (aspect-fit, letterbox).
* `docs/performance/README.md` measured the previous uGUI version and is kept as history.

Screenshots in `docs/screenshots/` come from the offline renderer of the C# drawing
code (the same commands Unity executes), not from the Unity editor.

## Event expansion (2026-10-31)

Unity-only feature: the four passive gold event texts are replaced by 50 selectable events. Pure C# definitions/sessions and RunState.Events own floor/rarity selection, six three-chapter chains, multi-stage encounters, nonlethal sacrifices, atomic costs, deterministic risks and per-run history. EventScene integrates decisions and results with the existing map; the scene manager no longer gives a second gold reward. The JSON design source generates C# data and the complete tracking list (docs/events.md), with all art marked pending. EditMode EventTests and PlayMode EventFlowTests cover domain rules and map/event interaction. Unity-only development was reconfirmed by the user on 2026-10-31.

## Second event batch: 100 additional events (2026-10-31)

The catalog now contains 150 events. The original 50 definitions are preserved unchanged. The second batch adds 60 independent encounters and 10 four-event stories: six linear and four with mutually exclusive endings. Eight new encounters contain multiple stages (12 total). New floor bands include 7–9 and 10+, with late stories ending on floors 8–9 or deeper. All choices run through the existing pure C# event engine; no new art or runtime mechanics were added. GenerateEvents derives totals and story paths from the data. EventExpansionTests checks every stage path, floor selection, link reachability, acyclic stories, exit choices and exclusive endings. All 150 visual designs remain pending.
