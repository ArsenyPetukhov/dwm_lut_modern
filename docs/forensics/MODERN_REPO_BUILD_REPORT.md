# DWM LUT Modern Build Report

Date: 2026-06-29  
Repo path: `C:\Users\arsen\Documents\Code\DWM LUT\dwm_lut_modern`  
Package version: `4.0.0-modern-windows`

## Executive Summary

This repo is now structured as a maintained forward-port instead of a loose fork checkout. It builds a single multi-profile `dwm_lut.dll` that selects its DWM hook profile from the loaded `dwmcore.dll` CodeView PDB GUID/age. That is the correct discriminator because Windows build numbers and Insider labels can reuse the same DWM payload, while cumulative updates can also replace DWM without changing the marketing version in a helpful way.

The current native DLL was rebuilt and smoke-tested:

```text
dwm_lut.dll SHA256: 4A0B8EA231A93E919E39D33545249BC37426AE365867EFEFF05386276ABA7725
Programmatic build checks passed.
```

The normal portable release zip is:

```text
C:\Users\arsen\Documents\Code\DWM LUT\dwm_lut_modern\dist\4.0.0-modern-windows\dwm_lut_modern-4.0.0-modern-windows-win-x64.zip
SHA256: 4B9013C5813831903786BA575569BA97829105ED9374DE23BCED9EFA44AEFFD3
```

## What Changed

- Created a maintained repo skeleton with `src`, `docs`, `scripts`, `tests`, `examples`, `.github`, and `artifacts`.
- Moved prior forensic reports into `docs\forensics`.
- Moved generated DWM profile provenance into `artifacts\profiles`.
- Moved UUP metadata snapshots into `artifacts\uup`.
- Added exact multi-profile DWM identity table and generated header.
- Added architecture-tagged profiles and runtime architecture checks.
- Added GUI-side PE machine validation before injection.
- Added ARM64 project configurations and platform-aware build/package scripts.
- Added Release build, smoke-test, checksum, normal package, and per-build matrix package scripts.
- Added GitHub issue templates, workflows, stale config, discussion metadata, README, security, support, roadmap, contributing, compatibility, HDR, DisplayCAL, validation, troubleshooting, and release docs.
- Removed a stale copied GUI `build_log.txt`.

## Runtime Engine

The GUI and DLL cooperate through a short-lived staging area:

1. The GUI copies `dwm_lut.dll` to `%SystemRoot%\Temp\dwm_lut.dll`.
2. The GUI creates `%SystemRoot%\Temp\luts`.
3. The GUI copies selected SDR/HDR `.cube` LUTs into that folder.
4. The GUI writes `%SystemRoot%\Temp\luts\manifest.tsv`.
5. The GUI injects the DLL into `dwm.exe` by remote `LoadLibraryA`.
6. The DLL opens the active `dwmcore.dll`, reads its CodeView PDB GUID/age, and selects a matching RVA profile.
7. The DLL hooks `COverlayContext::Present` and available flip/overlay suppression functions.
8. The DLL runs the LUT shader over the compositor backbuffer.
9. The GUI deletes the staging folder after injection returns; the DLL has already loaded LUT data and logged the manifest.

The durable runtime log is:

```text
%SystemRoot%\Temp\dwm_lut.log
```

The log now includes timestamp, process ID, thread ID, selected DWM profile, hook addresses, staged monitor manifest, loaded LUT filenames, LUT coordinates, HDR/SDR mode, and fallback choices.

## Multi-Monitor Work

This build fixes one real config bug and adds safer diagnostics:

- The GUI now persists `target_id`, `source_id`, `name`, `connector`, and `position` in `config.xml`.
- The loader now resolves saved monitor settings by path first, then target ID, then source ID, then legacy `id`.
- The injection manifest now includes `position`, `source_id`, `target_id`, `device_path`, `name`, `connector`, `sdr_lut`, and `hdr_lut`.
- The DLL stores the source filename for every parsed LUT and logs each loaded LUT.
- Single-monitor mode uses a fast path, so your setup does not depend on fragile DWM private coordinates.
- Multi-monitor mode still uses coordinate identity for the actual DWM context match, but it now has exact match, tight nearest-position fallback, HDR/SDR fallback logging, and a complete GUI topology manifest in the log.

Important limitation: true robust multi-monitor support still requires deriving a stable display identity from DWM internals, likely near `CSwapChainRealization::GetDisplayId` or adjacent object state. The current build is substantially more diagnosable and safer, but it does not yet prove source-ID-to-DWM-context matching.

## Compiled DWM Profiles

| Profile | Status | Key risk |
| --- | --- | --- |
| `26200.8655_current` | Built and matched to this machine profile | Current-machine validation still depends on live LUT test |
| `26200.8737_stable` | Built | Needs live validation on KB5095093 |
| `26220.8754_beta25h2__26300.8758_exp26h2` | Built | Two Windows labels share one payload |
| `28000.2340_published26h1` | Built | New branch; needs live validation |
| `28020.2366_beta26h1__28120.2374_exp26h1` | Built | `OverlaysEnabled` missing; relies on direct overlay-test global |
| `29617.1000_canary` | Built, diagnostic | `OverlaysEnabled` missing; backbuffer path must be live-tested |

Full profile table: `artifacts\profiles\dwm_profiles_table.md`.

## Package Matrix

All per-build packages were regenerated from the same tested binaries:

| Zip | Size | SHA-256 |
| --- | ---: | --- |
| `dwm_lut_modern-4.0.0-modern-windows-win-x64-26100.8737_24H2-fallback.zip` | 1,056,576 | `BD97337C09863BE766FC5C91918201CAF37229A616B11EE85BAB05E3C4B8F5A2` |
| `dwm_lut_modern-4.0.0-modern-windows-win-x64-26200.8655_current-machine.zip` | 1,056,581 | `9EE159D350A387D686E453F4921093CCCB32E382FDA170C0A977B7403CEA0C9C` |
| `dwm_lut_modern-4.0.0-modern-windows-win-x64-26200.8737_stable-25H2-KB5095093.zip` | 1,056,585 | `39772DB2D157010801BCE3CC740E7EF3788821CF8F80995379C852396B179A02` |
| `dwm_lut_modern-4.0.0-modern-windows-win-x64-26220.8754_beta-25H2.zip` | 1,056,575 | `8C7DD5BE1EF6E9D9F67C9A0C2C23A2C0B094F78A9B23E16241CA0A62750D2D29` |
| `dwm_lut_modern-4.0.0-modern-windows-win-x64-26300.8758_experimental-26H2.zip` | 1,056,577 | `3941E312CF22C03ED2ED776626648377020C8E1209B6D260029A706B35EAF7BF` |
| `dwm_lut_modern-4.0.0-modern-windows-win-x64-28000.2340_published-26H1.zip` | 1,056,574 | `0D46F372473AD5625F34B87CA261A929D3F30DCBA34DE202A31E15187B3A5453` |
| `dwm_lut_modern-4.0.0-modern-windows-win-x64-28020.2366_beta-26H1.zip` | 1,056,574 | `3D231B0CB433726A18BA0A4B2725F9AAA40214A02005E332B02E25E454BB5EBD` |
| `dwm_lut_modern-4.0.0-modern-windows-win-x64-28120.2374_experimental-26H1.zip` | 1,056,577 | `9ACBEF76B9F20B9B7D8CE42FD18621149C8C3F327E3E60B0888F41701F986C68` |
| `dwm_lut_modern-4.0.0-modern-windows-win-x64-29617.1000_canary-future-platforms.zip` | 1,056,589 | `6B4AA941F9D8749452113ADD2935F4084895EEE6CB7700E106507766913C7FF9` |

Matrix folder:

```text
C:\Users\arsen\Documents\Code\DWM LUT\dwm_lut_modern\artifacts\packages\build-matrix\4.0.0-modern-windows
```

## Verification Performed

Command:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\test-build.ps1 -Platform x64
```

Result:

- MSBuild Release build completed.
- GUI output exists.
- Native DLL exists in GUI output.
- Profile header contains stable 25H2, published 26H1, and Canary profile identifiers.
- `dwm_lut.dll` hash recorded.
- Programmatic smoke checks passed.

This is a build/package/provenance test. It does not replace live DWM injection validation on each Windows build.

ARM64 project and script support is present, but this machine does not currently have the MSVC ARM64 C++ toolchain installed and no ARM64 `dwmcore.dll` RVA profiles have been generated yet. The ARM64 build path fails early with a clear toolchain error instead of producing a misleading package.

## How To Install On Another PC

1. Pick the normal release zip unless you intentionally want the build-labeled folder.
2. Extract it.
3. Run `DwmLutGUI.exe` as administrator.
4. Choose your SDR LUT. Choose an HDR LUT only if your calibrator produced an HDR-domain LUT.
5. Press Apply.
6. If it fails or the image does not change, open `%SystemRoot%\Temp\dwm_lut.log`.
7. Look for `Selected DWM symbol profile`, `Loaded LUT`, and `DWM HOOK DLL INITIALIZATION`.

The package folder name does not force a profile. The DLL always picks by the real `dwmcore.dll` PDB identity on the target system.

## Canary Pathway

The Canary profile is compiled, but it is still a diagnostic profile. The PDB-derived RVAs are enough to hook known functions, but the high-risk unknown is whether the old 25H2-style backbuffer retrieval path still yields a valid `ID3D11Texture2D` under live Canary DWM.

Recommended VM path:

1. Install the target Canary build in a VM or secondary boot.
2. Disable snapshots only after capturing a clean baseline snapshot.
3. Use an identity LUT first.
4. Apply a deliberately obvious SDR test LUT.
5. Inspect `%SystemRoot%\Temp\dwm_lut.log`.
6. If the profile selects but no color change occurs, instrument the swapchain/backbuffer retrieval path next.
7. If DWM crashes, stop using that profile and diff `dwmcore.dll` PDB symbols against the last known working build.

## What To Fix Next

1. Implement a symbol-driven DWM display identity mapper and stop using coordinates as the primary multi-monitor key.
2. Add a DWM-safe diagnostic mode that logs swapchain vtable addresses and texture descriptors before shader application.
3. Add automated PDB profile generation from public symbols so future Insider builds become a repeatable pipeline.
4. Add signed release artifacts.
5. Add a minimal LUT parser unit test harness outside DWM.
6. Add a VM validation checklist per profile with pass/fail evidence.

## Web Sources Used

- Microsoft Windows 11 release information: <https://learn.microsoft.com/en-us/windows/release-health/windows11-release-information>
- KB5095093 release notes: <https://support.microsoft.com/en-us/topic/june-23-2026-kb5095093-os-builds-26200-8737-and-26100-8737-preview-0e2a20f2-cf9e-46f8-9f08-e6996220882d>
- Windows Insider build announcement, 2026-06-26: <https://blogs.windows.com/windows-insider/2026/06/26/announcing-new-builds-for-26-june-2026-retail-launch-of-new-wip-improvements/>
- Future Platforms preview build 29617.1000: <https://learn.microsoft.com/en-us/windows-insider/release-notes/experimental-future-platforms/preview-build-29617-1000>
- Microsoft public symbols overview: <https://learn.microsoft.com/en-us/windows-hardware/drivers/debugger/microsoft-public-symbols>
- DbgHelp `SymInitialize`: <https://learn.microsoft.com/en-us/windows/win32/api/dbghelp/nf-dbghelp-syminitialize>
- DIA SDK overview: <https://learn.microsoft.com/en-us/visualstudio/debugger/debug-interface-access/debug-interface-access-sdk>
- GitHub issue forms: <https://docs.github.com/en/issues/tracking-your-work-with-issues/configuring-issues/configuring-issue-templates-for-your-repository>
- GitHub Actions artifacts: <https://docs.github.com/en/actions/using-workflows/storing-workflow-data-as-artifacts>
