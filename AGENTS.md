# Agent instructions

Read README.md and docs/port-status.md before changes.
Use Unity 6000.3.6f1. Code identifiers, comments and filenames are English; UI text is Spanish.
Keep Domain and Application free of UnityEngine and other I/O dependencies.
Keep the current pixel-art hero artwork, frame ordering and timing; do not redraw faces or warp atlases.
Texture atlases use Point filtering, no mipmaps, NPOT scale None, uncompressed, maximum size 8192.
Keep .meta files and Packages/packages-lock.json in version control. Never commit Library, Temp, Logs, UserSettings, TestResults, builds or credentials.
Run EditMode and PlayMode tests before publishing changes; inspect logs and XML results, not just the process-launch exit code.
Tools/Validate.ps1 provides extra compile/domain checks and does not replace Unity tests.
Update README.md and docs/port-status.md when migration scope or architecture changes.
