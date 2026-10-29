# Publication verification — 2026-10-29

Fresh source snapshot; original installation, user settings, processes and startup entries untouched.

PASSED — `cmd /d /c build.cmd`: exit 0; 0 warnings; no compilation errors.
No installer, uninstaller, portable app or global input/filter hook was executed for verification.
NOT RUN — interactive behavior, startup installation, uninstall/recovery and application compatibility.
No automated test suite is present; this result establishes compilation only.

Only reviewed source files are committed. Build output, user settings, caches and undocumented icons
are omitted (ScreenDarkFilter retains its locally generated icon with the generator source).
DashTray's public defaults use neutral snippets and opt-in Windows startup; its publish helper checks
native build exit status. The original working installation keeps its existing configuration.
