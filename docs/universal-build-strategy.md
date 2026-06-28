# Universal Build Strategy

## Short Answer

One truly universal DWM LUT binary is not realistic today.

The practical strategy is:

- One **x64 multi-profile package** for known amd64 builds.
- One future **ARM64 multi-profile package** for known ARM64 builds.
- A repeatable profile ingestion pipeline for every new Windows build.

## Why A Single Binary Cannot Cover Everything

### CPU Architecture

`dwm.exe` is a native process. Windows cannot load an x64 DLL into an ARM64 `dwm.exe`. WOW64 emulation does not solve in-process injection into a native ARM64 compositor.

### Private DWM Internals

DWM does not expose a supported public LUT hook. This project hooks private C++ functions in `dwmcore.dll`. Microsoft can move, rename, inline, or remove these functions at any update.

### Build Number Is Not Identity

The app must not use `BuildLabEx` or the visible Windows build as its primary key. Multiple Windows build labels can share one DWM payload, and one cumulative update can replace the DWM payload under a familiar OS label. The correct key is:

```text
dwmcore.dll CodeView PDB GUID + PDB age
```

## Current Implementation

The current x64 DLL contains a compiled table of known DWM profiles. At startup it:

1. Reads the loaded `dwmcore.dll` CodeView PDB GUID/age.
2. Searches `DwmProfiles.generated.h`.
3. Uses exact RVAs for the matching profile.
4. Logs the selected profile and hook addresses.
5. Falls back to legacy byte signatures only when no exact profile matches.

This keeps known builds deterministic and makes unknown builds visibly diagnosable.

## Runtime PDB Resolver Option

A more universal future design is possible:

1. Read `dwmcore.dll` PDB GUID/age.
2. Download or load the matching Microsoft public PDB.
3. Resolve required public symbols at runtime.
4. Convert PDB addresses to PE RVAs.
5. Validate candidate functions and vtable relationships.
6. Hook only if every required invariant passes.

This is feasible but high-risk inside `dwm.exe` because symbol loading, network access, PDB parsing, and failure behavior all happen in a privileged compositor process. The safer design is an offline profile generator plus a rebuilt signed package.

## Decision

Keep the production path profile-based and deterministic. Build automation should make profile generation fast enough that new Insider builds can be supported without turning `dwm.exe` into a symbol-processing host.
