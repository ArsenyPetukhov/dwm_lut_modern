# Legacy DWM LUT Fork Survey And Universal Support Plan

Date: 2026-06-29  
Reference checkout root: `C:\Users\arsen\Documents\Code\DWM LUT\references\legacy-github`

This document records the GitHub survey and the engineering plan for the actual target: one maintained app that a normal user can download, run on a Windows 10 or Windows 11 PC, and have it choose the right DWM path automatically.

## What Was Downloaded

The reference updater is:

```powershell
.\scripts\update-legacy-references.ps1 -IncludeForks
```

It mirrors the seed repos and public forks under:

```text
C:\Users\arsen\Documents\Code\DWM LUT\references\legacy-github
```

Generated manifests committed in this repo:

```text
artifacts\research\legacy-repos.generated.csv
artifacts\research\legacy-native-engines.generated.csv
```

Seed repositories:

- <https://github.com/ledoge/dwm_lut>
- <https://github.com/lauralex/dwm_lut>
- <https://github.com/ed1ii/dwm_lut_fixed>
- <https://github.com/0x401gg/dwm_lut-windows-25h2>
- <https://github.com/hadoooooouken/dwm_lut_fps_boost>
- <https://github.com/RobWilton3000/DwmLUTGUI-copy>

The updater also mirrors public forks of `ledoge/dwm_lut` through the GitHub API.

## High-Level Result

The old GitHub ecosystem does not contain dozens of independent compatibility breakthroughs. It mostly collapses into six native engine hashes:

| Hash prefix | Engine | Meaning |
| --- | --- | --- |
| `BA9E67DC9C55` | `ledoge/dwm_lut` master era | Original C engine; Windows 10 20H2/21H1 plus early Windows 11 support. Sixteen forks are identical at the native engine level. |
| `EC70960FE941` | `Windowscoder/dwm_lut` | Older/alternate fork, less useful than ledoge master for our target. |
| `66418E172FB7` | `lauralex/dwm_lut` | C++/vcpkg era with 22H2/23H2 lineage and 24H2 branch history. |
| `AB1F81559238` | `hadoooooouken/dwm_lut_fps_boost` | Performance-tuned fork, useful to inspect later for GPU overhead choices. |
| `C0586787608A` | `ed1ii/dwm_lut_fixed` | 2026 25H2/23H2 fixed fork; has multi-monitor experimentation and later "verified 23H2" retreat. |
| `B6D9543EB01F` | `0x401gg/dwm_lut-windows-25h2` | 25H2-focused fork with diagnostics and GUI work. |

The conclusion: the universal app should not be a pile of separate EXEs. It should be one resolver with multiple engine families.

## Compatibility History From The Diffs

### Windows 10 20H2/21H1: Original Signature Engine

`ledoge/dwm_lut` initially claimed Windows 10 20H2/21H1 support. The relevant native file is `dwm_lut.c`.

Core method:

- scan `dwmcore.dll` for byte signatures;
- hook `COverlayContext::Present`;
- hook `COverlayContext::IsCandidateDirectFlipCompatible`;
- hook `COverlayContext::OverlaysEnabled`;
- retrieve `IDXGISwapChain` from `overlaySwapChain + IOverlaySwapChain_IDXGISwapChain_offset`;
- map monitor by `COverlayContext_DeviceClipBox_offset`;
- disable DirectFlip/MPO while a LUT is active.

Important original constants:

```text
COverlayContext::Present signature: COverlayContext_Present_bytes
IOverlaySwapChain_IDXGISwapChain_offset: -0x118
COverlayContext_DeviceClipBox_offset: -0x120
IOverlaySwapChain_HardwareProtected_offset: -0xbc
```

This still exists in our current `src/lutdwm/dllmain.cpp` as the non-Windows-11 fallback path.

### Early Windows 11: Separate Signature And Object Layout

Commit: <https://github.com/ledoge/dwm_lut/commit/bfbe5dc033060910b6d017d695f7b36a3d6943ac>

That commit added Windows 11 support by introducing:

- `isWindows11 = NtBuildNumber >= 22000`;
- new W11 `COverlayContext::Present` signature;
- new W11 DirectFlip/Overlays signatures;
- new W11 clip-box offset;
- different swapchain pointer retrieval.

The important lesson is that Windows 11 was not just "same hook, new bytes." The object layout changed. A universal app needs an engine family or ABI mode, not only a table of RVAs.

### Windows 11 22H1/22H2 Era: UBR-Sensitive Offset Drift

Commit: <https://github.com/ledoge/dwm_lut/commit/246ae6b9f0c86755e38808f39070ceaf8161ea8a>

That commit added a Windows update build revision check:

```text
HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\UBR
if UBR >= 706, COverlayContext_DeviceClipBox_offset_w11 += 8
```

The important lesson is that marketing version is too coarse. Even inside "Windows 11", cumulative update revisions can move private fields. This is why our current exact-profile approach uses `dwmcore.dll` PDB GUID/age instead of "22H2" or "25H2" labels.

### lauralex C++ Era: vcpkg, Diagnostics, 22H2+ Continuity

Repository: <https://github.com/lauralex/dwm_lut>

Notable commit: <https://github.com/lauralex/dwm_lut/commit/c85db60c34bade79743b46da130d5250a78ca87a>

That code introduced more debugging and offset changes around the C++/vcpkg lineage. It is the ancestor of our current repo shape.

Useful lesson:

- keep the C++/vcpkg structure;
- do not return to a pure C monolith;
- preserve old signature engines as fallback strategies;
- move stable known payloads into exact PDB profiles.

### 24H2: New Present ABI And New Modern Signature Engine

Commit: <https://github.com/lauralex/dwm_lut/commit/f9e59eac6ca702e212c1f8016645782d0496d081>

24H2 added:

- `COverlayContext_Present_bytes_w11_24h2`;
- a different Present hook typedef:

```text
long long Present_24h2(void*, void*, unsigned int, rectVec*, int, void*, bool)
```

- `IOverlaySwapChain_IDXGISwapChain_offset_w11_24h2 = 0x108`;
- `COverlayContext_DeviceClipBox_offset_w11_24h2 = 0x53E8`;
- different DirectFlip compatibility function signature.

The important lesson is that some Windows changes are ABI changes, not just offset changes. A profile must specify the engine ABI family.

### 25H2 / 23H2 Fixed Forks

Repositories:

- <https://github.com/ed1ii/dwm_lut_fixed>
- <https://github.com/0x401gg/dwm_lut-windows-25h2>

Findings:

- there are real 25H2 fixes and diagnostics in the fork history;
- later commits in `ed1ii/dwm_lut_fixed` emphasize "Official 23H2 Support - VERIFIED STABLE";
- multi-monitor detection was experimented with through dynamic memory scanning and then simplified back toward known offsets;
- this supports our current approach: do not pretend "25H2 support" means all future DWM layouts are solved.

## What Our Current Repo Already Has

The current repo already includes:

- legacy Windows 10 byte-signature fallback;
- early Windows 11 byte-signature fallback;
- 24H2 byte-signature fallback;
- 25H2 byte-signature fallback;
- exact PDB GUID/age profile selection for known modern DWM payloads;
- detailed GUI injection logs;
- detailed DLL logs;
- single-monitor fast path;
- safer pointer reads around private DWM object probing.

So the next step is not to "port old 22H2 code." The old engine is already largely present. The next step is to formalize the resolver and profile schema so support becomes systematic.

## Desired Zero-Friction Runtime Behavior

The user experience should be:

1. User downloads one portable package.
2. User runs `DwmLutGUI.exe` as Administrator.
3. GUI detects OS build, architecture, `dwmcore.dll` PDB identity, and support mode.
4. GUI says one of:
   - supported by exact profile;
   - supported by legacy signature fallback;
   - experimental profile, proceed with warning;
   - unsupported DWM payload, export diagnostics.
5. User selects a LUT.
6. Apply either works or explains exactly why it cannot.

The user should never have to know "25H2", "RVA", "PDB GUID", "MPO", or "DirectFlip."

## Required Resolver Architecture

The DLL should eventually resolve support in this order:

1. **Exact PDB profile engine**
   - Match `dwmcore.dll` machine + PDB GUID + PDB age.
   - Use explicit RVAs and explicit ABI family.
   - This is the best path for 24H2/25H2/26H1/Canary and all future builds.

2. **Known SHA-256 alias engine**
   - If two Windows labels share the same DWM payload, alias them by `dwmcore.dll` SHA-256.
   - This prevents duplicate profiles and false "unsupported" states.

3. **Legacy signature engine**
   - Use old Win10 and early Win11 byte signatures only when exact profile is missing.
   - Verify that all required functions and object reads are plausible before hooking.
   - This is the path for Win10 20H2/21H1/possibly 22H2 and older Win11 builds.

4. **Modern signature fallback**
   - Keep 24H2/25H2 patterns as emergency fallback.
   - Prefer exact profiles over pattern scans when both exist.

5. **Fail closed**
   - If no path resolves cleanly, do not hook DWM.
   - Log a compact diagnostic bundle.

## Profile Schema Upgrade Needed

The current `DwmSymbolProfile` stores offsets. A universal resolver needs a richer schema:

```text
id
machine
pdbGuid
pdbAge
sha256
osBuildLabels[]
engineFamily
presentAbi
swapchainStrategy
backbufferStrategy
overlayStrategy
monitorIdentityStrategy
requiredHooks[]
optionalHooks[]
validationState
```

Why:

- 20H2/21H1 use one object layout.
- early Windows 11 uses another.
- 24H2 changes the Present ABI.
- 25H2/Canary may remove `OverlaysEnabled`.
- future builds may keep symbols but change the backbuffer path.

Offsets alone cannot encode all of that.

## Automatic Future Support: What Is Realistic

Fully automatic for already-profiled builds:

- yes, with exact PDB profile and engine family selection.

Fully automatic for a brand-new future DWM payload:

- not safely, unless the app can ingest a signed profile pack or the maintainer ships an updated release.

A safe future-proof path:

1. GUI detects unknown `dwmcore.dll`.
2. GUI exports:
   - OS build;
   - architecture;
   - `dwmcore.dll` SHA-256;
   - PDB GUID/age;
   - public symbol availability;
   - log files.
3. Maintainer automation downloads public symbols and generates a candidate profile.
4. Candidate profile is tested in VM/secondary install.
5. A signed app/profile update is published.

An unsafe path:

- app downloads arbitrary RVAs from the internet and hooks them inside `dwm.exe` without signature verification.

Do not do that. A dynamic profile pack must be signed or embedded in a normal signed release.

## Windows 10 Coverage Reality

The old README claim was Windows 10 20H2/21H1. Windows 10 22H2 is the final Windows 10 feature release line, but support needs actual `dwmcore.dll` payload validation, not assumption from the marketing version.

Current practical plan for Windows 10:

1. Treat old 20H2/21H1 signatures as a legacy fallback, not as proof of all Windows 10.
2. Add exact PDB profiles for Windows 10 19042, 19043, 19044, and 19045 if public symbols are available.
3. Validate at least 19045 because it is the final/common Windows 10 22H2 line.
4. Keep the GUI message plain: "Windows 10 DWM profile supported" or "Unsupported Windows 10 DWM payload."

Microsoft references:

- Windows 10 release information: <https://learn.microsoft.com/en-us/windows/release-health/release-information>
- Windows 10 update history: <https://support.microsoft.com/en-us/topic/windows-10-update-history-93345c32-4ae1-6d1c-f885-6c0b718adf3b>

## Concrete Next Engineering Steps

1. Add a GUI-visible support status panel:
   - architecture;
   - OS build;
   - DWM PDB identity;
   - resolver mode;
   - profile ID or fallback engine ID;
   - link/button to open logs.

2. Extend `DwmSymbolProfile` into a real engine profile:
   - do not hardcode "24H2/25H2" booleans as the primary selector;
   - let the matched profile choose ABI and strategy.

3. Add Windows 10 exact profiles:
   - extract `dwmcore.dll` for 19042/19043/19044/19045;
   - resolve public symbols;
   - add profile rows;
   - validate in VM.

4. Build a profile ingestion tool:
   - PE RSDS reader;
   - Microsoft symbol server downloader;
   - DIA/DbgHelp symbol resolver;
   - JSON profile writer;
   - profile diff report.

5. Add signed profile-pack design:
   - profiles can update faster than full app releases;
   - DLL should only accept profile packs signed by the maintainer key;
   - unknown unsigned profiles should be rejected.

6. Add "Export diagnostics" in GUI:
   - zip logs;
   - config;
   - profile table;
   - `dwmcore.dll` identity/hash;
   - Windows build.

7. Validate Canary:
   - profile matching is already solved;
   - unknown is live backbuffer retrieval.

## Bottom Line

The universal app is feasible as a maintained compatibility system, not as a magical one-hook binary.

The right model is:

```text
One GUI/package
  + one native DLL per CPU architecture
  + multiple engine families
  + exact PDB profiles for modern builds
  + legacy signature fallbacks for old builds
  + clear support status for the user
  + signed profile/update pipeline for new DWM payloads
```

That gets the user experience you want: download, run, pick LUT, Apply. The technical complexity stays inside the maintainer workflow and logs.
