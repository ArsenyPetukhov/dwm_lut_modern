# Windows Build Catalog

Last refreshed: 2026-06-29

This catalog separates four things that must not be mixed together:

- **Windows build label**: the number Microsoft shows, such as `29617.1000`.
- **Architecture**: `amd64` and `arm64` are different native DWM processes.
- **DWM payload identity**: `dwmcore.dll` SHA-256 and CodeView PDB GUID/age are the real compatibility keys.
- **Package support**: whether this repo has a compiled native DLL and profile for that architecture.

## Universal Or Per-Version?

For amd64/x64, this repo now builds one multi-profile DLL. That is the closest safe thing to a universal build: the DLL reads the loaded `dwmcore.dll` PDB GUID/age and selects the matching compiled profile.

It is not universal across CPU architectures. An x64 DLL cannot be loaded into an ARM64 `dwm.exe`. ARM64 needs:

1. ARM64 Visual C++ toolchain installed.
2. ARM64 MinHook/vcpkg dependency build.
3. ARM64 `dwmcore.dll` extraction.
4. ARM64 public PDB resolution.
5. ARM64 RVAs generated into a separate ARM64 profile table.
6. An ARM64 native `dwm_lut.dll`.
7. ARM64 live validation on Windows on Arm hardware or VM.

It is also not universal across unknown future builds. For future builds, the supported path is automated discovery and profile generation, followed by a package rebuild.

## Current Public Build Rows

Generated source: `artifacts\uup\build-catalog.generated.csv`

| Build | Arch | UUP | Support | Profile | Notes |
| --- | --- | --- | --- | --- | --- |
| `19045.7417` | amd64 | yes | x64 fallback | Windows 10 byte-signature fallback | Exact 19045 PDB/RVA profile is not generated yet; GUI manual Win10 signature mode is available. |
| `19045.7417` | arm64 | yes | not built | not profiled | Requires ARM64 native DLL and ARM64 DWM PDB/RVA profile. |
| `26100.8737` | amd64 | yes | x64 fallback | 24H2 byte-signature fallback | Exact PDB profile not yet generated for 26100.8737. |
| `26100.8737` | arm64 | yes | not built | not profiled | Requires ARM64 native DLL and ARM64 DWM PDB/RVA profile. |
| `26200.8737` | amd64 | yes | x64 compiled | `26200.8737_stable` | Exact PDB GUID/age profile compiled. |
| `26200.8737` | arm64 | yes | not built | not profiled | Requires ARM64 native DLL and ARM64 DWM PDB/RVA profile. |
| `26220.8754` | amd64 | yes | x64 compiled | `26220.8754_beta25h2__26300.8758_exp26h2` | DWM payload alias with `26300.8758`. |
| `26220.8754` | arm64 | yes | not built | not profiled | Requires ARM64 native DLL and ARM64 DWM PDB/RVA profile. |
| `26300.8758` | amd64 | yes | x64 compiled | `26220.8754_beta25h2__26300.8758_exp26h2` | DWM payload alias with `26220.8754`. |
| `26300.8758` | arm64 | yes | not built | not profiled | Requires ARM64 native DLL and ARM64 DWM PDB/RVA profile. |
| `28000.2340` | amd64 | yes | x64 compiled | `28000.2340_published26h1` | Distinct PDB identity; same RVAs as 28020/28120 branch. |
| `28000.2340` | arm64 | yes | not built | not profiled | Requires ARM64 native DLL and ARM64 DWM PDB/RVA profile. |
| `28020.2366` | amd64 | yes | x64 compiled | `28020.2366_beta26h1__28120.2374_exp26h1` | Exact PDB GUID/age profile compiled. |
| `28020.2366` | arm64 | yes | not built | not profiled | Requires ARM64 native DLL and ARM64 DWM PDB/RVA profile. |
| `28120.2374` | amd64 | yes | x64 compiled | `28020.2366_beta26h1__28120.2374_exp26h1` | DWM payload alias with `28020.2366`. |
| `28120.2374` | arm64 | yes | not built | not profiled | Requires ARM64 native DLL and ARM64 DWM PDB/RVA profile. |
| `29617.1000` | amd64 | yes | x64 diagnostic | `29617.1000_canary` | Canary profile compiled; live VM validation still required. |
| `29617.1000` | arm64 | yes | not built | not profiled | Requires ARM64 native DLL and ARM64 DWM PDB/RVA profile. |

## Refresh Command

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\update-build-catalog.ps1
```

The script writes:

- `artifacts\uup\build-catalog.generated.csv`
- `artifacts\uup\build-catalog.generated.json`

## Sources

- Microsoft Windows 11 release information: <https://learn.microsoft.com/en-us/windows/release-health/windows11-release-information>
- Microsoft Windows 10 release information: <https://learn.microsoft.com/en-us/windows/release-health/release-information>
- Windows Insider release blog, 2026-06-26: <https://blogs.windows.com/windows-insider/2026/06/26/announcing-new-builds-for-26-june-2026-retail-launch-of-new-wip-improvements/>
- Future Platforms build 29617.1000: <https://learn.microsoft.com/en-us/windows-insider/release-notes/experimental-future-platforms/preview-build-29617-1000>
- UUP dump API search endpoint: <https://api.uupdump.net/listid.php?search=29617.1000&sortByDate=1>
