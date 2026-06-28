# DWM LUT Forward-Port Master Report

Date: 2026-06-29  
Workspace: `C:\Users\arsen\Documents\Code\DWM LUT`  
Source base: [`zkippp/dwm_lut_fixed`](https://github.com/zkippp/dwm_lut_fixed), tag `v1.0.8_25H2`, itself derived from [`lauralex/dwm_lut`](https://github.com/lauralex/dwm_lut)

## Executive Summary

This pass produced a buildable, packaged, multi-profile diagnostic forward port of DWM LUT. It is not a magic universal DWM hook. It is a deterministic profile-based DLL that reads the loaded `dwmcore.dll` PDB identity at runtime and selects exact RVAs mined from Microsoft public symbols.

The main conclusion is:

1. The tool was not forward-ported cleanly because DWM internals are private, PGO/LTO-reordered, and unstable across Windows cumulative builds. Byte signatures became an unreliable substitute for symbols.
2. The old "find a few functions by AOB and assume the same private object/vtable layout" model does not scale to 24H2/25H2/26H1/Canary.
3. The right forward-port path is a generated profile pipeline:
   UUP payload -> `dwmcore.dll` -> CodeView PDB identity -> public PDB -> symbol RVAs -> compiled profile -> live texture-path validation.
4. The hardest unresolved technical gate is not finding `COverlayContext::Present`. It is whether the compositor backbuffer path still yields the correct `ID3D11Texture2D` on the live build.
5. For your single-monitor setup, the fragile coordinate-based LUT lookup has been bypassed when all staged LUT files describe one monitor. This removes the largest practical multi-monitor-derived failure mode for your current machine.

## What I Built

Built source:

`C:\Users\arsen\Documents\Code\DWM LUT\source-profiled`

Portable version folders and zips:

`C:\Users\arsen\Documents\Code\DWM LUT\versions`

Packages created:

| Package | Purpose |
| --- | --- |
| `26200.8655_current-machine.zip` | Your current installed `dwmcore.dll` profile. |
| `26200.8737_stable-25H2-KB5095093.zip` | Latest stable 25H2 preview build found in Microsoft release info. |
| `26220.8754_beta-25H2.zip` | Insider Beta 25H2. |
| `26300.8758_experimental-26H2.zip` | Insider Experimental 26H2; exact DWM payload alias of `26220.8754`. |
| `28000.2340_published-26H1.zip` | Published 26H1 build from Microsoft release info/UUP. |
| `28020.2366_beta-26H1.zip` | Insider Beta 26H1. |
| `28120.2374_experimental-26H1.zip` | Insider Experimental 26H1; exact DWM payload alias of `28020.2366`. |
| `29617.1000_canary-future-platforms.zip` | Latest Canary/Future Platforms build found in Microsoft docs. |

The DLL SHA-256 in the final packages is:

`6359DB7BBC399DE3A7D1911333F1A7514891B6431D4BDF2A9D512121C020036D`

Build result:

`MSBuild Release|x64`: success, 0 errors. Remaining warnings are pre-existing numeric conversion/link-runtime warnings, not profile selection errors.

## Sources Used

Primary project sources:

- Original repo: [lauralex/dwm_lut](https://github.com/lauralex/dwm_lut)
- Forward-port fork used as base: [zkippp/dwm_lut_fixed](https://github.com/zkippp/dwm_lut_fixed)
- MinHook dependency: [TsudaKageyu/minhook](https://github.com/TsudaKageyu/minhook)

Windows build sources:

- [Windows 11 release information](https://learn.microsoft.com/en-us/windows/release-health/windows11-release-information)
- [KB5095093: OS builds 26200.8737 and 26100.8737](https://support.microsoft.com/en-us/topic/june-23-2026-kb5095093-os-builds-26200-8737-and-26100-8737-preview-0e2a20f2-cf9e-46f8-9f08-e6996220882d)
- [Windows Insider builds announced June 26, 2026](https://blogs.windows.com/windows-insider/2026/06/26/announcing-new-builds-for-26-june-2026-retail-launch-of-new-wip-improvements/)
- [Windows Insider Experimental 26H2 builds announced June 19, 2026](https://blogs.windows.com/windows-insider/2026/06/19/announcing-new-builds-for-19-june-2026-26h2-for-experimental/)
- [Future Platforms / Canary build 29617.1000 release note](https://learn.microsoft.com/en-us/windows-insider/release-notes/experimental-future-platforms/preview-build-29617-1000)

Symbol/debug sources:

- [Microsoft public symbols](https://learn.microsoft.com/en-us/windows-hardware/drivers/debugger/microsoft-public-symbols)
- [DbgHelp `SymInitialize`](https://learn.microsoft.com/en-us/windows/win32/api/dbghelp/nf-dbghelp-syminitialize)
- [DIA SDK](https://learn.microsoft.com/en-us/visualstudio/debugger/debug-interface-access/debug-interface-access-sdk)
- [UUP dump API](https://api.uupdump.net/)
- [wimlib](https://wimlib.net/)
- [Microsoft vcpkg](https://github.com/microsoft/vcpkg)

## Build Matrix

As of 2026-06-29, the relevant published/Insider/Future builds found and handled are:

| Windows build | Microsoft channel/status | UUP DWM payload SHA-256 | `dwmcore.dll` SHA-256 | PDB identity | Package status |
| --- | --- | --- | --- | --- | --- |
| `26200.8655` | Current installed machine | Local system DLL | `03C820B55D3CF470...` | `35D945A7-1171-77C7-B6CD-6D7D9ED0A2E8:1` | Built |
| `26200.8737` | Stable 25H2 preview / KB5095093 | `c57af36c9f38581c0575739417d0817de3807db4aae406e990be13e626cade6a` | `38225FEBFF2B43F6...` | `AE59991D-5182-9D46-C2DF-79FD96B670FD:1` | Built |
| `26220.8754` | Insider Beta 25H2 | `7db83f499db79fc3add771556bf6b7fb2565dc14ff04a4b2619c0c9763249414` | `02BA53D0351FECA7...` | `12B33AD2-4F89-E236-EB73-414E39695B45:1` | Built |
| `26300.8758` | Insider Experimental 26H2 | Same as `26220.8754` | Same as `26220.8754` | Same as `26220.8754` | Built as alias |
| `28000.2340` | Published 26H1 | `975c0f6bb7f80fe599fb4b285350959d0a68ca3eed7518fa3f671b509fef7264` | `B7603B710B4EC607...` | `A501A027-A993-31EF-661B-19606968926E:1` | Built |
| `28020.2366` | Insider Beta 26H1 | `e42428f8bf448ed6607f6266a15f537495e3e2c4f00d25dc88a49cb2929cd69b` | `93CF529C49A94A87...` | `14BD81D0-E230-08E7-EECE-589B8D29E6EA:1` | Built |
| `28120.2374` | Insider Experimental 26H1 | Same as `28020.2366` | Same as `28020.2366` | Same as `28020.2366` | Built as alias |
| `29617.1000` | Canary / Future Platforms | `f1386664379c49b47f0c4b2bceb05ed9e9933d7aa3e93097211df5642be958de` | `0FCD486AB588F609...` | `9D2F7102-1CDA-8409-2018-100E70836B6E:1` | Built |

Important nuance: Microsoft can ship a new OS build number while reusing the same component payload for DWM. That happened here for `26300.8758` and `28120.2374`. Treat the `dwmcore.dll` PDB identity as the truth, not the marketing build number.

## Runtime Profile Table

The final compiled profile header is:

`C:\Users\arsen\Documents\Code\DWM LUT\source-profiled\lutdwm\DwmProfiles.generated.h`

Human-readable copy:

`C:\Users\arsen\Documents\Code\DWM LUT\profiles\dwm_profiles_table.md`

| Profile | Present | DirectFlip | OverlaysEnabled | OverlayTestMode | GetBackBuffer | Notes |
| --- | ---: | ---: | ---: | ---: | ---: | --- |
| `26200.8655_current` | `0x231800` | `0xb1414` | `0x1a2be8` | `0x3fb22c` | `0x2c830` | Current installed machine. |
| `26200.8737_stable` | `0x473c0` | `0x0` | `0x4ed64` | `0x3e7ef4` | `0x1de700` | DirectFlip symbol absent. |
| `26220.8754_beta25h2__26300.8758_exp26h2` | `0x473c0` | `0x0` | `0x4ed64` | `0x3e7ef4` | `0x1de700` | Exact payload alias. |
| `28000.2340_published26h1` | `0x18b510` | `0x23e424` | `0x0` | `0x3e0c84` | `0x1b05b0` | Same RVAs as 28020, distinct PDB identity. |
| `28020.2366_beta26h1__28120.2374_exp26h1` | `0x18b510` | `0x23e424` | `0x0` | `0x3e0c84` | `0x1b05b0` | `OverlaysEnabled` absent. |
| `29617.1000_canary` | `0x5e534` | `0x17fabc` | `0x0` | `0x3b8688` | `0x1acfb0` | `OverlaysEnabled` absent. |

## Why This Was Not Ported Cleanly Before

The fixed fork's git history supports a retreat from broad forward-port ambition back to a verified-stable 23H2 baseline:

- `db0a483`: tag `v1.0.8_25H2`, version bump.
- `69bf735`: `v1.0.8: Official 23H2 Support - VERIFIED STABLE`.
- `759a8bb`: dynamic memory scanner for precise multi-monitor detection on Windows 11 23H2.
- `e14d7f6`, `26089e5`, `ed7c32f`: repeated 23H2 multi-monitor/coordinate fixes.

That pattern is not laziness. It is a signal that:

1. The project depends on private DWM classes, not a stable Windows API.
2. Minor Windows updates reorder functions and alter class layout.
3. AOB scanning can produce false positives once DWM is optimized differently.
4. The 25H2/26H1/Canary branches do not all expose the same public symbols.
5. Multi-monitor lookup by DWM-internal coordinates is brittle.
6. The real texture extraction path is built from private vtable ordinal assumptions.

The public 25H2 tags exist, but the stability signal in later commits moved back toward verified 23H2. The reason is maintainability: a DWM hook needs per-build symbol-aware profile generation plus live validation. Without that, each "fixed" build is a time bomb after the next cumulative update.

## How The App Works

### GUI

The GUI is a WPF/.NET Framework app:

`C:\Users\arsen\Documents\Code\DWM LUT\source-profiled\DwmLutGUI\DwmLutGUI`

Important behavior:

- It enumerates active display paths with `WindowsDisplayAPI.DisplayConfig.PathInfo.GetActivePaths()`.
- It stores `config.xml` beside `DwmLutGUI.exe`, not in AppData.
- It expects `dwm_lut.dll` beside `DwmLutGUI.exe`.
- On Apply it copies `dwm_lut.dll` to `%SystemRoot%\Temp\dwm_lut.dll`.
- It stages LUTs under `%SystemRoot%\Temp\luts\`.
- It injects into every running `dwm.exe` via `CreateRemoteThread(... LoadLibraryA ...)`.
- On successful injection it deletes the staged LUT folder after the DLL has loaded LUT data into memory.
- On Disable it remote-calls `FreeLibrary` on `dwm_lut.dll`.

### DLL

The native DLL:

`C:\Users\arsen\Documents\Code\DWM LUT\source-profiled\lutdwm\dllmain.cpp`

Core flow:

1. `DllMain` runs inside `dwm.exe`.
2. It detects the loaded `dwmcore.dll`.
3. It now reads the CodeView RSDS PDB GUID/age from `dwmcore.dll`.
4. It selects a known profile from `DwmProfiles.generated.h`.
5. It hooks `COverlayContext::Present` using MinHook.
6. It optionally hooks DirectFlip compatibility and/or `OverlaysEnabled` depending on what exists for that profile.
7. It writes `CCommonRegistryData::m_dwOverlayTestMode = 5`.
8. In the Present hook it obtains a compositor backbuffer texture, copies/filters it through a D3D11 shader, applies the 3D LUT, and writes the result back.

The shader supports:

- SDR LUT path: direct RGB 3D LUT in normalized display-linear-ish space as implemented by the original project.
- HDR LUT path: scRGB -> BT.2100/PQ-ish transform, LUT, then back to scRGB.
- Tetrahedral LUT interpolation.
- Dithering/noise.

### HDR Reality Check

The code has an HDR path, and the GUI can stage `_hdr.cube` files. That is not the same as saying every HDR calibration LUT will be correct.

HDR correctness depends on:

- Whether DWM is handing the hook scRGB, PQ, or another intermediate for that frame.
- Whether the LUT was generated for the same expected domain.
- Whether Windows Advanced Color tonemapping is active.
- Whether the app content is SDR-in-HDR, native HDR, fullscreen MPO, or a protected path.

The build can apply an HDR LUT path, but HDR must be validated with an identity LUT, an obvious test LUT, and then the calibrator LUT while checking `%SystemRoot%\Temp\dwm_lut.log`.

## Code Changes Made

Major native changes:

- Added `DwmProfiles.generated.h`.
- Added runtime PDB identity parsing from loaded `dwmcore.dll`.
- Added exact profile selection by PDB GUID/age.
- Added profile RVAs for current, 25H2 stable, 25H2 beta/26H2 alias, 26H1 published, 26H1 beta/experimental, and Canary.
- Changed logs from `C:\DWMLOG\dwm.log` to `%SystemRoot%\Temp\dwm_lut.log`.
- Forced diagnostic logging in Release builds.
- Made `COverlayContext::OverlaysEnabled` optional because it is missing in 26H1/Canary symbols.
- Made DirectFlip hook optional because the exact symbol is absent in 26200.8737/26220.8754.
- Added direct `m_dwOverlayTestMode = 5` write from profile RVA.
- Preserved the previous overlay-test value and restores it on detach instead of blindly writing `0`.
- Added a single-monitor fast path: if all staged LUTs have the same GUI monitor position, select by HDR/SDR only and ignore fragile DWM private monitor coordinates.
- Removed duplicated optional hook creation block.

This is the most important architectural change:

Old model:

`scan bytes -> first match wins -> assume hook set complete`

New model:

`read loaded dwmcore PDB identity -> exact build profile -> optional hook set -> log every selected address`

## What Is Still Fragile

### Texture Retrieval

The remaining hard blocker is the backbuffer object path. The fixed fork still relies on private object/vtable knowledge around the overlay swap chain and display swap chain. Public symbols prove these functions exist:

- `CDDisplaySwapChain::GetBackBuffer`
- `CDDisplaySwapChain::GetPhysicalBackBuffer`
- `CSwapChainRealization::GetDeviceTexture`
- `CSwapChainRealization::GetDXGIResource`
- `CSwapChainRealization::IsHDRContent`
- `CSwapChainRealization::GetDisplayId`

But live DWM must confirm which object pointer is available from `COverlayContext::Present` on each branch. If the current ordinal path fails, the next fix is to instrument the live vtable pointers and map them back to the public PDB symbol RVAs.

### Multi-Monitor

This build is improved for single monitor. It is not yet a robust multi-monitor rewrite.

Known multi-monitor weaknesses:

- GUI keys LUTs by `left,top`.
- DLL reads private DWM geometry to map context to `left,top`.
- Negative coordinates, duplicated origins, cloned displays, portrait transforms, DPI, HDR mixed-mode, and display hotplug can break the mapping.
- Multi-GPU systems can produce identical or misleading coordinate surfaces.

Robust multi-monitor design:

1. GUI writes a manifest into `%SystemRoot%\Temp\dwm_lut_manifest.json`.
2. Manifest keys each LUT by:
   - `DISPLAYCONFIG_PATH_TARGET_INFO.adapterId`
   - `id`
   - source ID
   - target ID
   - connector/device path
   - monitor serial/EDID hash when available
3. DLL stops relying on `left,top`.
4. DLL obtains the DWM display identity from `CSwapChainRealization::GetDisplayId` or adjacent symbol-derived object state.
5. DLL maps that display identity to the manifest.
6. Cache per context and invalidate on display topology change.

That is the correct multi-monitor fix. Anything else is another coordinate heuristic.

## VM Feasibility

VM is feasible for symbol/profile validation and safe crash testing. It is only partially feasible for color/HDR validation.

Feasible in VM:

- Confirm DLL injection works.
- Confirm PDB profile selection works.
- Confirm hooks install.
- Confirm DWM does not crash repeatedly.
- Confirm logs show Present hook activity.
- Test SDR identity and obvious test LUT in a visible desktop session.

Not fully reliable in VM:

- HDR correctness.
- MPO/direct flip behavior.
- Protected video paths.
- Display-specific EDID/ICC behavior.
- Real GPU scanout behavior.

Best VM plan:

1. Use Hyper-V, VMware, or VirtualBox for first-pass Canary/Insider smoke testing.
2. Disable Enhanced Session if it hides real display behavior.
3. Use a local console session, not only RDP.
4. Use identity LUT first.
5. Then use an obvious low-risk LUT.
6. Treat HDR validation as "smoke only" unless GPU passthrough or a physical secondary install is used.

Best physical plan:

Use a secondary Windows install or spare SSD for Canary/Insider builds. This is the only trustworthy path for HDR and MPO behavior.

## Exact Usage On Another PC

1. Open `winver` on the target PC.
2. Pick the closest matching package folder/zip from:
   `C:\Users\arsen\Documents\Code\DWM LUT\versions`
3. Extract the zip.
4. Run `DwmLutGUI.exe` as Administrator from the extracted folder.
5. Select SDR `.cube` and, if needed, HDR `.cube`.
6. Click Apply.
7. Inspect `%SystemRoot%\Temp\dwm_lut.log`.
8. If log says `No matching DWM symbol profile`, that Windows build needs a new profile.
9. Click Disable before replacing files or changing LUTs.

What "installed" means:

- The app is portable. It is not installed into Program Files.
- GUI config is `config.xml` beside the EXE.
- During Apply, DLL goes to `%SystemRoot%\Temp\dwm_lut.dll`.
- Runtime log is `%SystemRoot%\Temp\dwm_lut.log`.
- LUT staging is `%SystemRoot%\Temp\luts\` and is removed after injection.

## Downsides And Risks

This is an unsupported DWM injection tool. Real risks:

- DWM crash/restart.
- Black screen or compositor flicker until DWM restarts.
- Oversaturated/blown-out colors if the LUT domain is wrong.
- HDR transforms can be wrong if the calibrator LUT assumes a different signal domain.
- Windows Update can invalidate the profile.
- Anticheat/security software may dislike process injection, LSASS token impersonation, or DLL staging in `%SystemRoot%\Temp`.
- Protected content may bypass or reject the path.
- Multi-monitor is still not robust.
- Canary can change underneath us quickly.

Specific per-branch risk:

- `26200.8737`, `26220.8754`, `26300.8758`: exact `COverlayContext::IsCandidateDirectFlipCompatible` public symbol is absent; build relies on `OverlaysEnabled` plus `OverlayTestMode`.
- `28000.2340`, `28020.2366`, `28120.2374`, `29617.1000`: `COverlayContext::OverlaysEnabled` is absent; build relies on DirectFlip hook plus direct `OverlayTestMode`.
- All branches: texture retrieval still needs live validation.

## How To Forward-Port The Next Build

Precise pipeline:

1. Find build in Microsoft release docs or Insider post.
2. Use UUP dump API:
   `https://api.uupdump.net/listid.php?search=<build>&sortByDate=1`
3. Fetch metadata:
   `https://api.uupdump.net/get.php?id=<uuid>&lang=en-us&edition=professional`
4. Download `Microsoft-Win4-Feature.ESD`.
5. Verify SHA-256 from UUP metadata.
6. Extract with `wimlib-imagex`:
   `wimlib-imagex.exe extract Microsoft-Win4-Feature.ESD 1 / --dest-dir=extracted --no-acls`
7. Locate `dwmcore.dll` in the extracted DWM compositor component.
8. Read CodeView PDB identity with `dumpbin /headers`.
9. Download PDB:
   `https://msdl.microsoft.com/download/symbols/dwmcore.pdb/<GUID_NO_DASHES><AGE>/dwmcore.pdb`
10. Dump publics:
    `llvm-pdbutil.exe dump -publics dwmcore.pdb`
11. Extract these symbols:
    - `?Present@COverlayContext`
    - `?PresentMPO@COverlayContext`
    - `?IsCandidateDirectFlipCompatible@COverlayContext`
    - `?OverlaysEnabled@COverlayContext`
    - `?m_dwOverlayTestMode@CCommonRegistryData`
    - `?GetBackBuffer@CDDisplaySwapChain`
    - `?GetPhysicalBackBuffer@CDDisplaySwapChain`
    - `?PresentDFlip@CDDisplaySwapChain`
    - `?PresentMPO@CD3DDevice`
    - `?GetDeviceTexture@CSwapChainRealization`
    - `?GetDXGIResource@CSwapChainRealization`
    - `?GetDisplayId@CSwapChainRealization`
    - `?IsHDRContent@CSwapChainRealization`
12. Convert PDB segment:offset to PE RVA by adding section virtual address.
13. Add a new `DwmSymbolProfile`.
14. Build Release x64.
15. Test in VM with identity LUT.
16. Test on physical install for HDR/MPO.

## AI-Assisted Rebuild Strategy

Use AI for the parts where semantic comparison helps; keep address generation deterministic.

AI should do:

- Compare public symbol sets across builds.
- Detect renamed/missing methods.
- Summarize disassembly differences around Present/DirectFlip/GetBackBuffer.
- Generate hypotheses for changed object relationships.
- Read logs and propose the next instrumentation point.

AI should not do:

- Invent offsets.
- Guess vtable indexes without live validation.
- Treat Windows build number as stronger evidence than PDB identity.

Deterministic tools should do:

- UUP fetch.
- Hash verification.
- ESD extraction.
- PDB download.
- Symbol parsing.
- PE section/RVA conversion.
- Build/package.

## Next Engineering Step

Run the final package on a test target with an identity LUT and collect `%SystemRoot%\Temp\dwm_lut.log`.

The pass/fail fork:

- If log shows profile selected, hooks installed, and Present hook applies: current profile path works.
- If profile selected but no visual change: texture retrieval or shader path is failing.
- If DWM crashes: wrong hook signature/object assumptions.
- If `No matching DWM symbol profile`: new build needs a profile.
- If HDR only fails: LUT domain/HDR state detection path needs work.

If texture retrieval fails, implement this precise fix:

1. Add diagnostic logging of the `overlaySwapChain` vtable pointer in `COverlayContext_Present_hook_24h2`.
2. Log candidate vtable entries around the old ordinal path.
3. Convert each function pointer to `dwmcore.dll` RVA.
4. Match RVAs against public PDB symbols.
5. Replace ordinal calls with named, profile-backed calls where possible.
6. Prefer `CSwapChainRealization::GetDeviceTexture` or `GetDXGIResource` if a reliable object path is found.
7. Use `CSwapChainRealization::IsHDRContent` instead of guessing HDR from a context pointer when available.

## Installed Tooling

Installed into this workspace:

- vcpkg: `C:\Users\arsen\Documents\Code\DWM LUT\tools\vcpkg`
- NuGet CLI: `C:\Users\arsen\Documents\Code\DWM LUT\tools\nuget.exe`
- wimlib: `C:\Users\arsen\Documents\Code\DWM LUT\tools\wimlib-imagex.exe`

Installed dependencies:

- vcpkg `minhook:x64-windows-static`
- NuGet `WindowsDisplayAPI 1.3.0.13`

Installed Codex skill:

- `improve` from `shadcn/improve` to `C:\Users\arsen\.codex\skills\improve`

Codex must be restarted before the newly installed skill appears in future skill lists.

## Final State

This is now a real forward-port baseline:

- It builds.
- It packages.
- It has public-symbol-derived profiles through published 26H1, Insider Beta/Experimental, and Canary/Future Platforms.
- It avoids a single-monitor LUT mapping trap.
- It logs to an accessible Windows temp location.
- It does not pretend multi-monitor or future Canary texture retrieval is solved without live proof.

The next unknown is empirical, not research: does the live DWM object path on each target build still yield the texture the shader needs? The package and logging now exist to answer that cleanly.
