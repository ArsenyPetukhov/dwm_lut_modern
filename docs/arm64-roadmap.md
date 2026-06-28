# ARM64 Roadmap

ARM64 support is not the same as adding more x64 DWM profiles.

## Current State

- UUP exposes ARM64 entries for the current public build set in `docs/build-catalog.md`.
- The repo has ARM64 solution, native project, GUI project, and script plumbing.
- This machine has x64 MSVC tools installed, but no `Hostx64\arm64` compiler directory was found under the installed Build Tools.
- A quiet installer attempt from this non-elevated Codex process failed with Visual Studio Installer exit code `5007`; the installer log says quiet/passive commands must start elevated.
- A later elevated installer attempt reached the setup engine but failed with exit code `8006` / `VSProcessesRunning`. An idle reusable `MSBuild.exe` node from the previous local build was found and stopped afterwards.
- The repo does not yet contain ARM64 `dwmcore.dll` PDB/RVA profiles.
- The repo intentionally refuses architecture-mismatched injection and skips exact profiles whose architecture does not match the loaded DWM binary.

## Required Work

1. Install Visual Studio Build Tools ARM64 C++ components from an elevated PowerShell:

```powershell
& "C:\Program Files (x86)\Microsoft Visual Studio\Installer\setup.exe" modify --installPath "C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools" --add Microsoft.VisualStudio.Component.VC.Tools.ARM64 --quiet --norestart
```

Then continue:

```text
2. Confirm `Hostx64\arm64\cl.exe` exists.
3. Build vcpkg dependencies for `arm64-windows-static`.
4. Verify MinHook supports the ARM64 hook method needed here, or replace it with a proven ARM64 detour implementation.
5. Extract ARM64 `dwmcore.dll` for each target build.
6. Download matching ARM64 public PDBs.
7. Generate ARM64 RVAs and validate symbol availability.
8. Add ARM64 profile rows to `src\lutdwm\DwmProfiles.generated.h`.
9. Create an ARM64 package that contains ARM64 `dwm_lut.dll`.
10. Test on Windows on Arm hardware or a VM with real ARM64 DWM.
```

## Why Not Ship ARM64 Now?

Shipping an x64 DLL and labeling it ARM64 would not work. Shipping an ARM64 DLL without ARM64 DWM profiles would be equally misleading. The correct next step is toolchain installation plus profile extraction.

## First ARM64 Target

Use `29617.1000` ARM64 first because it is the Canary/Future Platforms target and UUP exposes a direct ARM64 row:

```text
Build: 29617.1000
Arch: arm64
UUP UUID: ca767acf-e249-40c2-a5e8-974ff7f77ffd
```
