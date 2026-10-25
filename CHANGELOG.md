# Changelog

## [Unreleased]

### Added

- A paste-ready community compatibility report generated beside validation evidence.
- A no-reinstall validation policy using representative Hyper-V/DWM implementation families and
  distinct evidence labels for built, VM, physical, measured, and community-reported results.

### Changed

- Replaced compatibility warning clutter with one compact neutral report-an-issue notice and a
  direct GitHub compatibility-report link on About.

### Fixed

- UI evidence capture now prefers `PrintWindow` and permits screen-copy fallback only after the
  exact application HWND is confirmed foreground, preventing unrelated windows from being saved.

## [5.0.3-fullscreen-preview] - 2026-10-26

- Releases screen-buffer references before fullscreen transitions and rebuilds copy buffers when their format changes.
- Preserves Windows' graphics state around LUT rendering and fixes SDR/HDR settings carrying across frames.
- Rebuilds GPU LUT resources after DWM device replacement on the exact `26100.4946` profile.
- Removes an unnecessary overlay hook on that profile while preserving the native fullscreen calling behavior.

## [5.0.2-fullscreen-preview] - 2026-10-26

- Adds fullscreen windowed LUT correction for the exact Windows 11 24H2 `26100.4946` payload through its Independent Flip control.
- Keeps SDR/HDR assignments separate and restores the previous composition settings on Disable.
- Records the earlier AMD windowed repair as confirmed working by the user.

## [5.0.1-amd24h2-preview] - 2026-10-25

- Adds the AMD laptop hotfix for Windows 11 24H2 build `26100.4946` ([#2](https://github.com/ArsenyPetukhov/dwm_lut_modern/issues/2)).
- Corrects backbuffer access and matches LUTs to the graphics adapter and display output.
- Includes an x64 portable preview with the patched DLL and matching debug symbols.

## [5.0.0] - 2026-07-10

### Added

- New self-contained .NET 8 WPF/Fluent application with per-display SDR/HDR assignments, LUT
  library, onboarding, tray controls, global hotkeys, theme/language settings, diagnostics, and
  developer/project links.
- Persisted desired on/off state under `%AppData%\DwmLutModern`.
- Current-user-only named-pipe command channel so one process owns state and DWM operations.
- Strict, validated CLI with structured status and meaningful exit codes.
- Adapter-aware display identity and exact adapter/target HDR-state queries.
- Automated tests for controller recovery, disabled intent, autostart transactions, state
  migration, CLI validation, IPC, display identity, hotkey failures, and DWM validation labels.
- Self-contained x64 portable ZIP and WiX MSI installer with uninstall autostart cleanup.
- Liquid-glass-inspired Mica design system with layered translucent surfaces, redesigned navigation,
  status hero, display cards, library, settings, onboarding, hotkey, and provenance screens.
- Repeatable Windows Sandbox package smoke test, deterministic diagnostic LUT generator, and a
  structured environment/DWM/GPU/monitor/package evidence collector.
- Tiered VM, physical HDR/multi-monitor, and instrumented calibration validation lab.
- Truthful v1-v5 source milestone/provenance policy, architecture review, and v6 roadmap.

### Fixed

- A disabled LUT no longer re-applies after logon, wake, unlock, or display changes.
- Cold-start watchdog recovery now restores an enabled LUT with bounded retries.
- Disable only reports success after `dwm_lut.dll` is verified absent from DWM.
- Failed scheduled-task and hotkey changes no longer remain displayed/persisted as successful.
- Start-minimized is effective only for a configured startup launch or explicit `--minimized`.
- CLI commands are forwarded to a running tray instance instead of silently exiting or racing the
  state file.
- SDR/HDR drag-and-drop targets are explicit; filenames no longer guess the signal domain.
- Closing or failing onboarding no longer marks setup complete; apply/autostart errors stay visible.
- Clearing the last assigned LUT disarms desired Enabled state instead of starting a recovery loop.
- A multi-display desktop with only one configured LUT no longer enters the single-display fast
  path and bleed that LUT onto unconfigured outputs.
- Unsupported/unknown DWM architectures now block Apply centrally instead of only showing a warning.
- CLI display numbers must resolve to exactly one output; ambiguous multi-adapter numbers fail and
  point to the stable device path. Onboarding now makes the target display explicit.
- Empty live topology no longer falls back to stale saved coordinates.
- Partial multi-DWM injection is rolled back and remains unhealthy/retryable until consistent.
- Semantically invalid JSON state is preserved for diagnostics, repaired to safe collections, and
  disarmed before startup can enumerate it.
- MSI major upgrades preserve the user's autostart task; explicit uninstall still removes it.
- A failed Windows advanced-color query is reported as unknown instead of being presented as SDR.
- The screenshot/smoke build gets an isolated singleton scope, so it can run beside the elevated
  production tray app without forwarding into a cross-integrity pipe and exiting.

### Safety

- Multi-monitor native routing requires an exact position/domain match. Nearest-position and
  first-available-LUT fallbacks were removed so ambiguous contexts receive no LUT.
- Native routing rejects duplicate candidates, and its single-display fast path requires a full
  topology manifest proving exactly one connected display.
- The test-only state-root override is absent from production builds.
- Compiled PDB profiles are labeled separately from live-validated support.
- Desktop refresh uses Win32 invalidation and `DwmFlush` instead of a temporary overlay window.
- The UI is code-native and DPI-aware; generated concept imagery is reference material rather than
  a raster dependency in the control surface.

### Validation status

- Automated and package validation is required in CI.
- Exact-build elevated SDR/HDR, sleep/reboot, DWM-restart, and multi-monitor evidence remains a
  release gate; compiled profiles are not automatically called stable.
- Windows Sandbox is explicitly limited to clean-machine package/UI proof. Physical display and
  meter-backed evidence are separate release tiers.

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
