# TaskbarCycler

Windows 11 taskbar utility that cycles windows in grouped taskbar buttons using a global mouse hook
and UI Automation. This is a single-file C# source project for .NET Framework 4.x.

## Build

From a Windows command prompt in this directory:

```bat
build.cmd
```

The script uses the built-in Framework `csc.exe` and WPF UI Automation assemblies. No SDK `.csproj`
is included, so `dotnet build` is not the build command. The output is `TaskbarCycler.exe`.
The undocumented custom icon is omitted; the program has a system-icon fallback.

Portable launch and installation are separate from compilation. `install.cmd` writes startup,
stops a same-name instance, copies files to LocalAppData and starts the application.
Read install/uninstall scripts before use. They were not executed during publication checks.

## Verification

See VALIDATION.md for compile results. Actual grouped-button behavior, Windows/Explorer versions,
global hooks and recovery require interactive verification. No automated tests are present.
