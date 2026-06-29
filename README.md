# DWM LUT Modern

System-wide 3D LUT correction for modern Windows 10/11 SDR/HDR workflows.

This is a maintained Windows 11 fork of [`lauralex/dwm_lut`](https://github.com/lauralex/dwm_lut), built from the practical 25H2 work in [`zkippp/dwm_lut_fixed`](https://github.com/zkippp/dwm_lut_fixed) and extended with profile-driven DWM symbol selection for current Windows releases.

## Why This Exists

DWM LUT applies 3D LUTs to the Windows desktop by hooking into DWM. That is useful when a display lacks hardware LUT calibration but you still want a practical system-wide calibration path:

Colorimeter -> DisplayCAL/ArgyllCMS -> 3D LUT -> DWM LUT -> Windows desktop correction.

Windows DWM internals are private and change across Windows updates. This fork treats compatibility as an engineering process: public-symbol profiles, logged hook selection, reproducible packages, and explicit compatibility notes.

## Current Compatibility

| Windows build | Channel | Status |
| ---: | --- | --- |
| `19045.7417` | Windows 10 22H2 | Fallback engine, exact profile pending |
| `26200.8655` | Current test machine | Built |
| `26200.8737` | Windows 11 25H2 Stable Preview | Built, needs live validation |
| `26220.8754` | Windows Insider Beta 25H2 | Built, DWM payload alias with `26300.8758` |
| `26300.8758` | Windows Insider Experimental 26H2 | Built through exact DWM payload alias |
| `28000.2340` | Published Windows 11 26H1 | Built |
| `28020.2366` | Windows Insider Beta 26H1 | Built |
| `28120.2374` | Windows Insider Experimental 26H1 | Built through exact DWM payload alias |
| `29617.1000` | Canary / Future Platforms | Built, experimental |

See [docs/compatibility-matrix.md](docs/compatibility-matrix.md) for the full matrix and source evidence.
See [docs/build-catalog.md](docs/build-catalog.md) for the amd64/ARM64 build catalog and support status.
See [docs/universal-build-strategy.md](docs/universal-build-strategy.md) for why this is a multi-profile x64 build, not a magic single binary for every future DWM.
See [docs/engineering/fix-all-technical-guide.md](docs/engineering/fix-all-technical-guide.md) for the current maintainer-level porting, logging, Canary, HDR, and multi-monitor guide.
See [docs/research/legacy-universal-support.md](docs/research/legacy-universal-support.md) for the Windows 10 through Canary auto-detection plan based on old GitHub forks and diffs.

## Download / Local Packages

The current multi-profile x64 app is staged here:

```text
dist\universal-win-x64\DwmLutGUI.exe
```

The matching portable zip is:

```text
dist\dwm_lut_universal-win-x64.zip
```

This is "universal" across the compiled x64 DWM profiles and fallback engines in this repo. It is not a single-file app and it is not an ARM64 build. Keep `DwmLutGUI.exe`, `dwm_lut.dll`, `WindowsDisplayAPI.dll`, and `DwmLutGUI.exe.config` together in the same folder.

Per-build matrix artifacts are staged under:

`artifacts/packages/build-matrix`

Each per-build zip contains:

- `DwmLutGUI.exe`
- `dwm_lut.dll`
- profile table
- PDBs for diagnostics
- `README_INSTALL.md`

## Quick Start

1. Pick the package matching your Windows build.
2. Extract it.
3. Run `DwmLutGUI.exe` as Administrator.
4. Assign an SDR `.cube` LUT, and optionally an HDR `.cube` LUT.
5. Leave Compatibility on `Auto-detect (recommended)` unless testing a specific Windows build family.
6. Click Apply.
7. Check `%SystemRoot%\Temp\dwm_lut.log`.
8. Click Disable before changing package files or LUT files.

## Logging

The DLL writes timestamped diagnostic logs to:

`%SystemRoot%\Temp\dwm_lut.log`

The GUI stages monitor metadata as `manifest.tsv` and compatibility selection as `resolver.cfg` during injection. The DLL copies the useful parts into the log before the GUI removes the staging folder.

## Building

```powershell
.\scripts\build-release.ps1 -Platform x64
```

Packaging:

```powershell
.\scripts\package-release.ps1 -Version 4.0.0-modern-windows
.\scripts\package-build-matrix.ps1 -Version 4.0.0-modern-windows -Platform x64
```

Programmatic checks:

```powershell
.\scripts\test-build.ps1
```

Refresh the public amd64/ARM64 build catalog:

```powershell
.\scripts\update-build-catalog.ps1
```

Refresh mirrored legacy GitHub references and generated research manifests:

```powershell
.\scripts\update-legacy-references.ps1 -IncludeForks
```

See [BUILDING.md](BUILDING.md) for the full local build and package flow.

## Security And Risk

This project injects a DLL into `dwm.exe` and hooks private Windows compositor functions. That is inherently risky and unsupported by Microsoft. Read [SECURITY.md](SECURITY.md) and [docs/known-issues.md](docs/known-issues.md) before using it on a production machine.

## Credits

Original project: [`lauralex/dwm_lut`](https://github.com/lauralex/dwm_lut)  
25H2 fork base: [`zkippp/dwm_lut_fixed`](https://github.com/zkippp/dwm_lut_fixed)

This repository keeps the GPL-3.0 license and upstream attribution intact.
