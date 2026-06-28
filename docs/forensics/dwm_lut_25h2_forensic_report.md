# Forensic Report: Why `lauralex/dwm_lut` Does Not Apply LUTs on This Windows Build

Date: 2026-06-28  
Machine inspected: `C:\Users\arsen` Windows desktop  
Target project: [lauralex/dwm_lut](https://github.com/lauralex/dwm_lut)  
Release archive supplied by user: `C:\Users\arsen\Downloads\Release24h2 (2).zip`

## Executive Conclusion

The failure is not primarily a DisplayCAL `.cube` problem, not a PATH problem, and not a normal ICC-profile problem. The root cause is that the lauralex 24H2 release is a private DWM hook that depends on exact byte signatures inside `dwmcore.dll`, and the required `COverlayContext::Present` signature from lauralex `v4.0.2` is absent from this machine's current DWM binary.

This system reports Windows 11 `25H2`, build `26200.8655`, while lauralex `v4.0.2` only distinguishes Windows 11 `>=26100` as "24H2". It therefore selects its 24H2 hook path, scans for the 24H2 `Present` AOB pattern, finds nothing, and cannot install the hook that actually runs the LUT pixel shader.

The hard evidence:

| Hook pattern scanned against `C:\Windows\System32\dwmcore.dll` | Source | Hits on this machine |
| --- | --- | ---: |
| `COverlayContext_Present_w11_24h2` | lauralex `v4.0.2` | `0` |
| `COverlayContext_IsCandidateDirectFlipCompatible_w11_24h2` | lauralex `v4.0.2` | `2` |
| `COverlayContext_OverlaysEnabled_relative_w11_24h2` | lauralex `v4.0.2` | `1` |
| `COverlayContext_Present_w11_25h2` | `zkippp/dwm_lut_fixed` source | `1`, at `0x231800` |
| `COverlayContext_IsCandidateDirectFlipCompatible_w11_25h2` | `zkippp/dwm_lut_fixed` source | `2`, at `0x14ed8`, `0xb1414` |
| `COverlayContext_OverlaysEnabled_w11_25h2` | `zkippp/dwm_lut_fixed` source | `1`, at `0x1a2be8` |

The practical recommendation is:

1. Do not waste time adding `DwmLutGUI.exe` to PATH. The GUI loads `dwm_lut.dll` from its own application folder and copies it to `%WINDIR%\Temp`; PATH is not part of the hook failure.
2. For this system, lauralex `Release24h2.zip` is stale. Replace it with a build that has explicit 25H2 build `26200` handling.
3. The `zkippp/dwm_lut_fixed` / mirrored `ed1ii/dwm_lut_fixed` source is a plausible upgrade base because its essential 25H2 signatures match this machine's `dwmcore.dll`.
4. If you want to stay on lauralex specifically, forward-port the 25H2 detection, AOB patterns, swap-chain discovery, and DirectFlip/MPO logic from the fixed fork, then build x64 Release.
5. Rollback should be a last resort. If rollback is chosen only for dwm_lut, target a Windows build that the chosen dwm_lut release was actually written against, not merely "Windows 11".

## Forward-Port Research Addendum

This section corrects the scope: the goal is to forward-port `lauralex/dwm_lut` to the latest Windows build, not merely explain why an old release fails.

### Tooling Status

Git was not on PATH at the start of the investigation. I installed Git for Windows with `winget`:

```powershell
winget install --id Git.Git -e --source winget --accept-package-agreements --accept-source-agreements --silent
```

After refreshing PATH in the shell, Git resolves to:

```text
C:\Program Files\Git\cmd\git.exe
git version 2.54.0.windows.1
```

Still missing from PATH on this machine:

| Tool | State | Why it matters |
| --- | --- | --- |
| `cl.exe` | missing | C++ compiler for `dwm_lut.dll` |
| `MSBuild.exe` | not on PATH | solution/project build driver |
| Visual Studio 2022 Build Tools | not confirmed | fixed fork targets `v143` |
| `cmake` | missing | useful for local dependencies and diagnostic tools |
| `nuget` | missing | restore `WindowsDisplayAPI` package if not vendored |

The next deliberate tooling install for a rebuild should be:

```powershell
winget install --id Microsoft.VisualStudio.2022.BuildTools -e --source winget --accept-package-agreements --accept-source-agreements --override "--wait --quiet --add Microsoft.VisualStudio.Workload.VCTools --add Microsoft.VisualStudio.Workload.ManagedDesktopBuildTools --add Microsoft.VisualStudio.Component.Windows11SDK.26100 --includeRecommended"
winget install --id Kitware.CMake -e --source winget --accept-package-agreements --accept-source-agreements
winget install --id Ninja-build.Ninja -e --source winget --accept-package-agreements --accept-source-agreements
```

Then bootstrap vcpkg:

```powershell
mkdir C:\dev
git clone https://github.com/microsoft/vcpkg.git C:\dev\vcpkg
C:\dev\vcpkg\bootstrap-vcpkg.bat
C:\dev\vcpkg\vcpkg integrate install
```

### Why A Clean Forward-Port Was Not Landed

The public trail points to four overlapping reasons.

1. Windows 24H2/25H2 changed the graphics/color pipeline, not just one byte pattern. lauralex issue [#60](https://github.com/lauralex/dwm_lut/issues/60) reports that the new ACM path in 24H2 crashed the app. Microsoft's Advanced Color documentation confirms that Windows now uses automatic system color management and FP16 DWM composition when Advanced Color is active. Source: [DirectX Advanced Color](https://learn.microsoft.com/en-us/windows/win32/direct3darticles/high-dynamic-range).

2. The core hook is private, low-level, and must be retuned every OS iteration. DisplayCAL forum participants explicitly connect the maintenance burden to low-level DWM work and repeated Windows changes. Source: [DisplayCAL dwm_lut thread, page 24](https://hub.displaycal.net/forums/topic/i-made-a-tool-for-applying-3d-luts-to-the-windows-desktop/page/24/).

3. The 25H2 fork apparently worked for simple cases, then failed in the cases color-critical users actually need. The forum records a 25H2 success report, followed by reports that `1.0.8` applied the main-screen LUT to secondary displays, applied profiles to all monitors, crashed taskbars after sleep/wake, and broke secondary/third displays when toggling HDR. Source: [DisplayCAL dwm_lut thread, page 23](https://hub.displaycal.net/forums/topic/i-made-a-tool-for-applying-3d-luts-to-the-windows-desktop/page/23/).

4. The maintainer of the 24H2/25H2 fixed fork archived the repository. GitHub shows `ed1ii/dwm_lut_fixed` was archived on 2026-03-28 and is now read-only. Source: [ed1ii/dwm_lut_fixed](https://github.com/ed1ii/dwm_lut_fixed). The DisplayCAL thread says the associated Discord disappeared after multi-monitor reports and paraphrases the maintainer as leaving the project because the low-level work was not where they wanted to spend energy. Source: [DisplayCAL page 24](https://hub.displaycal.net/forums/topic/i-made-a-tool-for-applying-3d-luts-to-the-windows-desktop/page/24/).

Git history supports that reading. The fixed fork has 25H2 tags through `v1.0.8_25H2`, but later commits emphasize "Official 23H2 Support - VERIFIED STABLE" and "Final multi-monitor support with per-context caching." The project still contains 24H2/25H2 code, but the public stability signal retreated toward verified 23H2 behavior.

The reason, therefore, is not "nobody found the `Present` signature." The reason is that a production-quality port must solve at least four moving pieces together:

| Moving piece | Why it breaks |
| --- | --- |
| `COverlayContext::Present` signature | function prologue and call ABI shift per DWM build |
| swap-chain/back-buffer retrieval | 25H2 changed internal object layout and sometimes no fixed offset is reliable |
| monitor/context matching | multiple DWM overlay contexts can share similar coordinates or shift after sleep/HDR/reconfigure |
| flip/MPO suppression | hardware overlays and independent flip can bypass the shader path unless correctly disabled |

### Proposed Forward-Port Architecture

The right forward-port is a controlled merge from lauralex `v4.0.2` plus the fixed fork's 25H2 lessons, but with a diagnostic-first architecture.

Create a branch:

```powershell
git clone https://github.com/lauralex/dwm_lut.git C:\dev\dwm_lut
cd C:\dev\dwm_lut
git checkout win24h2
git checkout -b forward-port/windows-25h2-26200
git remote add fixed https://github.com/ed1ii/dwm_lut_fixed.git
git fetch fixed --tags
```

Do not merge the fixed fork wholesale. Instead, port the following concepts:

1. `BuildProfile` table.

Define one data-driven profile per Windows/DWM family:

```cpp
struct DwmBuildProfile {
    const char* name;
    DWORD minBuild;
    DWORD maxBuild;
    Pattern present;
    Pattern overlayDirectFlip;
    Pattern overlaysEnabled;
    Pattern windowDirectFlip;
    Pattern compSwapChainDirectFlip;
    Pattern compVisualPromotion;
    int overlaySwapChainOffset;
    int hardwareProtectedOffset;
    int deviceClipBoxOffset;
};
```

This prevents the current bug where build `26200` is treated as `>=26100` 24H2.

2. Preflight scanner in the GUI.

Before injection, scan `C:\Windows\System32\dwmcore.dll` for the selected profile's patterns and show:

* DWM file version.
* DWM SHA-256.
* Windows build and UBR.
* matched profile.
* required pattern hit counts.
* optional pattern hit counts.
* "safe to inject" or "unsupported DWM build."

No required `Present` match, no injection.

3. Diagnostic DLL mode.

Add a mode such as `DWM_LUT_PROBE_ONLY=1` that hooks but does not apply the LUT. It should log:

* selected function addresses,
* hook entry counts,
* argument pointer ranges,
* `rectVec` count and rectangles,
* back-buffer dimensions and format,
* swap-chain retrieval method,
* monitor/LUT match decision,
* HDR/SDR selection,
* DirectFlip/MPO suppression status.

Log to:

```text
%LOCALAPPDATA%\DwmLut\logs\dwm_lut_probe_<timestamp>.log
```

Do not rely on `_DEBUG` logging to `C:\DWMLOG`.

4. 25H2 swap-chain/back-buffer resolver.

Implement a resolver chain, not a single offset:

```text
direct 25H2 back-buffer method
then known offset profile
then vtable-validated dynamic scan
then fail closed
```

The dynamic scan must validate that a candidate pointer behaves like a DXGI swap chain by calling safe COM methods under SEH guards. `IsBadReadPtr` alone is not enough.

5. Monitor matcher rebuild.

The forum failures point directly at monitor mapping. The existing "match by left/top coordinate" strategy is insufficient on multi-monitor, multi-GPU, sleep/wake, HDR toggle, and cloned/virtual topologies.

Use the GUI to write a manifest with:

```json
{
  "monitors": [
    {
      "sourceId": 123,
      "devicePathHash": "...",
      "displayName": "...",
      "connector": "...",
      "desktopRect": {"left":0,"top":0,"right":3840,"bottom":2160},
      "sdrLut": "...",
      "hdrLut": "..."
    }
  ]
}
```

Inside the DLL, still match by compositor rectangle, but log all candidates and refuse ambiguous matches unless a cached context has already been proven stable for the same render target dimensions and HDR/SDR mode. If two monitors collide, fail that monitor instead of applying the primary LUT to every display.

6. DirectFlip/MPO handling as optional capability, not assumed capability.

Windows flip model lets DWM compose from shared back buffers rather than classic redirection copies. Source: [DXGI flip model](https://learn.microsoft.com/en-us/windows/win32/direct3ddxgi/dxgi-flip-model). MPO lets hardware compose planes into the final image. Source: [MPO support](https://learn.microsoft.com/en-us/windows-hardware/drivers/display/multiplane-overlay-support). The port must detect and report whether it really disabled the relevant promotion paths.

7. Build the shader path last.

Only after probe logs prove that hook entry, rectangle mapping, and back-buffer retrieval are correct should `ApplyLUT` be enabled. Otherwise a bad port can corrupt the wrong surface and look like a color-science problem.

### AI-Assisted Rebuild Workflow

AI is useful here, but only if each step produces machine-checkable artifacts. The workflow should be:

1. Repository setup agent.

Tasks:

* normalize the lauralex `win24h2` branch,
* add the fixed fork remote,
* generate a patch inventory from `v4.0.2` to `v1.0.8_25H2`,
* separate GUI changes, parser changes, hook changes, logging changes, and packaging changes.

Command sketch:

```powershell
git diff --stat v4.0.2 fixed/v1.0.8_25H2
git diff v4.0.2 fixed/v1.0.8_25H2 -- lutdwm/dllmain.cpp > work\diffs\fixed_25h2_dllmain.diff
```

2. Pattern/miner agent.

Tasks:

* extract every AOB pattern from lauralex and fixed fork source,
* scan current `dwmcore.dll`,
* produce JSON: pattern name, hit count, offsets, required/optional.

The direct scan already found:

| Pattern | Hits |
| --- | ---: |
| lauralex 24H2 `Present` | `0` |
| fixed fork 25H2 `Present` | `1` |
| fixed fork 25H2 `OverlaysEnabled` | `1` |
| fixed fork 25H2 overlay direct-flip pattern | `2` |

3. ABI/reverse-engineering agent.

Tasks:

* verify hook typedefs against call sites,
* inspect prologues around candidate addresses,
* determine whether `COverlayContext_Present_24h2_t` remains valid on 25H2,
* flag multiple-hit patterns that require disambiguation.

AI can help compare disassembly, but it must not invent offsets. Offset candidates must come from runtime logs or disassembly.

4. Instrumentation agent.

Tasks:

* add `DWM_LUT_PROBE_ONLY`,
* add release logging,
* add preflight scanner,
* add `BuildProfile` table,
* keep patch small enough to review.

5. Build agent.

Tasks:

* restore NuGet packages,
* build C++ DLL x64 Release and Debug,
* build WPF GUI x64 Release,
* stage a release folder with exactly the three expected files.

Example build commands after VS2022 Build Tools:

```powershell
$vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
$install = & $vswhere -latest -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
Import-Module "$install\Common7\Tools\Microsoft.VisualStudio.DevShell.dll"
Enter-VsDevShell -VsInstallPath $install -DevCmdArguments "-arch=x64"

msbuild .\lutdwm.sln /m /p:Configuration=Release /p:Platform=x64 /p:VcpkgEnableManifest=true
msbuild .\DwmLutGUI\DwmLutGUI.sln /m /restore /p:Configuration=Release /p:Platform=x64
```

6. Runtime probe agent.

Tasks:

* launch GUI elevated,
* apply in probe-only mode,
* collect logs,
* summarize hook counts and monitor matches,
* refuse to proceed if any monitor is ambiguous.

7. Visual validation agent.

Tasks:

* use a deliberately obvious LUT,
* validate single monitor SDR,
* validate multi-monitor SDR,
* validate HDR toggle,
* validate sleep/wake,
* validate full-screen/video/browser cases,
* record failures by build number and DWM hash.

8. Release agent.

Tasks:

* include `dwmcore.dll` version/hash in release notes,
* list verified display topology,
* include "unsupported if preflight required patterns do not match."

### Concrete Next Patch Set

The first real forward-port patch should not touch color math. It should add the compatibility scaffolding:

1. `lutdwm\PatternScanner.h/.cpp`
2. `lutdwm\DwmBuildProfile.h`
3. `lutdwm\ProbeLog.h/.cpp`
4. GUI preflight command in `DwmLutGUI\Injector.cs`
5. `DWM_LUT_PROBE_ONLY` support in `dllmain.cpp`
6. a JSON/log dump of matched DWM profile before any `MH_CreateHook`

Then the second patch ports 25H2 hook support:

1. 25H2 build detection for `>=26200`.
2. fixed-fork `COverlayContext_Present_bytes_w11_25h2`.
3. fixed-fork `COverlayContext_OverlaysEnabled_bytes_w11_25h2`.
4. direct/dynamic swap-chain/back-buffer resolver.
5. guarded optional DirectFlip/MPO hooks.

The third patch attacks the actual reason the fork stalled:

1. monitor identity manifest,
2. context-to-monitor diagnostic table,
3. ambiguity fail-closed behavior,
4. tests across sleep/wake and HDR toggle.

## System Facts From This Machine

PowerShell inspection returned:

| Field | Value |
| --- | --- |
| OS name | Microsoft Windows 11 Pro |
| DisplayVersion | `25H2` |
| CurrentBuild | `26200` |
| UBR | `8655` |
| BuildLabEx | `26100.1.amd64fre.ge_release.240331-1435` |
| Architecture | 64-bit |
| `dwmcore.dll` path | `C:\Windows\System32\dwmcore.dll` |
| `dwmcore.dll` file version | `10.0.26100.8457` |
| `dwmcore.dll` size | `4,362,240` bytes |
| `dwmcore.dll` last write | `2026-06-10 11:39:42` local |
| Primary discrete GPU | NVIDIA GeForce RTX 3080 |
| NVIDIA driver | `32.0.15.9186`, dated `2026-01-20` |
| Integrated GPU | AMD Radeon(TM) Graphics |
| AMD driver | `32.0.21018.14`, dated `2025-06-12` |

Microsoft's current Windows 11 release information page lists Windows 11 `25H2` as OS build `26200`, with `26200.8655` corresponding to the 2026-06 B update and `26200.8737` as the 2026-06 D update as of 2026-06-23. It also lists Windows 11 `24H2` as OS build `26100`, now serviced in parallel with similar UBR revisions. Source: [Windows 11 release information](https://learn.microsoft.com/en-us/windows/release-health/windows11-release-information).

Local dwm_lut residue check:

| Path | State |
| --- | --- |
| `C:\Windows\Temp\dwm_lut.dll` | missing |
| `C:\Windows\Temp\luts` | missing |
| `C:\DWMLOG\dwm.log` | missing |
| `DwmLutGUI` process | not running |

So the current system is clean; there is no already-injected old DLL to unload.

## Release Archive Facts

Supplied archive extracted to:

`C:\Users\arsen\Documents\Codex\2026-06-28\lauralex-dwm-lut-https-github-com\work\release24h2\Release`

It contains:

| File | Size | Last write |
| --- | ---: | --- |
| `DwmLutGUI.exe` | `49,664` bytes | `2024-10-23 05:29:38` |
| `dwm_lut.dll` | `51,200` bytes | `2024-10-25 03:17:24` |
| `WindowsDisplayAPI.dll` | `67,072` bytes | `2020-02-10 20:30:40` |

Hashes:

| File | SHA-256 |
| --- | --- |
| `DwmLutGUI.exe` | `2D840067AF990866687F52806D2249533F1EAEC8CAA60F622616E58AE3BCCDBD` |
| `dwm_lut.dll` | `694DF68EB70B2852093F03DABE3E0D2163B20EB050AAE5A74545E5F8F2F81C3E` |
| `WindowsDisplayAPI.dll` | `B53A976A8E669CF59783409FBE453222EB736D4F9642E374F8C52087DBA8FE86` |

This aligns with the lauralex `v4.0.2` timeframe. The lauralex Git history shows `v4.0.2` on the `win24h2` branch at commit `f526824`, titled "Fixed offsets, winver recognition and debug messages", dated 2024-10-25. The default `master` branch has later README edits but not the 24H2 hook source in its mainline snapshot; that is important when comparing source.

## How dwm_lut Works

The project does not use a supported Windows display-calibration API. It applies a 3D LUT by injecting a DLL into `dwm.exe` and altering the Desktop Window Manager's rendering path.

High-level flow:

1. `DwmLutGUI.exe` obtains debug/process privileges.
2. The GUI copies `dwm_lut.dll` to `%WINDIR%\Temp\dwm_lut.dll`.
3. It copies selected LUT files to `%WINDIR%\Temp\luts`, naming them by monitor coordinate, for example `0_0.cube`.
4. It uses `CreateRemoteThread` on the DWM process, with `LoadLibraryA` as the thread start routine.
5. `dwm_lut.dll` runs inside `dwm.exe`.
6. The DLL scans `dwmcore.dll` for private function byte signatures.
7. If the private functions are found, MinHook hooks them.
8. The hook intercepts DWM presentation, copies the back buffer, runs a Direct3D pixel shader with the 3D LUT texture, then writes the transformed pixels back.
9. DirectFlip and MPO paths are disabled or patched because they can allow presentation paths that bypass normal DWM composition.

This is clever engineering, but it is intrinsically brittle. A monthly cumulative update can reorder functions, change a prologue, split a class, alter a structure layout, or move the rectangle/swap-chain field. When that happens, the hook does not merely become less accurate; it often cannot initialize at all.

Relevant project statements:

* lauralex README: the tool applies LUTs by "hooking into DWM" and says DirectFlip and MPO are disabled because they can bypass DWM. Source: [lauralex/dwm_lut](https://github.com/lauralex/dwm_lut).
* The fixed fork's documentation describes the same architecture as Direct3D hooking using vtable pinning and AOB scanning. Source: [dwm_lut_fixed documentation](https://github.com/ed1ii/dwm_lut_fixed/blob/master/DOCUMENTATION.md).

## The Exact Failure Mechanism

### lauralex `v4.0.2` OS selection

The lauralex `v4.0.2` source defines:

```cpp
versionInfo24h2.dwBuildNumber = 26100;

if (VerifyVersionInfo(&versionInfo24h2, VER_BUILDNUMBER, dwlConditionMask))
{
    isWindows11_24h2 = true;
}
```

The condition is "build number greater than or equal to 26100". Your system is build `26200`, so lauralex `v4.0.2` classifies it as 24H2. It has no separate 25H2 branch.

### lauralex `v4.0.2` 24H2 signatures

In `v4.0.2`, the 24H2 DWM signatures include:

| Symbol | Pattern summary |
| --- | --- |
| `COverlayContext_Present_bytes_w11_24h2` | `4C 8B DC 56 41 56` |
| `COverlayContext_IsCandidateDirectFlipCompatbile_bytes_w11_24h2` | `48 8B C4 48 89 58 ?? ... 33 DB` |
| `COverlayContext_OverlaysEnabled_bytes_relative_w11_24h2` | `E8 ?? ?? ?? ?? 84 C0 B8 04 00 00 00` |

Then DllMain only proceeds to hooks if the required addresses exist and at least one LUT was parsed:

```cpp
if ((COverlayContext_Present_orig && COverlayContext_IsCandidateDirectFlipCompatbile_orig &&
     COverlayContext_OverlaysEnabled_orig) ||
    (COverlayContext_Present_orig_24h2 && COverlayContext_IsCandidateDirectFlipCompatbile_orig_24h2 &&
     COverlayContext_OverlaysEnabled_orig) && numLuts != 0)
{
    MH_Initialize();
    ...
}
return FALSE;
```

If the `Present` address is missing, `DllMain` returns `FALSE`. `LoadLibraryA` inside `dwm.exe` fails, `CreateRemoteThread` exits with `0`, and the GUI reports:

`Failed to load or initialize DLL. This probably means that a LUT file is malformed or that DWM got updated.`

### Direct pattern scan result on this machine

I scanned your installed `C:\Windows\System32\dwmcore.dll` directly against the lauralex 24H2 AOBs:

| Pattern | Hit count | First offsets |
| --- | ---: | --- |
| `lauralex_v4.0.2_COverlayContext_Present_w11_24h2` | `0` | none |
| `lauralex_v4.0.2_COverlayContext_IsCandidateDirectFlipCompatible_w11_24h2` | `2` | `0x14ed8`, `0xb1414` |
| `lauralex_v4.0.2_COverlayContext_OverlaysEnabled_relative_w11_24h2` | `1` | `0x230748` |

This is the decisive result. The support hooks are partially findable, but the main presentation hook is not.

For completeness, I also scanned the old lauralex default-branch Windows 10/11 patterns:

| Old/default branch pattern | Hit count |
| --- | ---: |
| `COverlayContext_Present_w11` | `0` |
| `COverlayContext_IsCandidateDirectFlipCompatible_w11` | `0` |
| `COverlayContext_OverlaysEnabled_w11` | `0` |
| `COverlayContext_Present_old` | `0` |
| `COverlayContext_IsCandidateDirectFlipCompatible_old` | `0` |
| `COverlayContext_OverlaysEnabled_old` | `0` |

Therefore neither the old branch nor the 24H2 branch has a complete signature set for this DWM binary.

## Why the LUT File Is Probably Not the Culprit

A malformed `.cube` can absolutely make this application fail. The parser expects to find a `LUT_3D_SIZE` line, then a dense RGB triplet payload. The GUI also supports converting 65^3 eeColor `.txt` files. lauralex's injector error message intentionally conflates two possibilities: bad LUT or updated DWM.

But on this machine, the missing `Present` signature is sufficient to explain failure before color science enters the picture.

Cases where the LUT file would still matter:

* The folder has zero valid SDR/HDR LUT assignments, so `numLuts == 0`.
* The `.cube` file has comments, domain metadata, or negative values in a form the parser mishandles.
* SDR LUT assigned while the monitor is in HDR mode, or HDR LUT assigned while the monitor is in SDR mode.
* Duplicate-display mode is active. lauralex explicitly states LUTs cannot be applied to monitors in duplicate mode.
* The selected LUT is visually too subtle to notice. Test with an exaggerated/inverted LUT when validating.

Those are secondary checks. They do not alter the primary DWM signature failure.

## Why PATH Is Not the Fix

The user asked to "install on path". For this tool, PATH is mostly irrelevant.

`DwmLutGUI` uses:

```csharp
DllName = "dwm_lut.dll";
DllPath = "%SYSTEMROOT%\\Temp\\dwm_lut.dll";
File.Copy(AppDomain.CurrentDomain.BaseDirectory + DllName, DllPath, true);
```

So the DLL must sit next to `DwmLutGUI.exe` in the application folder. The GUI copies it to `C:\Windows\Temp` before injecting it. Windows PATH is not used to locate `dwm_lut.dll`.

For automation, you can start `DwmLutGUI.exe` with full path and flags such as `-apply`, `-minimize`, and `-exit`. Adding the folder to PATH will not make a stale DWM hook work.

## Relationship to Windows Advanced Color, HDR, DirectFlip, and MPO

Windows' modern color stack has moved a lot since the original dwm_lut design. This matters because dwm_lut sits in a private, moving part of the graphics stack.

Microsoft's Advanced Color documentation says:

* Advanced Color covers HDR, wide color gamut, automatic system color management, and high bit depth.
* When Advanced Color is enabled, DWM composes using FP16/scRGB.
* Windows color management in HDR has a DWM stage and a display-kernel stage.
* Microsoft's public display calibration pipeline is a GPU display color transform with a matrix and 1D LUT exposed through MHC ICC profiles, not arbitrary app-controlled 3D LUT injection.

Sources:

* [Use DirectX with Advanced Color on high/standard dynamic range displays](https://learn.microsoft.com/en-us/windows/win32/direct3darticles/high-dynamic-range)
* [Windows hardware display color calibration pipeline](https://learn.microsoft.com/en-us/windows/win32/wcs/display-calibration-mhc)

Microsoft's DXGI flip model documentation explains that flip-model swap-chain buffers are shared with DWM rather than copied through the older bitblt redirection surface path. Source: [DXGI flip model](https://learn.microsoft.com/en-us/windows/win32/direct3ddxgi/dxgi-flip-model).

Microsoft's MPO documentation describes hardware composition of multiple planes, where hardware can compose layers into one scanned-out image. Source: [Multiplane overlay support](https://learn.microsoft.com/en-us/windows-hardware/drivers/display/multiplane-overlay-support).

This is why dwm_lut keeps chasing:

* `COverlayContext::Present`
* `COverlayContext::IsCandidateDirectFlipCompatible`
* `COverlayContext::OverlaysEnabled`
* `OverlayTestMode`
* swap-chain offsets
* monitor clip-rectangle offsets

If an application or the driver can promote a surface to independent flip or MPO, the pixels can escape the DWM shader pass. dwm_lut therefore has to fight both private DWM changes and public performance features.

## Evidence From the Maintenance Fork

The maintenance fork claims explicit support for 24H2 and 25H2 and says it improves multi-monitor/multi-GPU, HDR/SDR switching, and MPO/DirectFlip handling. Source: [zkippp/dwm_lut_fixed](https://github.com/zkippp/dwm_lut_fixed).

The fork's documentation specifically says Windows 11 build `26200` / 25H2 refactored DWM internals and that it addresses this by:

* adding 25H2 compatibility logic,
* changing swap-chain discovery,
* changing device clip-box offsets,
* patching `g_pOverlayTestMode`.

Source: [dwm_lut_fixed documentation](https://github.com/ed1ii/dwm_lut_fixed/blob/master/DOCUMENTATION.md).

I scanned the fixed fork's 25H2 signatures against your `dwmcore.dll`:

| Fixed fork 25H2 pattern | Hit count | First offsets |
| --- | ---: | --- |
| `fixed_COverlayContext_Present_w11_25h2` | `1` | `0x231800` |
| `fixed_COverlayContext_IsCandidateDirectFlipCompatible_w11_25h2` | `2` | `0x14ed8`, `0xb1414` |
| `fixed_COverlayContext_OverlaysEnabled_w11_25h2` | `1` | `0x1a2be8` |
| `fixed_CWindowContext_IsCandidateDirectFlipCompatible_w11_25h2` | `0` | none |
| `fixed_CCompSwapChain_IsCandidateDirectFlipCompatible_w11_25h2` | `0` | none |
| `fixed_CCompVisual_IsCandidateForPromotion_w11_25h2` | `0` | none |

Interpretation:

* The essential `Present` and `OverlaysEnabled` signatures match this system.
* The DirectFlip-compatible pattern has two candidates, so a build must choose the correct one.
* Optional extra anti-promotion hooks do not match this exact serviced DWM binary, which means the fork may still initialize but may not completely suppress every independent flip/MPO promotion path on every workload.
* The fork is a plausible practical replacement, but it is still private-hook software. It should be tested with an exaggerated LUT and a known set of applications before trusting it for color-critical work.

## Upgrade Options

### Option A: Use a trusted 25H2 build of `dwm_lut_fixed`

This is the fastest path if you can obtain the binary from the upstream release page you trust:

* [zkippp/dwm_lut_fixed releases](https://github.com/zkippp/dwm_lut_fixed/releases)
* Relevant tag observed by Git: `v1.0.8_25H2`

Do not mix files from different builds. Keep these together in one folder:

* `DwmLutGUI.exe`
* `dwm_lut.dll`
* `WindowsDisplayAPI.dll`

Run the GUI elevated. Apply a strong test LUT first. Then inspect whether `dwm_lut.dll` is loaded into `dwm.exe`.

Notes:

* I did not execute any third-party replacement binary in `dwm.exe`.
* I did not install a full build toolchain because the report did not require running unverified code.
* GitHub release asset links were not visible from static HTML in this environment, and the public GitHub API was rate-limited. That is why this report treats the fixed fork source as an upgrade base, not as a blindly installed binary.

### Option B: Forward-port 25H2 support into lauralex

Patch lauralex `v4.0.2` with at least:

1. A separate `isWindows11_25h2` branch for build `>=26200`.
2. A 25H2 `COverlayContext::Present` signature that matches this DWM binary.
3. Updated 25H2 `COverlayContext::IsCandidateDirectFlipCompatible` and `OverlaysEnabled` patterns.
4. Updated monitor-coordinate lookup.
5. Updated swap-chain discovery. The fixed fork attempts direct back-buffer retrieval and dynamic swap-chain offset probing for 25H2.
6. Better disambiguation when AOB signatures produce multiple hits.
7. A release-mode diagnostic path. The current release has no useful log unless built with `_DEBUG`.
8. A preflight scanner in the GUI before injection, so the user sees "this Windows DWM build is unsupported" instead of a generic malformed-LUT/DWM error.

Build requirements:

* Visual Studio 2022.
* C++ Desktop Development workload.
* .NET Desktop Development workload / .NET Framework 4.8 targeting pack.
* vcpkg with `minhook`.
* x64 Release build.

This machine has Visual Studio 2019 MSBuild files present but no `cl.exe` on PATH and no visible C++ compiler. The fixed fork project targets `v143`, so local source build would require installing VS2022 Build Tools or retargeting and validating with VS2019, which is not ideal for a DWM injection project.

### Option C: Use Windows' supported calibration path where possible

If the calibration can be represented as matrix plus 1D LUT, use Windows' MHC ICC pipeline rather than DWM injection. This is supported by Windows and survives DWM refactors.

Limitations:

* MHC does not provide arbitrary 3D `.cube` transformation in the way dwm_lut does.
* MHC profiles are best for display calibration, white point, primaries correction, tone response, HDR metadata, and similar transforms.
* Creative 3D LUT looks, film emulation, complex display nonlinearity correction, or per-app arbitrary 3D LUT processing still exceed what the supported Windows calibration path exposes.

### Option D: Roll back

Rollback is the least attractive technical path because it trades a display hook for OS security and platform state.

If rollback is still required:

* lauralex `v4.0.2` was released in the October 2024 24H2 timeframe. Windows 11 24H2 builds near `26100.2033` to `26100.2161` are much closer to its intended target than this 25H2 `26200.8655` machine.
* lauralex README says to use `v3.9.6` if `v4.0.2` has problems. That is not a 25H2 fix; it is a fallback for older Windows paths.
* For a stable, non-Insider machine, Windows 11 `23H2` / build `22631` plus an older dwm_lut release is likely less volatile than 25H2, but it is also older and eventually loses consumer servicing.

Rollback should be done only with a system image or restore plan. Do not roll back just by replacing `dwmcore.dll`.

## Recommended Immediate Test Plan

1. Stop any running `DwmLutGUI.exe`.
2. Confirm no stale injection:

```powershell
Get-Process -Name DwmLutGUI -ErrorAction SilentlyContinue
Get-Item "$env:WINDIR\Temp\dwm_lut.dll" -ErrorAction SilentlyContinue
```

3. Obtain a 25H2-aware build, preferably from `zkippp/dwm_lut_fixed` source or release.
4. Keep `DwmLutGUI.exe`, `dwm_lut.dll`, and `WindowsDisplayAPI.dll` in one folder.
5. Run `DwmLutGUI.exe` elevated.
6. Assign a deliberately obvious SDR LUT to one monitor, not a subtle calibration LUT.
7. Click Apply.
8. Check whether the module loaded:

```powershell
$dwm = Get-Process -Name dwm
$dwm.Modules | Where-Object ModuleName -eq 'dwm_lut.dll'
```

This may require elevated PowerShell.

9. If it loads but the visual output does not change, test:

* HDR off with SDR LUT.
* HDR on with a valid HDR LUT.
* no display duplication.
* single-monitor mode.
* one GPU active if possible.
* apps that are not exclusive fullscreen.
* a static desktop/windowed test image rather than a protected video surface.

10. If it does not load, the build still does not match the current DWM binary. Run the AOB scanner against its source patterns before trying more LUT files.

## What a Robust Future Version Should Do

A more maintainable dwm_lut should include:

1. A preflight compatibility scanner in the GUI.
2. A machine-readable table of supported Windows build ranges and DWM file hashes.
3. A release-mode diagnostic file under `%LOCALAPPDATA%`, not only `_DEBUG` logging to `C:\DWMLOG`.
4. Explicit warning when the OS is build `26200+` but the DLL only has `26100` signatures.
5. Pattern disambiguation when multiple hits occur.
6. Symbol-guided development notes, if private symbols or reverse-engineering metadata are being used by maintainers.
7. A safer apply path that fails before injecting if the required AOBs are absent.
8. A mode that uses supported MHC ICC profile generation when the transform can be represented as matrix plus 1D LUT.
9. A visible "composition path health" panel: HDR/SDR state, duplicate mode, loaded module state, DWM build hash, active LUT count, matching monitor coordinates, and DirectFlip/MPO suppression status.

The current UX hides too much behind "Failed to load or initialize DLL", which is accurate but not actionable.

## Search Log

I ran more than 20 targeted web searches. Selected queries:

1. `lauralex dwm_lut GitHub Windows 24H2 LUT not applying`
2. `site:github.com/lauralex/dwm_lut dwm_lut 24H2`
3. `dwm_lut Windows 11 24H2 not working`
4. `lauralex/dwm_lut issues Windows 11 24H2`
5. `GitHub lauralex dwm_lut repository branch master`
6. `lauralex/dwm_lut releases Release24h2 dwm_lut.dll`
7. `lauralex dwm_lut fork ledoge Release24h2`
8. `github lauralex dwm_lut raw README`
9. `site:github.com/lauralex/dwm_lut/issues 24H2 dwm_lut`
10. `site:github.com/lauralex/dwm_lut/issues 26100 dwm_lut`
11. `site:github.com/lauralex/dwm_lut/issues "DWM got updated"`
12. `site:github.com/lauralex/dwm_lut/issues "Failed to load or initialize DLL"`
13. `Windows 11 24H2 Advanced Color Management ACM color management release notes`
14. `Windows 11 Auto Color Management ACM 24H2 DWM SDR HDR color pipeline`
15. `Windows 11 24H2 DirectFlip Multiplane Overlay DWM bypass color management`
16. `Windows 11 24H2 MPO DirectFlip LUT not applied DWM`
17. `Microsoft Advanced Color Windows DirectX automatic color management developer blog`
18. `Microsoft Docs Advanced Color automatic color management Windows 11 SDR displays`
19. `Microsoft Docs DirectFlip Independent Flip Multiplane Overlay DWM DXGI`
20. `Microsoft hardware drivers multiplane overlays WDDM DirectFlip documentation`
21. `Windows 11 version 24H2 display color management release notes Auto Color Management`
22. `Windows 11 24H2 WDDM 3.2 display driver model release notes`
23. `Windows 11 24H2 known issues color management HDR Microsoft`
24. `Windows 11 24H2 automatic color management toggle display settings Microsoft`
25. `ledoge dwm_lut Windows 11 24H2 issue`
26. `dwm_lut alternative Windows 11 24H2 LUT applying desktop`
27. `dwm_lut Release24h2 v4.0.2 lauralex release notes`
28. `github lauralex dwm_lut v4.0.2 Release24h2`
29. `site:learn.microsoft.com "Windows hardware display color calibration pipeline"`
30. `site:learn.microsoft.com "MHC ICC profile" "display calibration"`
31. `Windows 11 version 25H2 update history Microsoft support 26200`
32. `Windows 11 release information current versions 25H2 24H2 Microsoft`
33. `site:github.com/zkippp/dwm_lut_fixed releases v1.0.8`
34. `zkippp dwm_lut_fixed 25H2 release`
35. `site:learn.microsoft.com/en-us/windows/win32/direct3darticles "Advanced Color" "Automatic System Color Management"`
36. `site:learn.microsoft.com/en-us/windows-hardware/drivers/display "multiplane overlay" "DirectFlip"`
37. `"dwm_lut" "25H2" forum`
38. `"dwm_lut_fixed" "Official 23H2 Support"`
39. `"dwm_lut_fixed" "GPU spikes"`
40. `"dwm_lut" "COverlayContext"`

## Source Index

Primary project sources:

* [lauralex/dwm_lut](https://github.com/lauralex/dwm_lut)
* [lauralex/dwm_lut v4.0.2 release](https://github.com/lauralex/dwm_lut/releases/tag/v4.0.2)
* [lauralex/dwm_lut issue 86](https://github.com/lauralex/dwm_lut/issues/86)
* [lauralex/dwm_lut issue 60](https://github.com/lauralex/dwm_lut/issues/60)
* [zkippp/dwm_lut_fixed](https://github.com/zkippp/dwm_lut_fixed)
* [ed1ii/dwm_lut_fixed archived fork](https://github.com/ed1ii/dwm_lut_fixed)
* [dwm_lut_fixed documentation](https://github.com/ed1ii/dwm_lut_fixed/blob/master/DOCUMENTATION.md)
* [DisplayCAL dwm_lut forum thread, page 23](https://hub.displaycal.net/forums/topic/i-made-a-tool-for-applying-3d-luts-to-the-windows-desktop/page/23/)
* [DisplayCAL dwm_lut forum thread, page 24](https://hub.displaycal.net/forums/topic/i-made-a-tool-for-applying-3d-luts-to-the-windows-desktop/page/24/)

Microsoft references:

* [Windows 11 release information](https://learn.microsoft.com/en-us/windows/release-health/windows11-release-information)
* [Use DirectX with Advanced Color on high/standard dynamic range displays](https://learn.microsoft.com/en-us/windows/win32/direct3darticles/high-dynamic-range)
* [Windows hardware display color calibration pipeline](https://learn.microsoft.com/en-us/windows/win32/wcs/display-calibration-mhc)
* [DXGI flip model](https://learn.microsoft.com/en-us/windows/win32/direct3ddxgi/dxgi-flip-model)
* [Multiplane overlay support](https://learn.microsoft.com/en-us/windows-hardware/drivers/display/multiplane-overlay-support)

Local files inspected:

* `work\release24h2\Release\DwmLutGUI.exe`
* `work\release24h2\Release\dwm_lut.dll`
* `work\release24h2\Release\WindowsDisplayAPI.dll`
* `work\dwm_lut-git`
* `work\dwm_lut-v4.0.2`
* `work\dwm_lut_fixed-master`
* `C:\Windows\System32\dwmcore.dll`

## Bottom Line

On this machine, lauralex `Release24h2.zip` is the wrong generation of hook for the installed DWM. It recognizes the OS as 24H2 because build `26200` is greater than `26100`, then fails to find the 24H2 `COverlayContext::Present` pattern. That one missing function is enough to stop LUT application.

The viable forward path is a 25H2-aware build. The fixed fork's source matches the essential signatures on this machine and is therefore the best available upgrade base. A Windows rollback would also work only if it returns DWM to a build whose private function layout matches the chosen release, but that is a heavier and riskier solution than updating the hook.

## 2026-06-29 Compatibility Update: Current PC, Multi-Monitor Risk, and Next Builds

### Current machine status

The test PC is on Windows 11 25H2 build `26200.8655` with `C:\Windows\System32\dwmcore.dll` version `10.0.26100.8457`, file length `4,362,240`, SHA-256 `03C820B55D3CF470C9E80C9905186F43937A7E79CC1D81BCE524DA823B6FCA8D`. The current Microsoft stable servicing tip I found for the 25H2 branch is `26200.8737`, published as KB5095093 on 2026-06-23. In other words, this machine is inside the correct 25H2 family for the fixed fork, but is one cumulative preview update behind the latest public stable servicing build.

The local binary scan against this exact `dwmcore.dll` gives the following 25H2 fixed-fork signature result:

| 25H2 signature | Hits in current `dwmcore.dll` | Interpretation |
| --- | ---: | --- |
| `COverlayContext_Present_bytes_w11_25h2` | 1 at `0x231800` | Good. This is the main presentation hook. |
| `COverlayContext_IsCandidateDirectFlipCompatbile_bytes_w11_25h2` | 2 at `0x14ed8`, `0xb1414` | Risky. The fork takes the first match, so this should be verified when forward-porting. |
| `COverlayContext_OverlaysEnabled_bytes_w11_25h2` | 1 at `0x1a2be8` | Good. This is used to alter overlay behavior. |
| `CWindowContext_IsCandidateDirectFlipCompatible_bytes_w11_25h2` | 0 | Optional suppression hook missing on this build. |
| `CCompSwapChain_IsCandidateDirectFlipCompatible_bytes_w11_25h2` | 0 | Optional suppression hook missing on this build. |
| `CCompVisual_IsCandidateForPromotion_bytes_w11_25h2` | 0 | Optional promotion hook missing on this build. |

That explains the practical result: the build can apply a LUT on this single-monitor system, but it is not a fully verified, future-proof 25H2 port.

### Downsides of the current 25H2 fixed build

1. It is a private-DWM hook. It depends on byte patterns and offsets inside `dwmcore.dll`, not on a Microsoft-supported API. Any cumulative update can move or rewrite the targeted code.
2. The current release binary is not a diagnostic build. It can fail optional hook discovery silently unless rebuilt with logging.
3. Multi-monitor handling is not trustworthy for 25H2. The 25H2 path reads a hardcoded rectangle from the internal overlay context and falls back to `(0,0)` when the rectangle looks invalid. That is acceptable for this user's single-monitor setup, but it can select the wrong monitor or collapse distinct monitors onto the primary display.
4. DirectFlip / Independent Flip / MPO suppression is incomplete on the tested `dwmcore.dll`. The required presentation hook is present, but optional DirectFlip/MPO-related signatures do not match. Some full-screen, video, overlay, or protected swap-chain paths may bypass the LUT.
5. The 25H2 `IsCandidateDirectFlipCompatible` signature has two matches. Duplicate matches are a sign that the pattern is too broad or that the matching code exists in more than one nearby layer. A forward port should disambiguate the target by surrounding control flow, xref, or function behavior.
6. HDR is code-supported, not fully validated here. The fork has an HDR branch and handles HDR-ish backbuffer formats, but the only visual test performed on this PC used creative camera LUTs, not a calibrated HDR display LUT. A calibrator-generated HDR LUT must be tested separately in actual Windows HDR mode.
7. Protected content and hardware-protected swap chains may be skipped or behave inconsistently. The code has offsets for hardware protection state, but this is another private field and should not be treated as stable.

### Is this the latest possible Windows build for this tool?

For the packaged `v1.0.8_25H2` build: no, not exactly. It works on this PC's `26200.8655`, and it probably has a reasonable chance on the current 25H2 servicing tip `26200.8737`, but that must be confirmed by scanning `dwmcore.dll` after the update. The safest rule is: do not assume a new Windows build is compatible until the three required signatures are found and the optional flip/promotion hooks are audited.

For the latest Windows build available in general: no. Microsoft has newer Insider builds beyond the 25H2 `26200` line. Those are not automatically compatible with this build. A higher build number does not mean "same DWM with a bigger number"; it may mean a different platform branch.

### Which next build should be targeted?

The best next engineering target is Windows 11 26H2 Experimental, currently observed as build `26300.8758` from Microsoft's 2026-06-26 Insider announcement. Microsoft described 26H2 as sharing the same servicing/core line as 24H2 and 25H2, enabled via an enablement package. That makes it the most likely branch where the 25H2 method can be transferred with new patterns and offsets.

The second target is Beta 25H2 build `26220.8754`. This is useful because it is close to the current 25H2 family and can catch near-term cumulative changes before they hit stable.

The less attractive target is 26H1, with builds such as Beta `28020.2366` and Experimental `28120.2374`. Microsoft has positioned 26H1 as a different platform track associated with Copilot+ PC silicon, not the straightforward successor to 24H2/25H2 for normal x64 desktop systems. It may still be worth researching, but it is not the best next daily-driver porting target for this PC.

The furthest-ahead target is the Future Platforms / Canary branch, observed as `29617.1000`. That is the "latest ever" public preview line I found, but it is too volatile to be the first port. It is useful for reconnaissance only: extract `dwmcore.dll`, run the scanner, record how badly the signatures drifted, and do not use it as the calibration machine.

### Forward-port strategy

Transfer the 25H2 fix by method, not by blindly reusing constants.

1. Add a build-profile table instead of broad booleans like `isWindows11_25h2 = build >= 26200`. Profiles should be keyed by `DisplayVersion`, build range, `dwmcore.dll` file version, and ideally SHA-256.
2. Split "required hooks" from "optional hooks." Required: `COverlayContext::Present`, the DirectFlip-compatibility hook currently used by the fork, and `COverlayContext::OverlaysEnabled`. Optional: `CWindowContext`, `CCompSwapChain`, and `CCompVisual` promotion/flip helpers.
3. Build a standalone scanner that can run on a copied `dwmcore.dll` without injection. It should report hits, duplicates, missing optional hooks, and bytes around each match.
4. For 26H2 `26300.x`, first test the existing 25H2 signatures. If required signatures still match uniquely, add a `26H2_26300` profile and validate with a neutral identity LUT plus a visible test LUT.
5. If required signatures do not match, use the 25H2 pattern locations as anchors in a disassembler and derive new AOBs from function prologues/control-flow regions that are unique inside the module.
6. Replace the 25H2 monitor-coordinate fallback with a single-monitor fast path for this use case. If there is one active display, always select the sole configured LUT instead of trusting uncertain internal overlay coordinates.
7. Rebuild a diagnostic DLL first. It should log build detection, selected profile, signature hits, duplicate hit counts, selected function addresses, swap-chain/backbuffer path, pixel format, HDR/SDR branch, and whether DirectFlip/MPO hooks were installed.
8. Only after diagnostic proof should the release DLL be produced.

### Practical recommendation

Keep this current PC on stable `26200.8655` or update only within the stable 25H2 `26200` servicing line after scanning `dwmcore.dll`. Do not jump the calibration machine straight to Canary or 26H1.

For developing the next fix, use a second PC or sacrificial install and target:

1. `26220.8754` Beta 25H2 for near-term breakage.
2. `26300.8758` Experimental 26H2 for the likely next annual branch.
3. `29617.1000` Canary/Future Platforms only for early warning.

Relevant Microsoft sources:

* [Windows 11 release information](https://learn.microsoft.com/en-us/windows/release-health/windows11-release-information)
* [KB5095093: OS Builds 26200.8737 and 26100.8737 Preview](https://support.microsoft.com/en-us/topic/june-23-2026-kb5095093-os-builds-26200-8737-and-26100-8737-preview-0e2a20f2-cf9e-46f8-9f08-e6996220882d)
* [Windows Insider announcement, 2026-06-26](https://blogs.windows.com/windows-insider/2026/06/26/announcing-new-builds-for-26-june-2026-retail-launch-of-new-wip-improvements/)
* [Windows Insider announcement, 2026-06-19, 26H2 Experimental](https://blogs.windows.com/windows-insider/2026/06/19/announcing-new-builds-for-19-june-2026-26h2-for-experimental/)

## 2026-06-29 Deep-Dive Addendum: Canary DWM Payloads, Symbols, and dwm_lut Engine Teardown

### Scope and evidence

This addendum answers the harder question: not "can we bump a build number," but "what actually has to be understood to keep `dwm_lut` alive across current and next Windows compositor builds?"

The new work performed here:

* Pulled UUP metadata for Windows 11 Insider Preview `29617.1000` from the public UUP dump API, using `search=29617.1000`, `lang=en-us`, `edition=professional`. UUP dump is an index, not an official Microsoft release authority, but the payload URLs it returned were Microsoft delivery URLs under `dl.delivery.mp.microsoft.com`.
* Downloaded and searched the relevant ESD payloads until the DWM component was found.
* Extracted the Canary `dwmcore.dll`, `dwm.exe`, `dwmapi.dll`, `dwmredir.dll`, `dwmscene.dll`, `uDWM.dll`, and `dcomp.dll`.
* Downloaded matching public symbols for both this PC's current `dwmcore.dll` and Canary `29617.1000` from the Microsoft public symbol server.
* Compared the fixed fork's 24H2/25H2 byte signatures against both current 25H2 and Canary 29617.
* Re-read the GUI/injector and native DLL code as a control plane plus injected render engine, not just as a release zip.

Important source links:

* Microsoft stable release table: [Windows 11 release information](https://learn.microsoft.com/en-us/windows/release-health/windows11-release-information)
* Latest stable 25H2 servicing package found during this investigation: [KB5095093, OS builds 26200.8737 and 26100.8737, 2026-06-23](https://support.microsoft.com/en-us/topic/june-23-2026-kb5095093-os-builds-26200-8737-and-26100-8737-preview-0e2a20f2-cf9e-46f8-9f08-e6996220882d)
* Microsoft Insider build announcement used for branch map: [Windows Insider announcement, 2026-06-26](https://blogs.windows.com/windows-insider/2026/06/26/announcing-new-builds-for-26-june-2026-retail-launch-of-new-wip-improvements/)
* Microsoft 26H2 Experimental branch context: [Windows Insider announcement, 2026-06-19](https://blogs.windows.com/windows-insider/2026/06/19/announcing-new-builds-for-19-june-2026-26h2-for-experimental/)
* UUP metadata entry point used for Canary discovery: [UUP dump API search for 29617.1000](https://api.uupdump.net/listid.php?search=29617.1000&sortByDate=1)

Local artifacts produced:

| Artifact | Local path |
| --- | --- |
| Canary UUP metadata | `work/canary_29617_get_enus_pro.json` |
| Extracted Canary DWM files | `work/canary_29617/extracted/` |
| Current 25H2 public symbols | `work/symbols/dwmcore_current/dwmcore.pdb` |
| Canary 29617 public symbols | `work/symbols/dwmcore_canary_29617/dwmcore.pdb` |
| Key symbol comparison | `work/symbols/dwmcore_key_symbols_compare.txt` |
| Overlay-control symbol comparison | `work/symbols/dwmcore_overlay_control_symbols_compare.txt` |

### Latest build map as of 2026-06-29

As of this run, the meaningful Windows branches for this project are:

| Channel / branch | Build observed | Why it matters for `dwm_lut` |
| --- | ---: | --- |
| Stable 25H2 | `26200.8737` | Current public stable servicing tip I found. Your PC is close at `26200.8655`. |
| Beta 25H2 | `26220.8754` | Best near-term breakage detector for the same 25H2 family. |
| Experimental 26H2 | `26300.8758` | Best next annual-branch target because Microsoft described 26H2 as sharing the 24H2/25H2 core and servicing line. |
| Beta 26H1 | `28020.2366` | Different platform track. Useful research target, poor daily-driver target for this PC. |
| Experimental 26H1 | `28120.2374` | Same concern as Beta 26H1. |
| Canary / Future Platforms | `29617.1000` | Furthest-ahead reconnaissance target. Too volatile to treat as the next practical user build. |

That branch map matters. The old mental model "higher build number means same DWM plus moved offsets" is wrong. `26300.x` is the likely next practical transfer target; `29617.1000` is a stress test of the method.

### Where DWM lives inside the Canary UUP payload

The Canary DWM compositor binaries were not in every client ESD. `Microsoft-Windows-Client-Desktop-Required-Package.esd` and `Microsoft-Windows-Client-Features-Package.esd` contained many shell/display related pieces, but not the classic DWM compositor core. The relevant payload was:

| UUP file | Size | SHA-256 |
| --- | ---: | --- |
| `Microsoft-Win4-Feature.esd` | `201,080,658` | `f1386664379c49b47f0c4b2bceb05ed9e9933d7aa3e93097211df5642be958de` |

Extracted DWM-adjacent files from `Microsoft-Win4-Feature.esd`:

| File | Size | Role |
| --- | ---: | --- |
| `dwmcore.dll` | `4,079,616` | Core compositor implementation and the main hook target. |
| `dwm.exe` | `126,976` | DWM process host. |
| `uDWM.dll` | `1,228,800` | User-interface/window-manager layer around the compositor. |
| `dwmscene.dll` | `2,400,256` | Scene/composition support library. |
| `dwmredir.dll` | `229,376` | Redirection support. |
| `dwmapi.dll` | `173,832` | Public DWM API surface. |
| `dcomp.dll` | `2,165,456` | DirectComposition runtime/API component. |

The extracted Canary `dwmcore.dll` had a CodeView debug record pointing to `dwmcore.pdb` with GUID `{9D2F7102-1CDA-8409-2018-100E70836B6E}`, age `1`. The Microsoft symbol-server path therefore resolves as:

`https://msdl.microsoft.com/download/symbols/dwmcore.pdb/9D2F71021CDA84092018100E70836B6E1/dwmcore.pdb`

This PC's current `C:\Windows\System32\dwmcore.dll` points to `dwmcore.pdb` GUID `{35D945A7-1171-77C7-B6CD-6D7D9ED0A2E8}`, age `1`, which resolves as:

`https://msdl.microsoft.com/download/symbols/dwmcore.pdb/35D945A7117177C7B6CD6D7D9ED0A2E81/dwmcore.pdb`

Both current and Canary `dwmcore.dll` also advertise CET compatibility and hotpatch compatibility in their extended DLL characteristics. That does not make private hooking safe; it just means the binary is built with modern control-flow and patchability metadata.

### Signature results: current 25H2 versus Canary 29617

The fixed fork's 25H2 byte signatures do match the minimum path on this PC, but they do not transfer to Canary.

| Pattern family | Current 25H2 `26200.8655` result | Canary `29617.1000` result | Meaning |
| --- | ---: | ---: | --- |
| 25H2 `COverlayContext::Present` | 1 hit at `0x231800` | 0 hits | Main presentation hook survives current 25H2, not Canary. |
| 25H2 `COverlayContext::IsCandidateDirectFlipCompatible` | 2 hits at `0x14ed8`, `0xb1414` | 0 hits | Current pattern is too broad. PDB says `0xb1414` is the real one. |
| 25H2 `COverlayContext::OverlaysEnabled` | 1 hit at `0x1a2be8` | 0 hits | Current overlay-control anchor disappears in Canary. |
| 25H2 optional window/swapchain/visual flip hooks | 0 hits | 0 hits | The current build is already partial here. |
| 24H2 patterns against Canary | Not relevant | 0 hits | Canary is not backward-compatible with 24H2 signatures. |

The critical correction: on your current machine the fixed fork's `IsCandidateDirectFlipCompatible` signature produces two matches. The public PDB proves the real `COverlayContext::IsCandidateDirectFlipCompatible` is RVA `0xb1414`, not the earlier `0x14ed8` hit. Any serious forward-port must stop using "first AOB hit wins" for this function.

### Public symbol comparison

The PDBs expose enough public symbols to guide a real port. These are RVAs, not absolute loaded addresses.

| Symbol | Current 25H2 RVA | Canary 29617 RVA | Porting interpretation |
| --- | ---: | ---: | --- |
| `COverlayContext::Present` | `0x231800` | `0x5e534` | Main hook function moved and was likely recompiled/reordered heavily. |
| `COverlayContext::PresentMPO` | `0x231cdc` | `0xe9878` | MPO path remains identifiable by symbol. |
| `COverlayContext::IsCandidateDirectFlipCompatible` | `0xb1414` | `0x17fabc` | Still present, but signature and access level changed enough to require a fresh profile. |
| `COverlayContext::OverlaysEnabled` | `0x1a2be8` | missing | Current overlay anchor cannot be relied on for Canary. |
| `CCommonRegistryData::m_dwOverlayTestMode` | `0x3fb22c` | `0x3b8688` | Better anchor than `OverlaysEnabled`; still exposed in publics. These are corrected PE RVAs, not raw PDB section offsets. |
| `COverlayContext::UpdateMPOCaps` | `0x1a2a1c` | `0x7cc58` | Candidate nearby logic for overlay capability behavior. |
| `COverlayContext::UpdateHDRMetaData` | `0x1d1ef0` | `0x1bafc4` | HDR path remains identifiable. |
| `COverlaySwapChain::IsHardwareProtected` | `0x1eafc0` | `0x1abbb0` | Protected-content check remains identifiable. |
| `CDDisplaySwapChain::GetBackBuffer` | `0x2c830` | `0x1acfb0` | Potentially better backbuffer route than vtable-index guessing. |
| `CDDisplaySwapChain::PresentDFlip` | `0x217d90` | `0x1dcb10` | DirectFlip presentation path remains visible. |
| `CD3DDevice::PresentMPO` | `0x1d8bc0` | `0x1ace74` | MPO device presentation path remains visible. |

This is the strongest evidence that a Canary port is possible as a research project, but not by copying the 25H2 byte arrays. The symbols tell us the conceptual targets still exist; the byte signatures say the current resolver cannot find them.

### Why this was not ported cleanly in the first place

There is no stable public DWM plugin API for "apply a user 3D LUT at the compositor backbuffer." The project therefore works by injecting a native DLL into `dwm.exe`, locating private functions in `dwmcore.dll`, and patching/hooking those functions at runtime. Microsoft is free to reorder, inline, specialize, rename, split, or remove these private internals in any cumulative update.

The fixed fork's history and code both support the same reading:

* The project can be made to work on specific builds.
* Every working build is a negotiated truce with one private `dwmcore.dll` layout.
* Later "verified stable" language around 23H2 is not random conservatism. It reflects the reality that 24H2/25H2 introduced significant flip/MPO/backbuffer changes, and the 25H2 port is rougher than a fully stabilized branch.
* The 25H2 code in the fixed fork is useful, but it mixes solid fixes with emergency heuristics: hardcoded object offsets, vtable-index probing, dynamic swap-chain scans, duplicated hook install blocks, and broad signatures.

So the reason is not "nobody tried hard enough." The reason is that DWM private ABI drift is the product. Forward-porting is reverse engineering plus runtime validation for each DWM build family.

### What the app actually is: control plane plus injected compositor engine

`dwm_lut` is best understood as two programs cooperating across a privilege boundary.

#### 1. GUI and configuration layer

The WPF GUI owns configuration and user flow. It stores monitor/LUT state in `config.xml` beside the GUI executable. Monitors are discovered through `WindowsDisplayAPI.DisplayConfig.PathInfo.GetActivePaths()`. Monitor identity is based on the display target device path, while the staged LUT filename is based on monitor position, for example `0_0.cube` and `0_0_hdr.cube`.

The GUI supports command-line control:

| Flag | Effect |
| --- | --- |
| `-apply` | Apply/inject configured LUTs. |
| `-disable` | Uninject/disable. |
| `-minimize` | Start minimized to tray. |
| `-exit` | Exit after command-line action. |

Autostart is implemented with a scheduled task named `DwmLutGUI_Autostart`, running the GUI with `-apply -minimize` at logon with highest privileges.

#### 2. Staging and injection layer

The injector copies the native DLL to:

`%SystemRoot%\Temp\dwm_lut.dll`

It stages LUTs in:

`%SystemRoot%\Temp\luts\`

Each active monitor gets a staged SDR `.cube` and optionally an HDR `.cube`. After injection, the GUI deletes the staged LUT folder because the DLL loads LUTs during attach. The DLL itself remains in `%SystemRoot%\Temp` while injected.

The injection method is classic remote `LoadLibraryA`:

1. Elevate or impersonate enough privilege to access `dwm.exe`.
2. Copy `dwm_lut.dll` into the Windows temp directory.
3. Clear DACLs on the temp DLL and LUT folder so the target process can read them.
4. Open each `dwm.exe` process.
5. Allocate remote memory for the DLL path.
6. Write the path into the DWM process.
7. Start a remote thread at `kernel32!LoadLibraryA`.
8. Treat a zero remote-thread return as failure, usually caused by malformed LUTs or unsupported DWM internals.

Uninject calls remote `FreeLibrary` on the loaded `dwm_lut.dll` module and deletes the temp DLL afterward.

#### 3. Native DLL bootstrap

On `DLL_PROCESS_ATTACH`, the native DLL:

1. Loads LUT files from `%SystemRoot%\Temp\luts`.
2. Loads or locates D3D/DXGI/DWM-related modules.
3. Detects Windows generation using build booleans such as `isWindows11_24h2` and `isWindows11_25h2`.
4. Scans `dwmcore.dll` for byte patterns.
5. Installs MinHook hooks when required functions are found.
6. Forces DWM overlay behavior by writing `CCommonRegistryData::m_dwOverlayTestMode` through a pointer recovered from code.

The current 25H2 path requires at least:

* `COverlayContext::Present`
* `COverlayContext::IsCandidateDirectFlipCompatible`
* `COverlayContext::OverlaysEnabled`

Optional flip/promotion hooks are attempted when signatures exist, but on your current `dwmcore.dll` the optional 25H2 signatures did not match.

#### 4. Presentation hook

The core hook is `COverlayContext::Present`. That is the point where DWM is presenting compositor output through an overlay swap chain.

On 25H2 the fork first tries a direct texture retrieval path:

* Start from the `IOverlaySwapChain` object passed into `COverlayContext::Present`.
* Call through vtable slot `24`.
* On the returned object, call through vtable slot `19`.
* Query the result for `ID3D11Texture2D`.
* Apply the LUT directly to that texture.

If that direct path fails, the code falls back to scanning likely pointer-sized offsets inside the overlay-swapchain object, trying candidate objects as `IDXGISwapChain` until `ApplyLUT` succeeds, then caching the discovered offset.

This is powerful but fragile. Vtable slots and object-member offsets are private implementation details. The public Canary PDB indicates a better future direction: anchor on public names such as `CDDisplaySwapChain::GetBackBuffer`, `CDDisplaySwapChain::PresentDFlip`, and `COverlaySwapChain::HrFindInterface` instead of guessing from raw offsets alone.

#### 5. Render engine

Once the DLL has an `ID3D11Texture2D`, it:

1. Gets or initializes a D3D11 device from the backbuffer.
2. Builds render state, shaders, LUT textures, vertex/index data, samplers, and noise texture.
3. Determines SDR versus HDR from the backbuffer format.
4. Selects the LUT matching monitor coordinate and HDR state.
5. Renders over dirty rects/backbuffer regions with the LUT shader.

The shader uses tetrahedral interpolation for the 3D LUT. For SDR it applies ordered dithering after the transform. For HDR it converts through a PQ/BT.2100 style transform path and back toward scRGB before writing the result. That means HDR support is not "the same LUT but brighter." The code expects a separate HDR LUT file and a float HDR compositor surface.

Format handling observed in source:

| Backbuffer format family | Branch |
| --- | --- |
| `B8G8R8A8_UNORM`, `R8G8B8A8_UNORM`, SRGB variants, `R10G10B10A2_UNORM` | SDR LUT path |
| `R16G16B16A16_FLOAT` | HDR LUT path |
| Other formats | No LUT applied |

Practical HDR conclusion: the code has a real HDR branch, but this investigation did not validate colorimetric accuracy with a calibrator-generated HDR LUT. A diagnostic build should log the actual DXGI format and selected `_hdr.cube` hash while Windows HDR is enabled.

#### 6. LUT selection and the single-monitor simplification

The fixed fork tries to match the current DWM overlay context to a monitor by reading an internal rectangle. On 25H2 it dereferences the overlay context and reads a rectangle at a hardcoded internal offset. If the values look invalid, it falls back to `(0,0)`.

For this user's setup, that is overcomplicated. You have one monitor. The best reliability fix is:

* If exactly one active monitor/LUT exists, return that LUT without relying on internal DWM coordinates.
* Still log the internal rectangle for research.
* Keep the existing coordinate matching only for multi-monitor profiles.

This is one of the highest-value changes because it removes a whole class of DWM-private offset failures for the only setup you care about.

#### 7. DirectFlip, MPO, and why "applied" can still look bypassed

DWM can present some content through DirectFlip, Independent Flip, or multiplane overlays. Those paths can bypass the normal composed backbuffer where a LUT shader would see pixels.

The DLL fights this in three ways:

1. Hook `COverlayContext::IsCandidateDirectFlipCompatible` and return `false` when a LUT is active.
2. Hook `COverlayContext::OverlaysEnabled` and return `false` when a LUT is active.
3. Write `CCommonRegistryData::m_dwOverlayTestMode = 5` in DWM memory.

That is why a "working" build still has edge cases:

* Protected video/swapchains can be skipped.
* Full-screen or overlay-heavy applications may still find unhooked paths.
* The optional hooks that would suppress more flip/promotion paths do not match on your current DWM.
* On detach, the DLL resets the overlay-test global to `0`, which may overwrite a pre-existing nonzero setting.

### Canary-specific conclusions

Canary 29617 is not compatible with the current 25H2 fixed fork.

Evidence:

* All 25H2 fixed-fork patterns tested against Canary `dwmcore.dll` returned zero hits.
* The public symbol for `COverlayContext::Present` moved from current RVA `0x231800` to Canary RVA `0x5e534`.
* `COverlayContext::OverlaysEnabled`, the function the current fork uses both as a hook and as a route to recover `m_dwOverlayTestMode`, is missing from Canary public symbols.
* `CCommonRegistryData::m_dwOverlayTestMode` still exists in Canary publics at PE RVA `0x3b8688`, so overlay-test control should be recovered directly from that global or from references to it, not indirectly through `OverlaysEnabled`.
* The important concepts still exist: presentation, DirectFlip compatibility, MPO presentation, HDR metadata, backbuffer access, protected-content checks. The current resolver is what fails.

So the correct statement is:

> Canary support is plausibly buildable, but it is a new DWM profile/resolver port, not a one-line transfer of the 25H2 arrays.

### Proposed next-generation forward-port architecture

The current fork should stop using broad build booleans as the main compatibility boundary. `isWindows11_25h2 = build >= 26200` is too loose. It accidentally groups stable 25H2, Beta 25H2, 26H2 experimental, 26H1 tracks, and Canary-like futures into one conceptual bucket.

Replace it with a `DwmBuildProfile` resolver:

| Field | Purpose |
| --- | --- |
| `displayVersion` | Human Windows version such as `25H2`. |
| `buildLab` / build number | Coarse branch classification. |
| `dwmcoreFileVersion` | Fast local compatibility clue. |
| `dwmcoreSha256` | Exact binary identity. |
| `pdbGuidAge` | Best symbol identity key. |
| Required hook RVAs/signatures | `Present`, DirectFlip compatibility, overlay-control path. |
| Optional hook RVAs/signatures | Window/swapchain/visual flip and promotion suppressors. |
| Backbuffer strategy | `IOverlaySwapChain` vtable path, `CDDisplaySwapChain::GetBackBuffer`, or fallback scan. |
| Monitor strategy | single-monitor fast path, coordinate offset, or symbol-derived field. |

The profile resolver should operate in this order:

1. Exact known SHA-256 profile.
2. Exact PDB GUID/age profile.
3. Public symbol-server resolver if PDB is available.
4. AOB fallback with uniqueness checks.
5. Fail closed with a clear diagnostic log.

No hook should be installed from an ambiguous AOB. If a signature has two hits, the resolver should reject it unless it can prove the intended function by symbol, xref, surrounding control flow, or a known RVA.

### AI-assisted rebuild workflow

AI is useful here if it is used as a repeatable reverse-engineering assistant, not as a magic patch generator.

Recommended pipeline:

1. `collect`: Copy `C:\Windows\System32\dwmcore.dll`, compute SHA-256, extract CodeView GUID/age, record Windows build and file version.
2. `symbolize`: Download the matching public PDB from `msdl.microsoft.com` when available.
3. `locate`: Resolve public RVAs for targets such as `COverlayContext::Present`, `COverlayContext::IsCandidateDirectFlipCompatible`, `CCommonRegistryData::m_dwOverlayTestMode`, `CDDisplaySwapChain::GetBackBuffer`, and `CDDisplaySwapChain::PresentDFlip`.
4. `signature`: Generate candidate byte patterns from the target functions while masking relocations, calls, absolute addresses, and build-volatile immediates.
5. `verify`: Run the candidate patterns across known 23H2, 24H2, 25H2, 26H2, and Canary samples. Reject patterns that are missing or non-unique.
6. `profile`: Emit a C++/JSON profile with exact RVAs where symbols are available and fallback AOBs where needed.
7. `diagnose`: Build a DLL that logs every selected address, hook creation result, backbuffer path, DXGI format, HDR/SDR choice, LUT file hash, and DirectFlip/MPO suppression result.
8. `release`: Only build the quiet release DLL after the diagnostic build proves the profile on that exact machine.

This approach lets AI help with repetitive disassembly comparison and signature generation while keeping the safety property humans need: every generated hook target must be proven unique against real binaries.

### Concrete coding changes for this user's single-monitor build

For the current PC and near-term 25H2/26H2 work, I would implement these in order:

1. Add a diagnostic log mode that is always on for debug builds. Log file: `%SystemRoot%\Temp\dwm_lut.log`.
2. Replace `build >= 26200` style behavior with exact DWM profile detection.
3. Hard-code the current PC's exact `dwmcore.dll` identity as a known profile:
   * Build: `26200.8655`
   * File version: `10.0.26100.8457`
   * SHA-256: `03C820B55D3CF470C9E80C9905186F43937A7E79CC1D81BCE524DA823B6FCA8D`
   * PDB GUID/age: `{35D945A7-1171-77C7-B6CD-6D7D9ED0A2E8}`, age `1`
   * Real `COverlayContext::IsCandidateDirectFlipCompatible`: RVA `0xb1414`
4. Fix ambiguous matching: do not accept the false `0x14ed8` match for DirectFlip compatibility on this DWM.
5. Add the single-monitor LUT selection fast path.
6. Stop deriving `m_dwOverlayTestMode` only through the `OverlaysEnabled` byte pattern. Prefer a profile/symbol-derived global address, with an AOB fallback based on references to `CCommonRegistryData::m_dwOverlayTestMode`.
7. Remove duplicated hook-install attempts for optional hooks.
8. Make optional hook failures visible in the GUI or log, not silent.
9. For Canary/26H2 research builds, use public-symbol RVAs to generate a new profile rather than broad 25H2 byte arrays.

### Risk model

| Risk | Impact | Mitigation |
| --- | --- | --- |
| Wrong hook address | DWM crash, black screen, failed injection | Exact PDB/SHA profile and uniqueness checks before hooking. |
| Ambiguous AOB match | Hook installed into wrong function | Reject ambiguous matches unless disambiguated by symbol or known RVA. |
| DWM update | LUT stops applying | Pre-injection scanner and build profile check. |
| DirectFlip/MPO bypass | Some content remains uncorrected | Better flip/promotion hooks; log when optional hooks are missing. |
| HDR mis-selection | Washed out, clipped, or oversaturated HDR | Log DXGI format, Windows HDR state, and selected `_hdr.cube`. |
| Wrong LUT type | Oversaturated/blown colors | Use identity LUT for smoke test, then calibrator LUT; never test with creative camera LUTs. |
| Multi-monitor coordinate drift | Wrong LUT on wrong monitor | Single-monitor fast path for this user; symbol-derived monitor mapping later. |
| Security tooling | Injection flagged or blocked | Keep portable/debug build transparent; do not hide injection behavior. |
| OverlayTestMode reset | User's previous overlay setting overwritten | Save previous value if readable and restore that, not blindly `0`. |

### Final engineering call

For your actual single-monitor 25H2 machine, the near-term fix is not a rollback. The right path is a cleaned-up 25H2 diagnostic build with exact profile matching, corrected DirectFlip target selection, and a single-monitor LUT fast path.

For the next branch, target `26300.x` 26H2 Experimental first, not Canary. It is the best probable continuation of the 24H2/25H2 compositor line. Canary `29617.1000` is valuable because we now have its DWM files and symbols, and it proves the current signature approach will fail, but it should be treated as a future-proofing lab sample rather than the build to run on a calibrated workstation.

## 2026-06-29 Canary Rebuild Pathway

### Web-confirmed target and toolchain facts

Microsoft's Windows Insider release notes now expose the Future Platforms / Canary target as [Windows 11 Insider Preview Build 29617.1000](https://learn.microsoft.com/en-us/windows-insider/release-notes/experimental-future-platforms/preview-build-29617-1000). That is the correct public Canary sample for this investigation.

The symbol path is legitimate, not a guess. Microsoft documents the [Microsoft public symbol server](https://learn.microsoft.com/en-us/windows-hardware/drivers/debugger/microsoft-public-symbols), and the normal Windows tooling path is either DbgHelp or DIA:

* DbgHelp: [SymInitialize](https://learn.microsoft.com/en-us/windows/win32/api/dbghelp/nf-dbghelp-syminitialize), `SymLoadModuleEx`, `SymFromName`, `SymFromAddr`.
* DIA SDK: [Debug Interface Access SDK](https://learn.microsoft.com/en-us/visualstudio/debugger/debug-interface-access/debug-interface-access-sdk), including symbol lookup by RVA/name.
* Local LLVM tools used here: `llvm-pdbutil`, `llvm-readobj`, and `llvm-objdump`.

The local build machine has Visual Studio Build Tools 2019 and 2022 installed, so rebuilding is not blocked by missing compilers. The blocker is a correct Canary profile and live validation on a Canary install.

### Canary profile we can build from now

For exact Canary `29617.1000`, extracted `dwmcore.dll`:

| Property | Value |
| --- | --- |
| File | `amd64_microsoft-windows-d..wmanager-compositor_31bf3856ad364e35_10.0.29617.1000_none_34a6841f8b294dea/dwmcore.dll` |
| Image base in file | `0x180000000` |
| Size | `4,079,616` bytes |
| PDB | `dwmcore.pdb` |
| PDB GUID/age | `{9D2F7102-1CDA-8409-2018-100E70836B6E}`, age `1` |
| Symbol-server URL | `https://msdl.microsoft.com/download/symbols/dwmcore.pdb/9D2F71021CDA84092018100E70836B6E1/dwmcore.pdb` |

Candidate exact Canary profile:

| Target | Canary RVA | Status |
| --- | ---: | --- |
| `COverlayContext::Present` | `0x5e534` | Required hook. Public PDB and disassembly confirmed. |
| `COverlayContext::IsCandidateDirectFlipCompatible` | `0x17fabc` | Required hook. Public PDB and disassembly confirmed. |
| `COverlayContext::PresentMPO` | `0xe9878` | Useful for mapping/diagnostics. |
| `COverlayContext::UpdateHDRMetaData` | `0x1bafc4` | Useful HDR diagnostic anchor. |
| `CDDisplaySwapChain::GetBackBuffer` | `0x1acfb0` | Potential replacement for brittle vtable guessing. |
| `CDDisplaySwapChain::PresentDFlip` | `0x1dcb10` | Potential DirectFlip validation/hook target. |
| `CD3DDevice::PresentMPO` | `0x1ace74` | Potential MPO validation/hook target. |
| `COverlaySwapChain::IsHardwareProtected` | `0x1abbb0` | Protected-content guard. |
| `CCommonRegistryData::m_dwOverlayTestMode` | `0x3b8688` | Corrected PE RVA: `.data` section RVA `0x3b6000` + PDB section offset `0x2688`. |
| `COverlayContext::OverlaysEnabled` | missing | Do not require this for Canary. Use `m_dwOverlayTestMode` plus DirectFlip hook instead. |

Canary exact-prefix scan:

| Target | Unique exact prefix length |
| --- | ---: |
| `COverlayContext::Present` | 24 bytes |
| `COverlayContext::IsCandidateDirectFlipCompatible` | 16 bytes |
| `CDDisplaySwapChain::GetBackBuffer` | 16 bytes |

This gives us two viable locator modes for the first Canary diagnostic build:

1. Exact PDB/RVA profile: safest for known `29617.1000`.
2. Exact AOB fallback: acceptable only for this same sample, with uniqueness checks.

### Minimal Canary diagnostic build

The first build should not try to solve all future builds. It should answer: "Can we inject into Canary `29617.1000`, hook the right functions, get a D3D11 texture, and apply an identity/test LUT without crashing DWM?"

Patch shape:

```cpp
struct DwmProfile {
    const char* name;
    const char* pdbGuidAge;
    uint32_t presentRva;
    uint32_t directFlipCompatRva;
    uint32_t overlaysEnabledRva;
    uint32_t overlayTestModeRva;
    uint32_t displaySwapChainGetBackBufferRva;
    bool requireOverlaysEnabledHook;
    bool singleMonitorFastPath;
};
```

Add:

```cpp
static const DwmProfile kCanary29617 = {
    "canary_29617_1000",
    "9D2F7102-1CDA-8409-2018-100E70836B6E:1",
    0x5e534,
    0x17fabc,
    0,
    0x3b8688,
    0x1acfb0,
    false,
    true
};
```

Then change initialization:

1. Identify `dwmcore.dll` by CodeView PDB GUID/age and optionally SHA-256.
2. If the profile is exact Canary `29617.1000`, set hook addresses as `moduleBase + rva`; do not scan for the old 25H2 byte arrays.
3. Allow initialization when `Present` and `IsCandidateDirectFlipCompatible` are found, even when `OverlaysEnabled` is absent.
4. Set `g_pOverlayTestMode = (int*)((BYTE*)dwmcore + profile.overlayTestModeRva)`.
5. Save the old overlay-test value and restore that value on detach instead of blindly writing `0`.
6. Install only the hooks that exist:
   * `COverlayContext::Present`
   * `COverlayContext::IsCandidateDirectFlipCompatible`
   * optional future hooks only if profile fields are nonzero
7. Add the single-monitor fast path in `GetLUTDataFromCOverlayContext`.
8. Log every profile decision to `%SystemRoot%\Temp\dwm_lut.log`.

This is the fastest honest Canary build. It avoids pretending the missing `OverlaysEnabled` symbol still exists.

### The likely remaining Canary blocker: backbuffer retrieval

Hook addresses are solvable. The real unknown is whether `GetBackBuffer_25H2()` still retrieves the compositor texture on Canary.

The existing 25H2 path calls through private vtable indexes:

* `overlaySwapChain->vt[24]`
* then returned object `vt[19]`
* then `QueryInterface(ID3D11Texture2D)`

Canary `COverlayContext::Present` disassembly shows different-looking internal call flow. It accesses fields around `0x4bd0`, `0x4c14`, and calls `COverlayContext::PresentMPO`; it also calls an overlay-swapchain vtable entry at offset `0x180` in one branch. That does not prove the 25H2 `vt[24]/vt[19]` chain is still valid.

Therefore the diagnostic build should log:

| Signal | Why |
| --- | --- |
| `Present` hook entered | Proves MinHook target/prototype is viable. |
| `overlaySwapChain` pointer and first 64 vtable entries | Lets us map live vtable entries to PDB public functions. |
| `GetBackBuffer_25H2` success/failure | Tells us whether old retrieval survived. |
| `IDXGISwapChain` fallback offset success/failure | Tells us whether the old dynamic pointer scan still works. |
| Backbuffer `DXGI_FORMAT` | Proves SDR/HDR branch selection. |
| Selected LUT path/hash | Prevents repeating the "what LUT did it apply?" confusion. |

If old backbuffer retrieval fails, the next Canary-specific fix is to use symbols and live vtable mapping:

1. In the `Present` hook, inspect the `overlaySwapChain` object's vtable.
2. For each vtable entry that points inside `dwmcore.dll`, convert pointer to RVA.
3. Compare those RVAs to public PDB symbols such as `COverlaySwapChain::*`, `CDDisplaySwapChain::*`, and `CSwapChainRealization::*`.
4. Identify whether the runtime object exposes a route to `CDDisplaySwapChain::GetBackBuffer`, `CSwapChainRealization::GetDeviceTexture`, `CSwapChainRealization::GetDXGIResource`, or a QI-compatible resource.
5. Replace blind vtable indexes with a profile-selected backbuffer strategy.

Useful Canary public symbols for this phase include:

| Symbol | Purpose |
| --- | --- |
| `CDDisplaySwapChain::GetBackBuffer` | Direct route to an internal device target. |
| `CDDisplaySwapChain::GetPhysicalBackBuffer` | Alternate physical buffer route. |
| `CSwapChainRealization::GetDeviceTexture` | Possible route to D3D texture wrapper. |
| `CSwapChainRealization::GetDXGIResource` | Possible route to DXGI resource. |
| `COverlaySwapChain::HrFindInterface` | Interface-discovery route if callable safely. |

### Durable solution: profile generator, not manual edits forever

The maintainable architecture is a small offline generator:

`DwmProfileGen.exe C:\Windows\System32\dwmcore.dll --symbols srv*C:\symbols*https://msdl.microsoft.com/download/symbols --out dwm_profiles.generated.h`

What it should do:

1. Read PE CodeView debug directory.
2. Resolve PDB GUID/age.
3. Download/load matching PDB through DbgHelp or DIA.
4. Look up public names:
   * `?Present@COverlayContext@@...`
   * `?IsCandidateDirectFlipCompatible@COverlayContext@@...`
   * `?m_dwOverlayTestMode@CCommonRegistryData@@0KA`
   * `?GetBackBuffer@CDDisplaySwapChain@@...`
   * `?PresentDFlip@CDDisplaySwapChain@@...`
5. Convert PDB section offsets to PE RVAs correctly.
6. Generate exact profile data and optional unique AOBs.
7. Refuse to generate a release profile when any required symbol is missing or ambiguous.

This should run outside `dwm.exe`. The injected DLL should not be doing network symbol downloads or COM-heavy DIA work inside DWM. The DLL should consume a compiled-in profile table or a local signed/hashed profile file.

### Live validation matrix

The Canary fix is not real until it passes this matrix on a Canary VM or sacrificial PC:

| Test | Expected result |
| --- | --- |
| Identity SDR LUT | No visual change, no DWM crash, log says SDR texture hit. |
| Obvious SDR test LUT | Clear visible color transform on desktop and normal apps. |
| Windows HDR on, identity HDR LUT | No clipping/oversaturation, log says `R16G16B16A16_FLOAT` and `_hdr.cube`. |
| Windows HDR on, obvious HDR LUT | Visible transform without SDR LUT being selected. |
| Browser/video playback | Log protected/overlay paths; no crash. Some protected content may stay uncorrected. |
| Full-screen borderless app | Confirm DirectFlip hook suppresses bypass or logs bypass. |
| Disable/uninject | DWM survives, overlay-test mode restores previous value. |

### My recommended path

1. Build an exact `canary_29617_1000` diagnostic profile using the RVAs above.
2. Skip `OverlaysEnabled` as a required hook for Canary and rely on `m_dwOverlayTestMode = 5` plus the DirectFlip compatibility hook.
3. Add single-monitor fast path immediately.
4. Run only on a Canary VM/secondary install first.
5. If texture retrieval fails, instrument live vtables and build a Canary-specific backbuffer strategy from PDB-mapped vtable entries.
6. Once Canary is proven, generalize into `DwmProfileGen` so future Canary builds are profile generation plus validation, not hand archaeology every time.
