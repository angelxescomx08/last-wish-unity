# CLAUDE.md

This is the active Last Wish project (Unity 6000.3.6f1, C#). User rule (2026-10-25): every new
feature, card, relic or enemy is built here, never in the Python repository
(`C:/Users/USER/Desktop/web/python/last-wish`), which is the frozen original used only as a
reference and as the source of the parity exporters in `Tools/`.

Read `AGENTS.md`, `README.md` and `docs/port-status.md` before changes; `docs/enemies.md` for
enemies. Code in English, on-screen text in Spanish.

Git: commit and push directly to `main`; the user does not want separate branches (rule of 2026-10-27).

Art: every new picture, sprite, animation or particle effect follows `docs/art-style.md`, the visual style the user
approved on 2026-10-27 (the rogue's animated card illustrations); it replaces the earlier art guidelines.

**Characters, enemies and their animations (user rules, FINAL, 2026-10-30): they are drawn by code in an HTML page, one complete figure per frame, exactly like Tools/VfxLab/mage_hero.html (the reference for the method and for the design of every character and enemy). Every frame computes the whole figure (body, limbs and what it holds) as ONE label map with one outline; each frame has its own written pose; nothing is tweened or rotated at play time; the same script is run with Node to export the game sheets. Never build or animate from pieces: no limb capsules or tubes, no IK, no per-part polygons, no rigs, no "body + arm and weapon drawn on top". Show the user a test pose before a full sheet. See docs/art-style.md section 3.1.** The user has had to repeat the no-pieces rule many times: do not propose it again in any form.

Compiler notes: Unity compiles against .NET Standard 2.1, so .NET 6+ APIs (e.g. `FirstOrDefault(predicate, default)`)
fail in Unity even if a net8/net9 build passes; check new code against netstandard2.1. Code that uses a Unity module
(UnityWebRequest, Physics, ...) also needs that module in `Packages/manifest.json` (`com.unity.modules.*`).
Unity's real compiler output is in `Library/Bee/tundra.log.json`.
