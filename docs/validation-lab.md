# Validation lab

This project modifies the Desktop Window Manager, so validation has four different meanings. A
green build does not prove a correct color transform, and a virtual display does not prove a
physical HDR or multi-monitor path. Use the cheapest tier that can answer the question, then retain
the evidence from the higher tiers before calling a Windows/DWM profile live-validated.

## No-reinstall build-family strategy

Do not reinstall the host for every Windows build. Windows Sandbox is built from the host OS, so it
cannot become an arbitrary older or Insider release. Keep a small Hyper-V library instead: one
checkpointed guest per distinct compositor foundation/hook strategy, then add a new checkpoint or
guest only when `dwmcore.dll` identity or the hook implementation meaningfully changes.

Use three kinds of coverage together:

1. Run offline payload/profile checks for **every** compiled PDB GUID/age. These prove exact
   selection, PE/RVA sanity, generated tables, and fail-closed mismatch behavior.
2. Run an obvious-LUT lifecycle smoke test in one VM per implementation family: Windows 10 19045,
   legacy Windows 11, 24H2/26100, 25H2/262xx, 26H1/280xx, and Canary when it is shipped.
3. Let exact-build community reports fill additional cumulative-update and hardware cells. Record
   them as community evidence rather than silently promoting them to maintainer validation.

Two Windows labels carrying the same exact DWM payload/profile do not need duplicate structural
qualification. A new PDB GUID/age remains a new payload candidate even when Microsoft keeps the
same marketing version. The primary-source rationale, media options, and VM limitations are in
[`docs/research/windows-build-family-validation.md`](research/windows-build-family-validation.md).

## What each tier proves

| Tier | Environment | What it can prove | What it cannot prove |
|---|---|---|---|
| 0 | Managed/native automated tests | State intent, CLI/IPC, monitor identity, exact fail-closed routing, LUT parsing, packaging guards | DWM injection, shader execution, GPU output, color accuracy |
| 1 | Windows Sandbox | Clean-machine package extraction, PE architecture match, UAC/startup, WPF rendering, uninstall hygiene | Physical displays, real HDR, persistent sleep/reboot, reliable DWM-private-hook behavior |
| 2 | Hyper-V Windows VM | Exact guest Windows build, elevation, DWM load/unload, watchdog and checkpoint-repeatable fault tests | Host physical output identity, calibration accuracy, real multi-monitor/HDR connector behavior |
| 3 | Physical Windows machine | Real GPU driver, connector, SDR/HDR transition, sleep/reconnect, multi-monitor/multi-adapter routing | Instrumented calibration accuracy unless a meter is also used |
| 4 | Physical machine + colorimeter/spectrometer | End-to-end correction accuracy and regression measurements | Nothing in the stated topology, if all evidence is retained |

Windows Sandbox supports `.wsb` XML configuration, vGPU, mapped folders, and logon commands. Its
contents are discarded when it closes. Microsoft also warns that mapped host folders cross the
isolation boundary, so this repository's launcher maps the package/scripts read-only and only the
dedicated evidence folder read-write. Networking and clipboard are disabled. See Microsoft's
[Sandbox configuration reference](https://learn.microsoft.com/en-us/windows/security/application-security/application-isolation/windows-sandbox/windows-sandbox-configure-using-wsb-file)
and [installation prerequisites](https://learn.microsoft.com/en-us/windows/security/application-security/application-isolation/windows-sandbox/windows-sandbox-install).

Hyper-V checkpoints are useful for repeatable update/install/failure tests. Microsoft documents
standard checkpoints as appropriate for test/development state rollback; they are not a substitute
for a clean OS image or physical-display proof. See [Hyper-V checkpoint concepts](https://learn.microsoft.com/en-us/windows-server/virtualization/hyper-v/features-terminology#checkpoints)
and [VMConnect](https://learn.microsoft.com/en-us/windows-server/virtualization/hyper-v/virtual-machine-connection).

## Tier 0: automated gate

Run from an ordinary PowerShell terminal in the repository root:

```powershell
.\scripts\test-build.ps1
.\scripts\capture-modern-ui.ps1
.\scripts\package-modern-installer.ps1
git diff --check
```

The UI harness uses an `asInvoker` smoke manifest and an isolated state/singleton scope. It can run
beside the real elevated tray app without forwarding commands into it. The screenshots under
`docs/images` are checked-in render evidence, not pixel-perfect snapshot tests.

## Tier 1: Windows Sandbox

Prerequisite (administrator PowerShell, once):

```powershell
Enable-WindowsOptionalFeature -FeatureName Containers-DisposableClientVM -All -Online
```

Build the package, generate the `.wsb`, then launch it:

```powershell
.\scripts\package-modern-installer.ps1
.\scripts\validation\Start-WindowsSandboxValidation.ps1 -Launch
```

Inside Sandbox:

1. Confirm `sandbox-smoke.json` says the EXE and DLL PE machine types match.
2. Accept the UAC prompt.
3. Open Home, LUT Library, Settings, and About; check clipping at 100%, 150%, and 200% scaling if available.
4. Import a generated identity LUT and confirm state persists during the Sandbox session.
5. Close the app and verify it remains available from the tray.
6. Close Sandbox. Evidence returns to `artifacts/validation/sandbox-evidence`.

Do not mark injection or color correction passed from this tier. Sandbox has a virtualized GPU and
virtual display path; the test is package/UI hygiene only.

## Tier 2: repeatable Hyper-V matrix

Create a Generation 2 Windows 11 VM for each representative DWM implementation family under test.
Keep one clean checkpoint after Windows Update and GPU integration services finish, before
installing DWM LUT. Preserve exact-build checkpoints only when they add a distinct payload identity
or useful servicing boundary.

For each build:

1. Revert to the clean checkpoint.
2. Copy the ZIP and MSI into the guest and record their hashes.
3. Install the MSI, reboot, and run the evidence collector elevated.
4. Apply `diagnostic-red-invert-2.cube`. Confirm an obvious visual change in the VM desktop.
5. Disable; confirm the baseline returns and `verify-lut-loaded.ps1` reports no module.
6. Leave Disabled, sign out/in, restart, lock/unlock, and restart DWM. It must remain disabled.
7. Leave Enabled, restart DWM. The bounded watchdog must recover or surface a failure.
8. Exercise MSI repair, major upgrade, and uninstall. Explicit uninstall must remove the task while retaining user state.
9. Revert the checkpoint and repeat once to prove reproducibility.

VM success proves that the private DWM hook can initialize on that guest payload. It still does not
validate the host's monitor device paths, physical HDR signaling, connector behavior, or measured
calibration.

## Tier 3: physical SDR/HDR and multi-monitor matrix

Generate deterministic test LUTs:

```powershell
.\scripts\validation\New-DiagnosticLuts.ps1
```

`identity-2.cube` should be visually neutral. `diagnostic-red-invert-2.cube` is intentionally
obvious and must never be used as a calibration.

For one display, test both signal domains:

1. Windows HDR off, assign red-invert to SDR and identity to HDR. Only SDR should visibly change.
2. Windows HDR on, assign identity to SDR and red-invert to HDR. Only HDR should visibly change.
3. Remove the LUT for the active domain. No cross-domain LUT may be substituted.
4. Repeat apply/disable after display off/on, cable reconnect, sleep/wake, lock/unlock, DWM restart, and reboot.

For multiple displays, cover this topology matrix:

| Case | Required observation |
|---|---|
| Two displays, one configured | Only the exact configured output changes |
| Two displays, distinct obvious LUTs | Each output receives its own LUT |
| Mixed SDR/HDR | Each output receives only its active signal-domain LUT |
| Negative X or Y coordinate | Exact output changes; no nearest-display fallback |
| Rearranged desktop | Assignment follows the stable monitor device path after refresh/reapply |
| Disconnect/reconnect | Assignment returns to the same physical display |
| Two GPUs/adapters | Equal target/source numbers do not merge identities |
| Clone mode | Record the behavior as experimental; clone members are not independently routed |
| Unmatched/ambiguous native context | No LUT is safer than a wrong LUT; log must show fail-closed routing |

The original wrong-monitor bleed bug is fixed: the native router no longer uses nearest-position,
first-LUT, or cross-domain fallbacks, and the app passes the full topology even if only one display
has a LUT. The remaining limitation is important: private DWM runtime objects are still keyed by
exact desktop position, not the monitor device path. If DWM reports a coordinate the app cannot
match, that output receives **no LUT**. This is fail-closed correctness, not complete identity-based
routing, and it requires physical topology testing.

Microsoft's CCD API returns active source-to-target display paths, and `DisplayConfigGetDeviceInfo`
can resolve target names/device paths. The app therefore persists device path plus adapter LUID and
target ID, while treating source number and coordinates as live topology. See
[QueryDisplayConfig](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-querydisplayconfig),
[DisplayConfigGetDeviceInfo](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-displayconfiggetdeviceinfo),
and [DISPLAYCONFIG_TARGET_DEVICE_NAME](https://learn.microsoft.com/en-us/windows/win32/api/wingdi/ns-wingdi-displayconfig_target_device_name).

## Tier 4: measurement

For release-quality calibration claims, use the same instrument and verification patch set before
and after applying the production LUT. Preserve at least grayscale/white-point error, color error,
gamma or EOTF tracking, black level, peak luminance, measurement software/version, meter correction,
display mode, HDR state, and ambient-light conditions. An obvious diagnostic LUT proves routing; it
does not prove numerical accuracy.

## Evidence collection and sign-off

Run the collector from an elevated terminal after each physical/VM case:

```powershell
.\scripts\validation\Collect-ValidationEvidence.ps1
```

It records the commit, OS, `dwmcore.dll` version/hash, GPU drivers, monitor records, app status,
DWM module visibility, logs, and package hashes. Copy `docs/validation/release-evidence-template.md`
into that run and complete every applicable row. Inspect device identifiers before publishing.

A profile may move from `built` to `live-validated` only when the evidence includes an exact
`dwmcore.dll` identity, real GPU/display topology, obvious-domain routing, disable/recovery cases,
and sanitized logs. Calibration claims additionally require Tier 4 measurements.

Community reports extend the matrix but remain labelled `community-reported` until the maintainer
reproduces the relevant tier. Multiple independent reports increase confidence; absence of reports
is not evidence that a build works. A crash, wrong-monitor application, or failed disable should
downgrade the affected profile immediately.

The primary-source research behind these boundaries is preserved in
[`docs/research/windows-validation-lab-sources.md`](research/windows-validation-lab-sources.md).
