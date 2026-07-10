# DWM LUT Modern v5 full-scope implementation report

Date: 2026-07-10
Prototype checkpoint: `4e5e281`
Implementation plan: `docs/implementation/full-scope-v5-plan.md`

## Outcome

The repository now has a testable .NET 8 control plane, a fail-closed native routing policy, a
single-owner CLI/GUI process model, truthful OS-setting transactions, current screenshots and docs,
and reproducible x64 portable/MSI packaging. The legacy GUI remains intact as a rollback artifact.

This implementation does not label compiled DWM profiles stable. Live elevated SDR/HDR, recovery,
multi-monitor, install/uninstall, and calibration measurements remain the release-evidence gate.

## Reliability and state

- `AppController` serializes UI, tray, hotkey, CLI, startup, display, power/session, and watchdog
  operations through one gate.
- Persisted `IsEnabled` represents desired state; module enumeration represents observed state.
- Desired Disabled never calls apply. Cold-start/DWM-restart healing is bounded while Enabled.
- Disable remains retryable until the native DLL is verified absent.
- Applying with no LUT is rejected. Clearing the last LUT disables and uninjects instead of arming
  an endless recovery cycle.
- State saves use a same-directory temporary file plus atomic replacement; corrupt input recovers
  to a disabled state without silently applying a legacy configuration.

## Process and command surface

- A per-user mutex elects one owner. A `PipeOptions.CurrentUserOnly` duplex named pipe forwards
  later GUI/CLI launches and returns success, error text, and exit code.
- Unknown/missing/conflicting CLI options are rejected. Monitor assignment validates display and
  LUT paths. `--status` reports desired/observed state and last operation failure.
- Startup only restores Enabled intent; stale `StartMinimized` no longer hides manual launches.

## Windows integrations

- Autostart uses an elevated logon scheduled task, verifies create/remove, commits settings only on
  success, handles already-absent removal without localized error parsing, and recreates enabled
  tasks to repair moved portable paths.
- Hotkey registration reports Win32 failure and uses `MOD_NOREPEAT`; Settings rolls back rejected
  bindings.
- Immediate redraw uses Win32 desktop invalidation and `DwmFlush`, not a temporary top-most window.
- Onboarding only completes after its requested autostart/apply actions succeed.

## Display and native safety

- Persistent matching uses device path, then adapter LUID + target ID. Source ID and position are
  topology metadata only; HDR queries use the exact adapter/target pair.
- Apply passes the full connected topology even when only one display has an assigned LUT.
- Apply refuses an empty live topology, CLI monitor numbers must resolve uniquely, onboarding makes
  the target display explicit, and unsupported DWM architectures are centrally blocked.
- Native routing is extracted into `LutRouting.h` and compiled tests cover exact match, missing
  domain, negative coordinates, multi-display single-LUT, and duplicate ambiguity.
- The single-display fast path requires a manifest proving exactly one connected display.
- Multi-display routing accepts exactly one coordinate + SDR/HDR match. Nearest, first-available,
  primary-coordinate coercion, opposite-domain, and duplicate-candidate fallbacks are absent.

## Build, packaging, and CI

- x64/ARM64 managed platform mappings select the matching native output; absence is a hard failure.
- `scripts/test-build.ps1` compiles Release, checks generated profiles/artifacts, runs native routing
  tests and 35 managed tests, and rejects unsafe native routing fragments.
- `scripts/package-modern-app.ps1` produces architecture-labelled framework-dependent or
  self-contained portable folders/ZIPs with hashes and user documentation.
- `scripts/package-modern-installer.ps1` performs a clean x64 build, publishes self-contained, and
  creates a WiX 6.0.2 per-machine MSI plus SHA-256. Uninstall removes the autostart task and retains
  per-user calibration state.
- Build/release workflows provision .NET/MSBuild/NuGet and publish modern artifacts; the release
  workflow also retains the legacy rollback package.

## Automated evidence from this implementation

- Full Release build: zero warnings, zero errors.
- Native routing test executable: pass.
- Managed xUnit suite: 35 passed, 0 failed, 0 skipped.
- Static/generated-profile/package-input checks: pass.
- Isolated non-elevated UI captures regenerated for Home, LUTs, Settings, and About without writing
  normal `%AppData%` state.

Final local x64 artifacts from this implementation:

- Portable ZIP SHA-256: `73D12EFEBBE30726097475ED6011C28F325DE04CEA6E2023BF8F959A3F4DE721`
- MSI SHA-256: `54134CCF953557AD30DFBBB620045CB1BD956DB41D415D760DBCBEBABE6A446E`
- Native x64 DLL SHA-256: `A04C433ECFB3BFCB029C75C973BDFF9899B08DC2FBDEF38E8F42BED53ED7F465`

The EXE and MSI both report `5.0.0`; all payload hashes and packaged guide images were re-verified.
Rebuild and replace these hashes whenever source or packaged documentation changes.

## Known residual risk / required live work

- Private DWM offsets and hooks can change with any Windows update.
- Multi-display native output identity remains coordinate-derived at runtime and therefore fails
  closed when it cannot prove an exact match.
- GPU composition, protected/exclusive content, HDR transitions, Task Scheduler elevation, DWM
  restart behavior, and calibration accuracy cannot be proven by unit/build tests.
- Release binaries are unsigned.
- ARM64 build plumbing exists, but requires a matching toolchain, ARM64 DWM profiles, package build,
  and its own live evidence before release.

Use `docs/validation/release-evidence-template.md` and `tests/manual-test-plan.md` for promotion.
