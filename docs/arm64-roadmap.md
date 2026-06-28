# ARM64 Roadmap

ARM64 support is not the same as adding more x64 DWM profiles.

## Current State

- UUP exposes ARM64 entries for the current public build set in `docs/build-catalog.md`.
- This machine has x64 MSVC tools installed, but no ARM64 compiler directories were found under the installed Build Tools.
- The repo does not yet build an ARM64 `dwm_lut.dll`.
- The repo does not yet contain ARM64 `dwmcore.dll` PDB/RVA profiles.

## Required Work

1. Install Visual Studio Build Tools ARM64 C++ components.
2. Confirm `Hostx64\arm64\cl.exe` exists.
3. Add `Debug|ARM64` and `Release|ARM64` configurations to `src\lutdwm\lutdwm.vcxproj`.
4. Add ARM64 mappings to `DwmLutModern.sln`.
5. Verify MinHook supports the ARM64 hook method needed here, or replace it with a proven ARM64 detour implementation.
6. Build vcpkg dependencies for `arm64-windows-static`.
7. Extract ARM64 `dwmcore.dll` for each target build.
8. Download matching ARM64 public PDBs.
9. Generate ARM64 RVAs and validate symbol availability.
10. Create an ARM64 package that contains ARM64 `dwm_lut.dll`.
11. Test on Windows on Arm hardware or a VM with real ARM64 DWM.

## Why Not Ship ARM64 Now?

Shipping an x64 DLL and labeling it ARM64 would not work. Shipping an ARM64 DLL without ARM64 DWM profiles would be equally misleading. The correct next step is toolchain installation plus profile extraction.

## First ARM64 Target

Use `29617.1000` ARM64 first because it is the Canary/Future Platforms target and UUP exposes a direct ARM64 row:

```text
Build: 29617.1000
Arch: arm64
UUP UUID: ca767acf-e249-40c2-a5e8-974ff7f77ffd
```
