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

The GUI now exposes the resolver decision instead of hiding it:

- `Auto`: exact PDB profile first, then the OS-family signature fallback.
- `Exact profile only`: fail closed unless `dwmcore.dll` exactly matches a compiled profile.
- `Win10 signatures`: force the legacy Windows 10 scanner.
- `Win11 signatures`: force the pre-24H2 Windows 11 scanner.
- `24H2 signatures`: force the 26100-family scanner.
- `25H2+ signatures`: force the modern 26200+ scanner.

The support label is computed from the local `System32\dwmcore.dll` PDB GUID/age and shows exact, fallback, experimental, or unknown status before the user clicks Apply.

## Offline Validation Boundary

`scripts\test-dwm-payload-profile.ps1` validates a copied `dwmcore.dll` without booting that Windows build. It parses PE headers, extracts the CodeView RSDS identity, matches `compiled_dwm_profiles.json`, verifies SHA-256, and checks every nonzero RVA against the image sections.

This is enough to validate profile identity and address sanity for payloads such as Canary `29617.1000`. It is not enough to prove the live compositor object graph still exposes the backbuffer through the same path. That last question requires a real DWM process on the target build, usually a VM or sacrificial install, with identity and obvious LUT tests plus `%SystemRoot%\Temp\dwm_lut.log`.

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
