# Agent instructions

Read README.md and docs/port-status.md before changes.
Use Unity 6000.3.6f1. Code identifiers, comments and filenames are English; UI text is Spanish.
Keep Domain and Application free of UnityEngine and other I/O dependencies.
Visual style: docs/art-style.md is mandatory for every new illustration, sprite, animation and particle effect, and for any redraw the user asks for (user rule 2026-10-27; it replaces the earlier art guidelines, including the code-drawn "Espectro" method of the Python version). Do not redraw existing art on your own; keep the frame ordering and timing of existing sheets and do not warp atlases.
Characters, enemies and their animations (user rules, FINAL, 2026-10-30): they are drawn by code in an HTML page, one complete figure per frame, exactly like Tools/VfxLab/mage_hero.html (the reference for the method and for the design of every character and enemy). Every frame computes the whole figure (body, limbs and what it holds) as ONE label map with one outline; each frame has its own written pose; nothing is tweened or rotated at play time; the same script is run with Node to export the game sheets. Never build or animate from pieces: no limb capsules or tubes, no IK, no per-part polygons, no rigs, no "body + arm and weapon drawn on top". Show the user a test pose before a full sheet. See docs/art-style.md section 3.1.
Texture atlases use Point filtering, no mipmaps, NPOT scale None, uncompressed, maximum size 8192.
Keep .meta files and Packages/packages-lock.json in version control. Never commit Library, Temp, Logs, UserSettings, TestResults, builds or credentials.
Git (user rule 2026-10-27): work, commit and push directly on main; never create separate branches.
Run EditMode and PlayMode tests before publishing changes; inspect logs and XML results, not just the process-launch exit code.
Tools/Validate.ps1 provides extra compile/domain checks and does not replace Unity tests.
Update README.md and docs/port-status.md when migration scope or architecture changes.
