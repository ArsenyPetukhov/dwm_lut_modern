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

### Changed

- Release logging now writes to `%SystemRoot%\Temp\dwm_lut.log`.
- `COverlayContext::OverlaysEnabled` and DirectFlip hooks are optional by profile.
- `m_dwOverlayTestMode` is restored to its previous value on detach.

### Known Issues

- Multi-monitor identity is improved but not final. True robust support still needs live mapping from DWM display identity to GUI display path.
- HDR correctness depends on LUT domain and Windows HDR state.
- Canary/Future Platforms support is experimental.
