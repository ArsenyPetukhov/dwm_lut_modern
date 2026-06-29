# DWM LUT Modern Fix-All Technical Guide

Date: 2026-06-29  
Repository: <https://github.com/ArsenyPetukhov/dwm_lut_modern>  
Local root: `C:\Users\arsen\Documents\Code\DWM LUT\dwm_lut_modern`

This document is the current senior-maintainer guide for the forward port. It records what is actually implemented in this repository, what was learned from the 25H2 fork, what is still experimental, and the precise procedure for extending the tool to future Windows DWM builds.

## Executive Position

The honest answer is: there is no literal universal DWM LUT executable that can be guaranteed to work on every future Windows build and every CPU architecture.

There can be a practical multi-profile x64 app that supports every known x64 DWM payload we have profiled. This repo now does that by selecting DWM hook offsets from the loaded `dwmcore.dll` PDB GUID/age instead of from a vague Windows marketing version. That is the right identity key because cumulative updates and Insider rings can reuse the same DWM binary across different visible build labels, while different DWM binaries can exist inside the same nominal Windows generation.

ARM64 cannot reuse x64 offsets. ARM64 needs its own native DLL and its own ARM64 `dwmcore.dll` PDB profiles. Canary and future builds need new profiles after Microsoft publishes matching payloads and public symbols.

## What Is Built Today

The current x64 package is a multi-profile build, not a single-build hack. The compiled profile source is `artifacts/profiles/compiled_dwm_profiles.json`, and the generated C++ header is `src/lutdwm/DwmProfiles.generated.h`.

Compiled x64 profiles:

| Profile | DWM status | Notes |
| --- | --- | --- |
| `26200.8655_current` | Built | Local machine payload used during development. |
| `26200.8737_stable` | Built | 25H2 stable preview profile. |
| `26220.8754_beta25h2__26300.8758_exp26h2` | Built | Shared DWM payload across those two known labels. |
| `28000.2340_published26h1` | Built | Published 26H1 profile. |
| `28020.2366_beta26h1__28120.2374_exp26h1` | Built | Shared Insider branch payload. |
| `29617.1000_canary` | Built, experimental | Canary/Future Platforms diagnostic profile. |

The current package output after `scripts/package-release.ps1` is:

```text
dist\4.0.0-modern-windows\dwm_lut_modern-4.0.0-modern-windows-win-x64.zip
dist\universal-win-x64\DwmLutGUI.exe
dist\dwm_lut_universal-win-x64.zip
```

If an existing GUI process locks `dist\universal-win-x64`, the package script writes a side-by-side alias:

```text
dist\universal-win-x64-next\DwmLutGUI.exe
dist\dwm_lut_universal-win-x64-next.zip
```

## Why The Old App Failed On New Windows

The original `lauralex/dwm_lut` design works by injecting a native DLL into `dwm.exe`, finding private compositor functions in `dwmcore.dll`, and drawing a LUT-applied shader pass over the desktop compositor backbuffer. That design is powerful, but every important target is private implementation detail:

- private class names such as `COverlayContext`;
- private function RVAs;
- private object layout and vtable assumptions;
- compositor overlay / DirectFlip / MPO behavior;
- DWM monitor coordinate reporting;
- backbuffer retrieval through internal swapchain objects.

Windows 24H2, 25H2, 26H1, and Canary builds moved enough of those internals that a fixed byte-pattern or fixed-offset approach is not maintainable. The key breakages are:

1. Hook offsets change by DWM payload, not by app version.
2. Some newer profiles no longer expose `COverlayContext::OverlaysEnabled` in public symbols.
3. DirectFlip/MPO compatibility can bypass the composition path that the LUT shader expects.
4. The old backbuffer retrieval path can fail if the internal object chain changes.
5. Multi-monitor coordinate matching is fragile when DWM reports private rectangles that do not exactly match WindowsDisplayAPI desktop positions.
6. Old diagnostics were too thin; failures looked like "nothing happens" instead of pointing to injection, hook setup, LUT parse, or texture retrieval.

## What The 25H2 Fork Contributed

The repository `0x401gg/dwm_lut-windows-25h2` was reviewed as a reality check: <https://github.com/0x401gg/dwm_lut-windows-25h2>.

Useful ideas found there:

- 25H2-specific hook work exists and is not purely speculative.
- The fork added diagnostic logging around the 25H2 path.
- It introduced a single-LUT fallback, which is important for a single-monitor user because monitor-coordinate identity can be ignored.
- It included work around monitor coordinate lookup.
- Later history emphasized verified 23H2 behavior more than broad verified 25H2/Canary behavior, which supports the reading that 25H2 code existed but was not proven stable across the full Windows matrix.

What this repo took from that reading:

- keep 25H2 support, but make it profile-driven;
- make single-monitor selection deterministic and boring;
- log enough detail to separate injection failure from hook/profile failure;
- treat Canary as a diagnostic profile until a live VM or secondary install proves the backbuffer path.

## Implemented Fixes In This Repo

### GUI Config And Identity Handling

Commit: `c8e9542 Harden GUI config and package identity handling`

Changes:

- fixed HDR LUT persistence so `hdr_luts` survives config save/load;
- handled XML parse failures instead of letting corrupt config crash the GUI;
- wrote config atomically through a temporary file and replacement;
- changed the single-instance lock from process-name based to package-path based, so side-by-side builds can run independently;
- removed the stale external update check that pointed at a different fork;
- updated project metadata links.

Why it matters:

The GUI is the operator interface. If it silently drops HDR LUT config or refuses to launch a new package because an older package is open, the user sees the native engine as broken even when the DLL is fine.

### Injection Diagnostics

Commit: `be7017d Add detailed injector diagnostics`

Changes:

- added `%SystemRoot%\Temp\dwm_lut_gui.log`;
- added exact Win32 error logging for privilege setup, `OpenProcess`, `VirtualAllocEx`, `WriteProcessMemory`, `CreateRemoteThread`, `WaitForSingleObject`, `GetExitCodeThread`, `CreateFile`, and `SetSecurityInfo`;
- used a null-terminated DLL path for the remote `LoadLibraryA` call;
- validated `.txt` LUT conversion size and rows.

Why it matters:

`CreateRemoteThread` injection fails for many mundane reasons: wrong elevation, wrong path, access denied, bad allocation, missing DLL, or DWM restart timing. The GUI now logs those separately.

Reference docs:

- `CreateRemoteThread`: <https://learn.microsoft.com/en-us/windows/win32/api/processthreadsapi/nf-processthreadsapi-createremotethread>
- `VirtualAllocEx`: <https://learn.microsoft.com/en-us/windows/win32/api/memoryapi/nf-memoryapi-virtualallocex>
- `WriteProcessMemory`: <https://learn.microsoft.com/en-us/windows/win32/api/memoryapi/nf-memoryapi-writeprocessmemory>
- `OpenProcess`: <https://learn.microsoft.com/en-us/windows/win32/api/processthreadsapi/nf-processthreadsapi-openprocess>

### Native LUT Parser Hardening

Commit: `2ed7506 Harden native LUT parsing`

Changes:

- removed unsafe resize macro usage from LUT parsing and active-target tables;
- bounded `.cube` `LUT_3D_SIZE` to a sane range;
- added overflow checks for LUT allocation;
- delayed global LUT-table mutation until a candidate LUT parses successfully;
- freed candidate memory on failure;
- logged parse failure causes.

Why it matters:

A malformed `.cube` should fail the LUT load, not corrupt DWM process memory. This is especially important because the parser runs inside `dwm.exe`.

### Private Pointer Probe Hardening

Commit: `0836ea1 Guard DWM private pointer probes`

Changes:

- added SEH-guarded pointer reads;
- replaced the old `IsBadReadPtr` style approach with explicit safe read helpers;
- validated dirty rectangle vectors once per hook entry;
- guarded legacy and 24H2 swapchain reads.

Why it matters:

Microsoft explicitly warns that `IsBadReadPtr` is obsolete and should not be used for validation. In this project, bad private DWM assumptions should produce a log line and no LUT, not an access violation in `dwm.exe`.

Reference docs:

- `IsBadReadPtr` warning: <https://learn.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-isbadreadptr>
- Structured exception handling: <https://learn.microsoft.com/en-us/cpp/cpp/structured-exception-handling-c-cpp>

### Fail-Closed Hook Startup

Commit: `0234c0c Fail closed on DWM hook startup errors`

Changes:

- validated `dwmcore.dll` module handle and image size;
- prevented signature-scan underflow;
- logged MinHook status strings through `MH_StatusToString`;
- made the primary Present hook required;
- kept helper hooks optional but logged their failures;
- removed the old detach-time sleep;
- guarded overlay test mode restore.

Why it matters:

The DLL must not half-install a hook set and continue as if it succeeded. A failed required hook should leave clear diagnostics and abort initialization.

Reference docs:

- DLL best practices and loader-lock restrictions: <https://learn.microsoft.com/en-us/windows/win32/dlls/dynamic-link-library-best-practices>
- MinHook upstream: <https://github.com/TsudaKageyu/minhook>

### Deterministic DWM Profile Generation

Commit: `ae74e6a Add deterministic DWM profile generation`

Changes:

- added `artifacts/profiles/compiled_dwm_profiles.json` as the editable source of profile truth;
- added `scripts/generate-dwm-profile-header.ps1`;
- generated `src/lutdwm/DwmProfiles.generated.h`;
- generated `artifacts/profiles/dwm_profiles_table.md`;
- made `scripts/test-build.ps1` fail if generated files are stale.

Why it matters:

Before this, profile data could drift between docs and code. Now the build asserts that the C++ profile table and Markdown table both come from the same JSON.

### Warning-Free Native Release Build

Commit: `d85eb88 Clean native release build warnings`

Changes:

- made GPU coordinate conversions explicit;
- made dither-noise conversion explicit;
- aligned x64/ARM64 native project runtime library settings with static vcpkg linkage;
- verified `scripts/test-build.ps1 -Platform x64` with zero warnings and zero errors.

Why it matters:

Warnings in a DWM injection project are not noise. A clean build makes future compiler warnings meaningful. Aligning `/MT` with the static dependency package also removes the `LIBCMT` conflict and improves portable DLL deployment.

Reference docs:

- MSVC runtime library options: <https://learn.microsoft.com/en-us/cpp/build/reference/md-mt-ld-use-run-time-library>
- Linker warning LNK4098: <https://learn.microsoft.com/en-us/cpp/error-messages/tool-errors/linker-tools-warning-lnk4098>
- vcpkg triplets: <https://learn.microsoft.com/en-us/vcpkg/users/triplets>

## Runtime Engine Explained

At runtime, the system is:

```text
DwmLutGUI.exe
  -> enumerates monitors through WindowsDisplayAPI
  -> saves/loads per-monitor SDR/HDR LUT paths
  -> stages LUT files in %SystemRoot%\Temp\luts
  -> writes %SystemRoot%\Temp\luts\manifest.tsv
  -> copies dwm_lut.dll to %SystemRoot%\Temp\dwm_lut.dll
  -> injects into dwm.exe by remote LoadLibraryA

dwm_lut.dll inside dwm.exe
  -> logs to %SystemRoot%\Temp\dwm_lut.log
  -> reads staged LUTs
  -> reads manifest.tsv for monitor metadata
  -> identifies loaded dwmcore.dll by PDB GUID/age
  -> selects matching DwmSymbolProfile
  -> disables/bends overlay DirectFlip path where needed
  -> hooks DWM Present path
  -> obtains ID3D11Texture2D backbuffer
  -> creates LUT texture and shader resources
  -> draws corrected rectangle(s) over compositor output
```

The app is not using Windows ICC calibration. It is a compositor shader correction layer. That is why it can correct desktop output in places where GPU LUT paths are unavailable, but also why DWM internals are the central compatibility risk.

## Monitor Selection Logic

Current behavior:

- If there is effectively one staged monitor/LUT, the DLL uses `TryGetSingleMonitorLUT` and ignores fragile DWM coordinates.
- If multiple LUTs exist, the DLL first tries exact `left,top` coordinate match.
- If exact match fails, it tries a tight nearest-position fallback.
- It logs exact/nearest/fallback decisions.
- GUI writes `manifest.tsv` with position, source ID, target ID, device path, name, connector, SDR LUT path, and HDR LUT path.

This is adequate for the user's stated single-monitor use case. For multi-monitor, it is better than the original but not yet the final robust design.

The robust multi-monitor target is:

1. derive the DWM display identity from `CSwapChainRealization::GetDisplayId` or adjacent symbol-derived object state;
2. map that identity to the GUI's `QueryDisplayConfig` / WindowsDisplayAPI source-target identity;
3. use coordinates only as a last-resort heuristic;
4. log the entire display mapping when ambiguity exists;
5. support mixed SDR/HDR targets without one display stealing another display's LUT.

Reference docs:

- `QueryDisplayConfig`: <https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-querydisplayconfig>
- DisplayConfig device info: <https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-displayconfiggetdeviceinfo>

## HDR Behavior

The native shader has an HDR path guarded by the staged LUT filename / monitor metadata. The HDR path maps scRGB through BT.2100/PQ-style transforms, samples the LUT, then maps back toward scRGB for DWM presentation.

What is proven:

- the HDR config path now saves and loads;
- HDR LUT files are staged with `_hdr.cube`;
- native parsing and shader resource creation build cleanly;
- the DLL distinguishes SDR and HDR LUT records.

What is not proven by programmatic build tests:

- physical HDR display correctness;
- Windows HDR toggle transitions;
- MPO/DirectFlip behavior across drivers;
- colorimetric accuracy versus a calibrator's measured target.

The correct validation sequence is:

1. identity LUT in SDR: no visible change;
2. obvious test LUT in SDR: visible controlled change;
3. identity HDR LUT with Windows HDR enabled: no visible change;
4. obvious HDR LUT: visible controlled change;
5. calibrator-generated LUT: compare measured patches;
6. inspect `%SystemRoot%\Temp\dwm_lut.log` for profile, hook, LUT, HDR, and backbuffer messages.

## Logs

GUI log:

```text
%SystemRoot%\Temp\dwm_lut_gui.log
```

DLL log:

```text
%SystemRoot%\Temp\dwm_lut.log
```

High-value log strings:

- `Inject start`
- `OpenProcess`
- `VirtualAllocEx`
- `WriteProcessMemory`
- `CreateRemoteThread`
- `LoadLibrary remote exit code`
- `Matched DWM profile`
- `Unsupported DWM profile`
- `Detected staged monitor manifest.tsv`
- `Single-monitor fast path selected`
- `Exact multi-monitor LUT match`
- `Nearest multi-monitor LUT selected`
- `No LUT found`
- `MinHook`
- `GetBackBuffer`

If the GUI says Apply succeeded but colors do not change, inspect logs in this order:

1. GUI injection log says DLL loaded successfully.
2. DLL log says a DWM profile matched.
3. DLL log says LUT files parsed.
4. DLL log says Present hook installed.
5. DLL log says backbuffer retrieval succeeded.
6. DLL log says the correct SDR/HDR LUT was selected.

## Future Build Port Procedure

For each new Windows build:

1. Determine whether the build is actually new DWM payload or just a label alias.
2. Extract `dwmcore.dll` for the target architecture.
3. Read CodeView RSDS PDB GUID and age from the PE debug directory.
4. Download the matching public PDB from Microsoft's symbol server.
5. Compute SHA-256 of `dwmcore.dll`.
6. Resolve required symbols with DIA or `dbghelp`.
7. Add or update one JSON row in `artifacts/profiles/compiled_dwm_profiles.json`.
8. Run `scripts/generate-dwm-profile-header.ps1`.
9. Run `scripts/test-build.ps1 -Platform x64`.
10. Package with `scripts/package-release.ps1`.
11. Validate in VM/secondary install using identity and obvious LUTs.
12. Only then promote the profile status from experimental to validated.

Symbols to seek first:

| Field | Preferred source |
| --- | --- |
| `presentRva` | `COverlayContext::Present` |
| `directFlipCompatibleRva` | `COverlayContext::IsCandidateDirectFlipCompatible` if present |
| `overlaysEnabledRva` | `COverlayContext::OverlaysEnabled` if present |
| `overlayTestModeRva` | global `m_dwOverlayTestMode` or equivalent |
| `displaySwapChainGetBackBufferRva` | `CDisplaySwapChain::GetBackBuffer` / profile-specific equivalent |
| `displaySwapChainPresentDFlipRva` | `CDisplaySwapChain::PresentDFlip` / profile-specific equivalent |
| `d3dDevicePresentMpoRva` | `CD3DDevice::PresentMPO` / profile-specific equivalent |

Reference docs:

- Microsoft public symbol server: <https://learn.microsoft.com/en-us/windows-hardware/drivers/debugger/microsoft-public-symbols>
- `SymInitialize`: <https://learn.microsoft.com/en-us/windows/win32/api/dbghelp/nf-dbghelp-syminitialize>
- DIA SDK: <https://learn.microsoft.com/en-us/visualstudio/debugger/debug-interface-access/debug-interface-access-sdk>

## Canary Pathway

The current Canary/Future Platforms profile in this repo is `29617.1000_canary`.

Known profile facts:

```text
COverlayContext::Present RVA: 0x5e534
COverlayContext::IsCandidateDirectFlipCompatible RVA: 0x17fabc
COverlayContext::OverlaysEnabled RVA: 0x0 (missing, do not require)
m_dwOverlayTestMode RVA: 0x3b8688
DisplaySwapChain/GetBackBuffer candidate RVA: 0x1acfb0
```

The hard unknown is not symbol discovery. The hard unknown is whether the old 25H2-style backbuffer retrieval path still yields the right `ID3D11Texture2D` on live Canary DWM.

Canary validation plan:

1. Test only in a VM, secondary install, or sacrificial Windows installation.
2. Use the packaged x64 build.
3. Start with identity LUT.
4. Apply an obvious LUT.
5. Read logs for profile match and backbuffer retrieval.
6. If profile matches but texture retrieval fails, map the live swapchain vtable entries back to public PDB symbols.
7. Replace the brittle vtable call chain with a symbol-profiled call path.
8. Re-run with DirectFlip/MPO enabled and disabled.
9. Document whether failure is profile, hook, swapchain, or HDR-specific.

## VM Feasibility

VM testing is feasible for:

- injection flow;
- DWM profile matching;
- basic Present hook installation;
- LUT parsing;
- single-monitor SDR identity and obvious LUT tests;
- DWM crash/restart behavior;
- log quality.

VM testing is weak for:

- real HDR;
- real MPO/DirectFlip paths;
- physical display colorimetry;
- multi-monitor GPU topology;
- driver-specific overlay behavior.

Recommended VM path:

1. Hyper-V or VMware with a Windows Insider Canary ISO/VHD.
2. Disable production account dependencies.
3. Keep a snapshot before installing the test build.
4. Run GUI as Administrator.
5. Keep Task Manager visible to watch DWM restarts.
6. Collect `%SystemRoot%\Temp\dwm_lut.log` and `%SystemRoot%\Temp\dwm_lut_gui.log`.

Do not interpret a VM SDR pass as proof of HDR correctness on the physical display.

## ARM64 Plan

ARM64 is a separate product artifact.

Required:

- Visual Studio ARM64 C++ tools installed;
- ARM64 vcpkg triplet working;
- ARM64 `dwm_lut.dll`;
- ARM64 `dwmcore.dll` extraction for each target build;
- ARM64 public PDB symbol resolution;
- ARM64 entries in `compiled_dwm_profiles.json`;
- an ARM64 package zip.

Not allowed:

- reuse x64 RVAs on ARM64;
- claim support because the C# GUI builds;
- claim support because UUP lists ARM64 payloads.

The GUI can remain AnyCPU or x64-hosted depending on packaging, but the injected DLL must match the architecture of `dwm.exe`.

## Programmatic Tests Now In Place

Primary smoke test:

```powershell
.\scripts\test-build.ps1 -Platform x64
```

This currently verifies:

- solution builds;
- GUI output exists;
- native DLL output exists;
- docs/catalog/profile files exist;
- generated DWM profile header is synchronized with JSON;
- generated Markdown profile table is synchronized with JSON;
- key profiles are compiled;
- UUP build catalog includes expected x64/ARM64 rows;
- DLL SHA-256 is emitted.

Last observed result after the warning cleanup:

```text
Warnings: 0
Errors: 0
dwm_lut.dll SHA256: 02C9E254475B79C36E5A27CADF66B92CFA7974A907968E03B8ADF83E6435D2F3
Programmatic build checks passed.
```

What this does not prove:

- live DWM injection on every profiled build;
- real HDR correctness;
- real multi-monitor correctness;
- future Canary stability.

## 2026-06-29 Compatibility Mode And Offline Validation Pass

This pass added a user-visible Compatibility mode instead of relying on silent auto-detection or exposing internal hook jargon.

Implemented code:

- `src\DwmLutGUI\DwmLutGUI\MainWindow.xaml` now shows a Compatibility selector, short inline use order, How to use window, and DWM support status.
- `src\DwmLutGUI\DwmLutGUI\DwmSupportStatus.cs` reads `System32\dwmcore.dll`, extracts PE machine + CodeView RSDS PDB GUID/age + SHA-256, and matches the generated GUI profile table.
- `src\DwmLutGUI\DwmLutGUI\DwmProfiles.generated.cs` is generated beside the native `DwmProfiles.generated.h`, so the GUI and DLL see the same profile metadata.
- `src\DwmLutGUI\DwmLutGUI\Injector.cs` stages `%SystemRoot%\Temp\luts\resolver.cfg` before injection.
- `src\lutdwm\dllmain.cpp` reads `resolver.cfg`, logs the internal mode, applies manual engine overrides, tries verified profiles first where appropriate, and fails closed for 26H1/Canary modes if DWM does not match.
- `artifacts\profiles\compiled_dwm_profiles.json` now carries `engineFamily`, `presentAbi`, `swapchainStrategy`, `backbufferStrategy`, `overlayStrategy`, `monitorIdentityStrategy`, and `validationState`.
- `scripts\test-dwm-payload-profile.ps1` validates a copied `dwmcore.dll` offline.

Compatibility modes:

| GUI mode | Native behavior | Use case |
| --- | --- | --- |
| Auto-detect (recommended) | Verified DWM profile first, then OS-family fallback | Normal users |
| Windows 10 22H2 (19045) | Force the Windows 10 fallback engine | Windows 10 fallback testing |
| Windows 11 22H2 / 23H2 | Force the older Windows 11 fallback engine | Older Windows 11 fallback testing |
| Windows 11 24H2 (26100) | Force the 26100-family fallback engine | 24H2 fallback testing |
| Windows 11 25H2 / 26H2 | Use known 26200/26300 profiles, then modern fallback | 25H2/26H fallback comparison |
| Windows 11 26H1 Insider | Require known 28000/28120 profile | Future-profile testing |
| Canary 29617 (experimental) | Require compiled Canary profile | Canary research only |

Static validation now performed:

```powershell
.\scripts\test-build.ps1 -Platform x64
.\scripts\test-dwm-payload-profile.ps1
.\scripts\test-dwm-payload-profile.ps1 -DwmCorePath "C:\Users\arsen\Documents\Codex\2026-06-28\lauralex-dwm-lut-https-github-com\work\canary_29617\extracted\amd64_microsoft-windows-d..wmanager-compositor_31bf3856ad364e35_10.0.29617.1000_none_34a6841f8b294dea\dwmcore.dll"
```

Observed local result:

```text
Profile match: 26200.8655_current
Engine: modern-profiled / overlay-present-modern7 / profiled-get-backbuffer / local-built
Static DWM payload profile validation passed.
```

Observed Canary result:

```text
Profile match: 29617.1000_canary
Engine: modern-profiled / overlay-present-modern7 / profiled-get-backbuffer / experimental
overlaysEnabledRva: 0x0 absent
overlayTestModeRva: 0x3B8688 ok:.data
Static DWM payload profile validation passed.
```

What this proves:

- the compiled profile table matches the exact payload identities for local 25H2 and extracted Canary;
- profile SHA-256 values are correct;
- all nonzero hard RVAs point into valid PE sections;
- the Canary profile no longer depends on `COverlayContext::OverlaysEnabled`, which is missing from the public Canary symbols.

What this still does not prove:

- that the live Canary compositor object passed to `COverlayContext::Present` still exposes the same backbuffer path;
- HDR visual correctness;
- multi-monitor correctness under real topology churn;
- future payloads with new PDB GUIDs.

The answer to "can we validate Canary without a VM?" is therefore: mostly for profile identity and address safety, yes; for live backbuffer behavior, no. The remaining unknown is empirical and requires a real Canary DWM process.

The answer to "should everything work except Canary?" is more nuanced:

- local `26200.8655` is the only profile validated on this actual desktop;
- other built profiles should select coherent hook addresses by code/static payload logic, but still need live DWM validation for color/HDR/MPO behavior;
- fallback-only Windows 10 and Windows 11 24H2 rows are best-effort, not equivalent to a verified DWM profile;
- Canary has a static profile, but live backbuffer retrieval is explicitly unvalidated.

Windows 10 status:

- UUP metadata now includes Windows 10 22H2 `19045.7417`.
- The app has a Windows 10 manual fallback mode and the build catalog marks amd64 Win10 as x64 fallback.
- No exact Win10 PDB/RVA row has been generated in this pass because there is no parsed 19045 `dwmcore.dll` payload in the local research cache. Exact Win10 support requires extracting the 19045 amd64 DWM payload, resolving its public PDB, adding a legacy-profile row, and validating it with `test-dwm-payload-profile.ps1`.

## Packaging Procedure

Build and test:

```powershell
.\scripts\test-build.ps1 -Platform x64
```

Package:

```powershell
.\scripts\package-release.ps1 -Version 4.0.0-modern-windows -Platform x64 -SkipBuild
```

Move to another PC:

1. Copy the zip.
2. Extract the entire folder.
3. Keep `DwmLutGUI.exe`, `DwmLutGUI.exe.config`, `WindowsDisplayAPI.dll`, and `dwm_lut.dll` together.
4. Run `DwmLutGUI.exe` as Administrator.
5. Select SDR/HDR `.cube` LUTs.
6. Click Apply.
7. Inspect logs.
8. Click Disable before replacing files.

## Downsides And Risks

Real risks:

- DWM can crash and restart if a private offset or object-layout assumption is wrong.
- Windows Update can replace `dwmcore.dll` and make a profile unsupported.
- HDR correctness needs physical measurement.
- Multi-monitor still has coordinate-based fallback until the DWM display identity path is completed.
- Some drivers aggressively use MPO/DirectFlip paths that may bypass or stress this hook strategy.
- The tool is unsigned.
- Security software may dislike DLL injection into `dwm.exe`.
- Apps using exclusive/fullscreen/HDR/overlay paths can behave differently from normal desktop windows.

Practical risk reduction:

- use the single-monitor fast path when possible;
- use identity LUT first;
- keep logs;
- package PDBs for diagnostics;
- avoid using experimental Canary builds on a production install;
- disable before replacing package files.

## Remaining Engineering Work

Highest value next tasks:

1. Build a DWM profile ingestion tool that extracts RSDS identity and resolves symbols through DIA automatically.
2. Validate `29617.1000_canary` in a VM and classify the backbuffer path.
3. Replace coordinate-based multi-monitor selection with DWM display identity matching.
4. Add a GUI button or menu item to open the two log files.
5. Add explicit runtime profile/status display in the GUI after injection.
6. Add ARM64 toolchain install verification and ARM64 profile generation.
7. Split heavy DWM initialization out of `DllMain` into a worker init path with an injector handshake.
8. Add D3D11 state backup/restore around the shader pass if live testing shows state leakage.
9. Sign release artifacts.
10. Build a repeatable VM validation script that collects logs and Windows build metadata.

## Loader-Lock Note

The current DLL startup is hardened but still does more work during load than ideal. Microsoft's DLL best-practice guidance warns against complex work under loader lock. A purist architecture would make `DllMain` do the minimum possible work, spawn or signal a separate initialization path, and report success/failure back to the injector. That refactor is possible, but it changes the injector/DLL contract and should be done as a dedicated commit with live testing.

For now, the repo has moved from "brittle and opaque" toward "fail closed with logs." That is a meaningful improvement, but not the final architecture.

## Source Links

Core upstreams:

- Original project: <https://github.com/lauralex/dwm_lut>
- Reviewed 25H2 fork: <https://github.com/0x401gg/dwm_lut-windows-25h2>

Microsoft / platform references:

- DLL best practices: <https://learn.microsoft.com/en-us/windows/win32/dlls/dynamic-link-library-best-practices>
- `IsBadReadPtr`: <https://learn.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-isbadreadptr>
- Public symbols: <https://learn.microsoft.com/en-us/windows-hardware/drivers/debugger/microsoft-public-symbols>
- `SymInitialize`: <https://learn.microsoft.com/en-us/windows/win32/api/dbghelp/nf-dbghelp-syminitialize>
- DIA SDK: <https://learn.microsoft.com/en-us/visualstudio/debugger/debug-interface-access/debug-interface-access-sdk>
- `CreateRemoteThread`: <https://learn.microsoft.com/en-us/windows/win32/api/processthreadsapi/nf-processthreadsapi-createremotethread>
- `VirtualAllocEx`: <https://learn.microsoft.com/en-us/windows/win32/api/memoryapi/nf-memoryapi-virtualallocex>
- `WriteProcessMemory`: <https://learn.microsoft.com/en-us/windows/win32/api/memoryapi/nf-memoryapi-writeprocessmemory>
- `OpenProcess`: <https://learn.microsoft.com/en-us/windows/win32/api/processthreadsapi/nf-processthreadsapi-openprocess>
- `QueryDisplayConfig`: <https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-querydisplayconfig>
- DisplayConfig device info: <https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-displayconfiggetdeviceinfo>
- MSVC runtime library options: <https://learn.microsoft.com/en-us/cpp/build/reference/md-mt-ld-use-run-time-library>
- LNK4098: <https://learn.microsoft.com/en-us/cpp/error-messages/tool-errors/linker-tools-warning-lnk4098>
- vcpkg triplets: <https://learn.microsoft.com/en-us/vcpkg/users/triplets>

Windows build tracking:

- Windows 11 release information: <https://learn.microsoft.com/en-us/windows/release-health/windows11-release-information>
- Windows Insider blog: <https://blogs.windows.com/windows-insider/>
- Future Platforms build 29617.1000: <https://learn.microsoft.com/en-us/windows-insider/release-notes/experimental-future-platforms/preview-build-29617-1000>
