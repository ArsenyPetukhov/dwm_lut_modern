# Validation

## Automated gate

Run from the repository root:

```powershell
.\scripts\test-build.ps1
.\scripts\package-modern-installer.ps1
git diff --check
```

The build gate compiles native x64 Release and both GUIs, builds the modern test project, runs the
controller/state/CLI/IPC/identity/hotkey/profile tests, rejects known unsafe native LUT fallbacks,
checks required artifacts, and prints the native SHA-256. Packaging publishes a self-contained ZIP,
MSI, and checksum files. A package is invalid if its managed/native architectures differ or the
native DLL is absent.

UI screenshots can be regenerated without touching normal user state:

```powershell
.\scripts\capture-modern-ui.ps1
```

The harness uses the test-only `DWM_LUT_STATE_ROOT` override and an `asInvoker` manifest. It proves
startup/rendering and catches obvious layout regressions; it does not prove elevated DWM injection.

## Elevated physical/VM release gate

Use `tests/manual-test-plan.md` and store a copy of
`docs/validation/release-evidence-template.md` for every promoted Windows/DWM profile. At minimum:

- exact Windows build and `dwmcore.dll` PDB identity;
- GPU/driver and display connector/topology;
- obvious SDR LUT apply/disable and measured calibration sanity;
- HDR on/off domain switching with distinct SDR and HDR LUTs;
- Disabled across app restart, logon, sleep/wake, unlock, and display reconnect;
- Enabled recovery after DWM restart, sleep/wake, unlock, and display reconnect;
- exact-coordinate behavior for negative-coordinate, mixed-HDR, and multi-adapter topologies;
- failed/ambiguous routing applies no LUT;
- clean MSI install, repair/upgrade where applicable, and uninstall task cleanup;
- sanitized `%SystemRoot%\Temp\dwm_lut.log` plus package hashes.

Do not label a compiled profile stable from automated tests alone. Runtime DWM internals, GPU state,
HDR transitions, elevation, and calibration accuracy require this live evidence.

## Regression triggers

Repeat live validation after a Windows cumulative/Insider update, DWM profile change, GPU driver
update, injection crash, monitor identity/routing change, or native shader/parser change.
