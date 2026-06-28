# Universality And Build Catalog Report

Date: 2026-06-29

## Answer

No, one binary cannot literally cover every future Windows build and every CPU architecture.

Yes, one x64 package can cover multiple known amd64 Windows builds if it contains a table of DWM profiles and chooses the profile by `dwmcore.dll` PDB GUID/age. That is what this repo now does.

ARM64 requires a separate native DLL and separate ARM64 profiles. Unknown future builds require profile ingestion after Microsoft publishes the build payload and symbols.

## What Was Added In This Pass

- Added `scripts\update-build-catalog.ps1`.
- Generated `artifacts\uup\build-catalog.generated.csv`.
- Generated `artifacts\uup\build-catalog.generated.json`.
- Added `docs\build-catalog.md`.
- Added `docs\universal-build-strategy.md`.
- Added `docs\arm64-roadmap.md`.
- Added package payload copies of the catalog, universal strategy, and ARM64 roadmap.
- Added a `26100.8737_24H2-fallback` matrix package.
- Extended the smoke test to assert ARM64 build catalog rows are present.

## Tested Result

```text
dwm_lut.dll SHA256: 4A0B8EA231A93E919E39D33545249BC37426AE365867EFEFF05386276ABA7725
Programmatic build checks passed.
```

Normal x64 zip:

```text
C:\Users\arsen\Documents\Code\DWM LUT\dwm_lut_modern\dist\4.0.0-modern-windows\dwm_lut_modern-4.0.0-modern-windows-win-x64.zip
SHA256: A92FBCE8861BC0114121A8E8D95824555AEF2D6F36F038E676E4EB35F806F26F
```

Stable local x64 alias:

```text
C:\Users\arsen\Documents\Code\DWM LUT\dwm_lut_modern\dist\universal-win-x64\DwmLutGUI.exe
C:\Users\arsen\Documents\Code\DWM LUT\dwm_lut_modern\dist\dwm_lut_universal-win-x64.zip
SHA256: A92FBCE8861BC0114121A8E8D95824555AEF2D6F36F038E676E4EB35F806F26F
```

## Current Build Catalog

The generated catalog covers the current public set I could verify through UUP:

| Build | amd64 status | ARM64 status |
| --- | --- | --- |
| `26100.8737` | fallback only | discoverable, not built |
| `26200.8737` | compiled profile | discoverable, not built |
| `26220.8754` | compiled profile | discoverable, not built |
| `26300.8758` | compiled alias | discoverable, not built |
| `28000.2340` | compiled profile | discoverable, not built |
| `28020.2366` | compiled profile | discoverable, not built |
| `28120.2374` | compiled alias | discoverable, not built |
| `29617.1000` | compiled diagnostic Canary profile | discoverable, not built |

Full generated CSV:

```text
C:\Users\arsen\Documents\Code\DWM LUT\dwm_lut_modern\artifacts\uup\build-catalog.generated.csv
```

## Why ARM64 Is Not Done Yet

The repo now has ARM64 solution/project/script plumbing, but this machine still has no usable `Hostx64\arm64` C++ compiler directory. A quiet Visual Studio Installer attempt from this non-elevated Codex process failed with exit code `5007`; the installer log says quiet/passive modification must start elevated. A later elevated attempt failed with exit code `8006` / `VSProcessesRunning`; an idle reusable `MSBuild.exe` node was then stopped. More importantly, an ARM64 package needs ARM64 `dwmcore.dll` extraction and ARM64 PDB/RVA profiles. The x64 RVAs are not safe to reuse on ARM64.

Elevated install command:

```powershell
& "C:\Program Files (x86)\Microsoft Visual Studio\Installer\setup.exe" modify --installPath "C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools" --add Microsoft.VisualStudio.Component.VC.Tools.ARM64 --quiet --norestart
```

The first correct ARM64 target is Canary/Future Platforms `29617.1000` ARM64:

```text
UUP UUID: ca767acf-e249-40c2-a5e8-974ff7f77ffd
```

## Sources

- Windows release information: <https://learn.microsoft.com/en-us/windows/release-health/windows11-release-information>
- Windows Insider release blog, 2026-06-26: <https://blogs.windows.com/windows-insider/2026/06/26/announcing-new-builds-for-26-june-2026-retail-launch-of-new-wip-improvements/>
- Future Platforms build 29617.1000: <https://learn.microsoft.com/en-us/windows-insider/release-notes/experimental-future-platforms/preview-build-29617-1000>
- UUP dump API search for Canary ARM64: <https://api.uupdump.net/listid.php?search=29617.1000&sortByDate=1>
