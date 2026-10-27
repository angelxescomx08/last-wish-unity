# CLAUDE.md

This is the active Last Wish project (Unity 6000.3.6f1, C#). User rule (2026-10-25): every new
feature, card, relic or enemy is built here, never in the Python repository
(`C:/Users/USER/Desktop/web/python/last-wish`), which is the frozen original used only as a
reference and as the source of the parity exporters in `Tools/`.

Read `AGENTS.md`, `README.md` and `docs/port-status.md` before changes; `docs/enemies.md` for
enemies. Code in English, on-screen text in Spanish.

Compiler notes: Unity compiles against .NET Standard 2.1, so .NET 6+ APIs (e.g. `FirstOrDefault(predicate, default)`)
fail in Unity even if a net8/net9 build passes; check new code against netstandard2.1. Code that uses a Unity module
(UnityWebRequest, Physics, ...) also needs that module in `Packages/manifest.json` (`com.unity.modules.*`).
Unity's real compiler output is in `Library/Bee/tundra.log.json`.
