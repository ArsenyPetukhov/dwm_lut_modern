# Changelog

## [4.0.0-modern-windows] - 2026-06-29

### Added

- Maintained repo structure for a real OSS fork.
- Runtime DWM profile selection by loaded `dwmcore.dll` PDB GUID/age.
- PDB-derived profiles for current, 25H2, 26H1, 26H2 Experimental, and Canary/Future Platforms builds.
- Timestamped DWM log lines with PID and thread ID.
- Staged monitor manifest logging for multi-monitor diagnostics.
- Single-monitor fast path that avoids fragile DWM private coordinate lookup.
- Tight nearest-position fallback for small coordinate drift on multi-monitor setups.
- Release build, package, checksum, system-info, and verification scripts.
- Programmatic build/package smoke tests.
- Platform-aware x64/ARM64 build scripts and solution configurations.
- GUI-side PE machine validation before injecting into `dwm.exe`.
- Native DLL architecture checks before selecting a DWM profile.
- Robust GUI privilege setup that enables `SeDebugPrivilege`, attempts SYSTEM impersonation through duplicate tokens, and falls back to elevated administrator instead of failing with `Not running as SYSTEM`.

### Changed

- Release logging now writes to `%SystemRoot%\Temp\dwm_lut.log`.
- `COverlayContext::OverlaysEnabled` and DirectFlip hooks are optional by profile.
- `m_dwOverlayTestMode` is restored to its previous value on detach.
- Release packaging now names matrix zips with `win-x64` / `win-arm64` architecture labels.
- Release packaging writes a `universal-win-x64-next` side-by-side alias if the stable `universal-win-x64` folder is locked by a running GUI.

### Known Issues

- Multi-monitor identity is improved but not final. True robust support still needs live mapping from DWM display identity to GUI display path.
- HDR correctness depends on LUT domain and Windows HDR state.
- Canary/Future Platforms support is experimental.
