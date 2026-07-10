# Architecture

## Product components

- `src/DwmLutModern.App`: primary .NET 8 WPF control plane, UI, CLI, tray, persistence, and OS integration.
- `src/lutdwm`: native x64/ARM64 DLL loaded into `dwm.exe`; selects the exact DWM profile and applies the shader.
- `src/DwmLutGUI`: preserved legacy WPF GUI, built and packaged only as a rollback path.
- `artifacts/profiles`: generated/curated DWM CodeView PDB identities and RVAs.
- `installer/DwmLutModern.Installer`: WiX x64 MSI using the self-contained modern application payload.
- `tests/DwmLutModern.App.Tests`: deterministic tests around public control-plane seams.

## State and ownership

`%AppData%\DwmLutModern\state.json` records user intent, monitor identities and LUT assignments.
`AppState.IsEnabled` is desired state; `InjectionService.GetStatus()` is observed state. They are
deliberately independent so the UI can report and recover from disagreement.

One process per Windows user owns state and DWM operations. It owns a per-user mutex and a
`PipeOptions.CurrentUserOnly` named-pipe server. Later GUI and CLI launches forward their original
arguments and wait for a structured result. All apply, disable, wake/unlock, startup, watchdog,
tray, hotkey, and CLI paths serialize through `AppController`.

Safety rules:

- Desired Disabled never enters the native apply path.
- Disable is successful only when the DLL is verified absent; an unknown/partial result remains a failure.
- Recovery is bounded and only runs while desired state is Enabled.
- Scheduled-task and hotkey settings are committed only after the OS operation succeeds.
- Unsupported DWM state, empty live topology, and ambiguous display selection never reach injection.
- Partial injection across DWM processes is rolled back; a failed Active state is not accepted as healthy.

## Apply flow

1. `MonitorService` reconciles attached displays into canonical state objects.
2. `InjectionService` validates elevation, the matching native DLL, LUT files, and DWM support.
3. The app stages the DLL, LUTs, `manifest.tsv`, and resolver mode under `%SystemRoot%\Temp`.
4. The app injects `dwm_lut.dll` into every accessible `dwm.exe` process with `LoadLibraryA`.
5. The DLL reads the loaded `dwmcore.dll` CodeView PDB GUID/age and selects exact compiled RVAs.
6. The DLL hooks the supported DWM present path and applies the selected SDR/HDR shader.
7. The app re-queries DWM modules and reports observed Active/Inactive/Unknown state.

## DWM profile strategy

Windows marketing/build numbers are not the compatibility key. Cumulative updates can reuse a
DWM payload across build labels, so the native resolver uses the loaded `dwmcore.dll` PDB GUID and
age. A compiled profile is labelled `built`/validation-pending until a maintainer preserves live
evidence. Only `verified-stable` or `live-validated` profiles are shown as verified.

## Display identity and LUT routing

The persistent identity is the monitor device path, with adapter LUID + target ID as the scoped
fallback. Source IDs and desktop coordinates are topology metadata and never reclaim a saved LUT
assignment by themselves. HDR state is queried with the exact adapter LUID + target ID pair.

The native single-display path may select that display directly. Multi-display routing currently
uses the DWM-private output coordinate and the signal domain as a degraded runtime key because an
identity-bearing private DWM object has not yet been proven across supported profiles. It therefore
fails closed: only an exact coordinate plus SDR/HDR-domain match receives a LUT. There is no nearest
position, first-LUT, or cross-domain fallback. Ambiguous/unmatched output contexts receive no LUT
and are logged for release evidence.

## Startup, recovery, and shutdown

The elevated scheduled task launches the current executable with `--startup`. On every owner launch,
an enabled task is recreated and verified so a moved portable directory is repaired. `--startup`
only restores desired Enabled state. A periodic watchdog observes DWM and attempts bounded healing
after a cold start or DWM restart. Power, session, and display events use the same intent-gated path.

Explicit uninstall removes the scheduled task but retains user calibration state. Major upgrades
preserve the task. Portable users can remove it from Settings before deleting the folder.

## Distribution

The primary artifacts are a self-contained x64 portable ZIP and a per-machine WiX MSI. Each must
contain the managed app and matching `dwm_lut.dll`; missing native output is a build failure. MSIX
and Microsoft Store distribution are not claimed because the product requires elevation and DWM
DLL injection. WiX is pinned to 6.0.2; moving to WiX 7 requires a deliberate OSMF EULA decision.
