# Windows Cleaner Tool

> A smarter, open-source Windows health & maintenance tool — think CCleaner, but focused on **root-cause diagnostics and safe, reversible fixes** instead of just deleting temp files.

[![CI](https://github.com/bsantacruzms/windows-cleaner/actions/workflows/ci.yml/badge.svg)](https://github.com/bsantacruzms/windows-cleaner/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

**Status:** v0.3 — early but working. Builds clean (0 warnings / 0 errors) with 23 unit
tests passing. A one-click **Clean** button (WPF) and a CLI drive seven health modules
on top of a reversible safety layer. Distributed as a single **portable** `.exe` — no installer.

## Download & install

Windows Cleaner Tool is a **single portable `.exe`** — no installation, no unzipping.
Download **`WindowsCleanerTool-<version>-portable.exe`** from the
**[Releases page](https://github.com/bsantacruzms/windows-cleaner/releases)** and run it.
No prerequisites — the .NET runtime and all dependencies are bundled into the one file
(it self-extracts to a temp folder on first launch), so it works on a clean Windows install.

Just click **Clean** and the tool scans and repairs everything automatically. The header
shows the app version and your Windows version; if your Windows build is unsupported it
warns you before running.

> The app requests administrator rights (UAC) on launch, since repairs touch system
> components. Windows 10 version 2004 (build 19041) or newer is required.

New versions are published to the Releases page automatically whenever a `v*` tag is
pushed (see [`.github/workflows/release.yml`](.github/workflows/release.yml)).

### Build it yourself

```powershell
powershell -ExecutionPolicy Bypass -File scripts/build-release.ps1                 # -> dist/*.exe
powershell -ExecutionPolicy Bypass -File scripts/build-release.ps1 -Version 0.3.0
```

## Why another cleaner?

Most "cleaners" only delete caches. The hard problems — a Microsoft Store add-on stuck
with error `0x80070003`, orphaned AppX packages, broken Gaming Services registrations,
a corrupted Windows Update queue — need **diagnosis**, not a broom.

Windows Cleaner Tool is built around a simple loop that mirrors how a good technician works:

1. **Detect** — read event logs, package state, registry, services.
2. **Diagnose** — explain the actual root cause in plain language.
3. **Back up** — create a System Restore point and export affected registry keys/files.
4. **Fix** — apply the smallest reversible change.
5. **Verify** — re-check that the issue is gone.

Every fix is reversible, supports a **dry-run** preview, and is logged.

## Health modules (v0.3)

| Module | What it does |
| ------ | ------------ |
| **Store / AppX Repair** | Finds orphaned/half-registered AppX packages (null install location, missing folders) and stale Gaming Services entries, then removes them safely. |
| **Temp & Cache Cleanup** | Reports and reclaims space from user/Windows temp, Store cache, Delivery Optimization, thumbnails. |
| **Windows Update Reset** | Stops update services, resets `SoftwareDistribution`, clears the stuck download queue. |
| **System Integrity** | Runs `DISM /RestoreHealth` and `SFC /scannow` and surfaces the results. |
| **Startup & Services** | Lists startup entries and heavy/optional services; lets you disable them (with backup). |
| **Privacy Cleanup** | Reviews common telemetry/privacy switches and hardens them on request. |
| **Drivers (official)** | Detects your motherboard/system and any problem devices, then links to the **manufacturer's official** driver page and Windows Update. Never installs third-party drivers. |

## Architecture

```
src/
  WindowsCleaner.Core/   Engine: modules, models, safety layer, process runner
  WindowsCleaner.App/    WPF desktop dashboard (single-file portable) — runs elevated
  WindowsCleaner.Cli/    Scriptable command-line runner (scan / fix / clean)
tests/
  WindowsCleaner.Core.Tests/   Unit tests for the engine
```

The engine is UI-agnostic: every capability is an `IHealthModule` that can `ScanAsync`
and `FixAsync`. Both the desktop app and the CLI consume the exact same modules.

## Requirements

- Windows 10 (build 19041+) or Windows 11
- .NET 8 SDK (to build)
- Administrator rights (required for most repairs)

## Build & run

```powershell
# Build everything
dotnet build

# Run the CLI (scan only, safe)
dotnet run --project src/WindowsCleaner.Cli -- scan

# Preview fixes without changing anything
dotnet run --project src/WindowsCleaner.Cli -- fix --all --dry-run

# Run the desktop app
dotnet run --project src/WindowsCleaner.App
```

> The desktop app ships with a `requireAdministrator` manifest, so Windows prompts for
> elevation (UAC) when you launch it — repairs need administrator rights.

### CLI reference

```
wclean clean [--dry-run]                Scan and auto-fix everything (one-click)
wclean scan                             Scan and print a health report
wclean fix --all [--dry-run]            Fix every fixable issue
wclean fix --module <id> [--dry-run]    Fix issues from a single module
```

Module ids: `store-appx`, `temp-cleanup`, `windows-update`, `system-integrity`,
`startup`, `privacy`, `drivers`.

Example scan output:

```
Health score: 80/100 (Good)
Issues: 14   Reclaimable: 529.2 MB

- Store / AppX Repair: OK
- Temp & Cache Cleanup: 2 issue(s)
    [Low] User temp files: 521.2 MB
    [Low] Microsoft Store cache: 8.1 MB
- Windows Update Reset: OK
- System Integrity (SFC/DISM): 1 issue(s)
- Startup & Services: 10 issue(s)
- Privacy Cleanup: 1 issue(s)
```

## Safety

- A **System Restore point** is created before the first fix in a session.
- Registry keys are exported to `artifacts/backups/` before deletion.
- `--dry-run` shows exactly what would change.
- Nothing is deleted outside well-known, documented locations.

## Development

```powershell
# Run the unit tests
dotnet test tests/WindowsCleaner.Core.Tests

# Build a single project
dotnet build src/WindowsCleaner.Core/WindowsCleaner.Core.csproj
```

Coding conventions live in [`.editorconfig`](.editorconfig): file-scoped namespaces,
nullable reference types and `_camelCase` private fields.

> If the repository lives on a mapped network drive, Git may report *dubious ownership*.
> Clear it with `git config --global --add safe.directory '<path>'`.

## Contributing

Issues and PRs welcome. New capabilities are just new `IHealthModule` implementations —
see [`src/WindowsCleaner.Core/Modules`](src/WindowsCleaner.Core/Modules). Register the
module in `DefaultModules.CreateAll` and it shows up in both the app and the CLI.

## License

[MIT](LICENSE) © 2026 Brian Santacruz
