# DWM LUT Modern v5 — Full-Scope Implementation Plan

Status: implementation complete; elevated live release evidence remains open
Baseline: preserved prototype checkpoint `4e5e281`
Owner: Arseny Petukhov / Codex implementation session

## Destination

Ship a production-quality, one-click Windows 11 DWM LUT application that keeps the legacy GUI
available as a rollback, correctly applies separate SDR and HDR calibration LUTs, never silently
re-enables a LUT the user disabled, fails safely on unsupported or ambiguous DWM/display states,
and is buildable, testable, installable, diagnosable, and releasable from this repository.

The destination includes the native DLL, GUI/control plane, portable package, installer, CLI,
autostart, sleep/wake and DWM-restart recovery, multi-monitor behavior, documentation, CI, and
release evidence. It is not complete merely because the UI renders or the solution compiles.

## Non-negotiable safety invariants

1. A persisted **Desired state = Disabled** must never call the native apply path during startup,
   wake, unlock, display change, watchdog polling, update, or installer repair.
2. **Desired state** and **Observed injection state** are separate. The UI must show disagreement,
   retain a usable Disable action, and expose the last failure.
3. Disable succeeds only after every accessible DWM process is verified not to contain
   `dwm_lut.dll`. Partial removal is a failure, not a successful toast.
4. A failed or ambiguous monitor match must apply no LUT. It must never fall back to another
   display's LUT.
5. SDR and HDR LUTs are separate signal domains. Missing one domain means no correction in that
   domain, never cross-applying the other LUT.
6. Only one process owns state and injection decisions. Secondary GUI/CLI launches send a command
   to that owner and receive a result.
7. User-visible settings reflect the real OS state. Scheduled-task and hotkey failures must not be
   persisted or presented as success.

## Public seams under test

- `AppController`: desired-state transitions, observed state, recovery policy, failures.
- State store: durable JSON, corrupt-state recovery, schema migration, atomic writes.
- Command surface: CLI parsing, validation, named-pipe request/response, single-owner dispatch.
- Autostart manager: create, query, remove, verify, repair, and path changes.
- Monitor provider/reconciler: adapter-aware identity, reconnect, topology changes, HDR state.
- LUT routing policy: exact display/domain selection and fail-closed ambiguity behavior.
- Packaging contract: every package contains the matching managed app and native DLL.

Tests use fakes only at OS boundaries (DWM injection, Task Scheduler, display APIs, hotkey API,
filesystem/time where necessary). Product behavior is asserted through the public seams above.
The `DWM_LUT_STATE_ROOT` environment override is compiled only into the isolated UI-capture build;
normal production builds always use `%AppData%\DwmLutModern`.

## Execution stages

### 1. Preserve and make the prototype testable

- Commit the current v5 prototype as a recoverable checkpoint.
- Add a .NET test project and explicit OS-boundary interfaces.
- Capture known bugs as failing tests before changing their behavior.
- Make modern app build/package absence fail CI instead of warning or silently skipping.

### 2. Correct the control/state model

- Replace the overloaded `IsEnabled` interpretation with explicit desired and observed state.
- Make apply/disable results verified and actionable.
- Allow Disable whenever desired state is enabled or injection may be active.
- Recover on normal launch when desired state is enabled, with bounded backoff and visible failure.
- Ensure wake/display/DWM restart recovery is intent-gated and serialized.

### 3. Establish one authoritative process

- Add per-user named-pipe IPC and request/response messages.
- Forward CLI and secondary GUI activations to the running owner.
- Validate unknown options, missing values, LUT paths, and monitor selectors.
- Return meaningful process exit codes and JSON status/results.

### 4. Make OS settings truthful

- Make autostart changes transactional and verify the task after creation/removal.
- Gate start-minimized behavior semantically, not only visually.
- Repair or report a moved portable executable path.
- Return and display `RegisterHotKey` errors; reject duplicate/reserved shortcuts.

### 5. Harden display and native routing

- Model identity as monitor device path plus adapter LUID and target ID; treat source ID and
  coordinates as current-topology metadata only.
- Retry `QueryDisplayConfig` after `ERROR_INSUFFICIENT_BUFFER`.
- Query HDR state using the exact adapter LUID + target ID pair.
- Remove nearest/first-LUT native fallbacks for multi-monitor operation.
- Investigate an identity-bearing DWM object path. Until proven, exact coordinate routing is an
  explicitly degraded mechanism that fails closed and is logged as such.
- Extract native LUT parsing/routing into testable functions and add deterministic tests.

### 6. Finish product delivery

- Produce current framework-dependent and self-contained portable packages.
- Add a per-machine x64 MSI/EXE installer with clean uninstall and shortcuts.
- Ensure uninstall removes or disables the autostart task without deleting user calibration data
  unless explicitly requested.
- Wire modern packages, hashes, tests, screenshots, and artifacts into CI/release workflows.
- Refresh README, user guide, troubleshooting, compatibility, security, and changelog.

### 7. Prove the release

- Run the complete automated suite and static package checks.
- Run non-destructive UI automation and accessibility/layout checks at supported scaling.
- On an elevated physical/VM session, validate identity and obvious LUTs for SDR/HDR, disabled
  reboot/wake behavior, DWM restart recovery, and supported multi-monitor topologies.
- Store exact build/GPU/display facts and sanitized logs as release evidence.
- Regenerate screenshots from the exact packaged binary.

## Release classification

- **Stable:** exact DWM profile plus completed live validation for the stated topology.
- **Experimental:** compiled/profiled but missing live validation; explicit warning and fail-closed
  behavior.
- **Unsupported:** no safe resolver/profile or architecture mismatch; Apply remains unavailable.

The UI must not translate “profile exists” into “verified stable.”

## Out of scope for v5 completion

- Microsoft Store distribution: the app requires elevation and injects an in-process DLL into DWM,
  which conflicts with normal Store/MSIX constraints.
- Automatic download/execution of unsigned native binaries.
- Claiming calibration accuracy without calibrator-generated LUTs and measurement evidence.

## Implementation status (2026-07-10)

| Stage | Result |
|---|---|
| Preserve/test seams | Complete: checkpoint plus managed and native routing test harnesses |
| State/recovery | Complete: desired/observed separation, verified disable, bounded healing, empty-config disarm |
| Single owner/CLI | Complete: per-user mutex and current-user-only duplex command pipe |
| OS settings | Complete: transactional autostart, moved-path repair, hotkey errors, semantic start-hidden |
| Display/native safety | Complete for implemented routing: adapter-aware identity and fail-closed exact/domain policy |
| Delivery | Complete: x64 portable, self-contained ZIP, WiX MSI, CI/release integration, screenshots |
| Live proof | Open release gate: elevated SDR/HDR, recovery, topology, calibration, install/uninstall evidence |

The remaining live-proof row is deliberately not converted into a code claim. It requires a real
elevated DWM/GPU/display session and the evidence template under `docs/validation`.
