# Windows validation lab: primary-source findings

Research date: 2026-07-10. Sources are Microsoft documentation. Statements under **Project inference** are conclusions for DWM LUT Modern; they are not claims made by Microsoft.

## Executive conclusion

Use virtualization for repeatable install, UI, privilege, injection-lifecycle, recovery, and topology-logic tests. Do not treat Windows Sandbox, an RDP/Enhanced Session, or ordinary virtual displays as proof that a LUT reached a particular physical SDR/HDR panel. Final display routing and color-correction acceptance needs a console session on physical hardware, real monitors, and, where color accuracy matters, measurement equipment.

## Windows Sandbox

### Sourced facts

- A `.wsb` file can configure vGPU, networking, mapped folders, a logon command, protected client mode, clipboard/device redirection, and memory. Disabling vGPU selects WARP software rendering. Microsoft warns that vGPU and networking increase attack surface. Mapped folders may expose host data; writes to a write-enabled mapping survive disposal of the sandbox. [Use and configure Windows Sandbox](https://learn.microsoft.com/en-us/windows/security/application-security/application-isolation/windows-sandbox/windows-sandbox-configure-using-wsb-file)
- Microsoft's safer sample for testing an unknown download disables networking and vGPU and exposes the host folder read-only. [Windows Sandbox sample configuration files](https://learn.microsoft.com/en-us/windows/security/application-security/application-isolation/windows-sandbox/windows-sandbox-sample-configuration)
- Sandbox is disposable, permits only one instance at a time, and runs the same Windows build as its host. Closing it deletes sandbox software, files, and state. [Windows Sandbox FAQ](https://learn.microsoft.com/en-us/windows/security/application-security/application-isolation/windows-sandbox/windows-sandbox-faq)
- The Sandbox desktop is remoted to the host through an Indirect Display Driver; enabling vGPU shares graphics execution but does not turn the remoted desktop into a physical monitor connection. [GPU paravirtualization](https://learn.microsoft.com/en-us/windows-hardware/drivers/display/gpu-paravirtualization#windows-sandbox)
- Sandbox requires supported Windows, hardware virtualization, at least two CPU cores, and at least 4 GB RAM (8 GB recommended). Running it inside a VM requires nested virtualization. [Install Windows Sandbox](https://learn.microsoft.com/en-us/windows/security/application-security/application-isolation/windows-sandbox/windows-sandbox-install)

### Project inference

Sandbox is appropriate for these automated smoke checks:

1. Portable/MSI contents, startup, uninstall, and first-run state.
2. UI navigation, CLI parsing, single-instance IPC, invalid-LUT rejection, and log/evidence creation.
3. Clean-machine dependency and privilege failures.
4. DWM injection lifecycle only if it works in that Sandbox build, with the result labeled **virtual/remoted-path evidence**.

Use an absolute, read-only package mapping, no network, and a separate write-enabled evidence mapping only when results must return to the host. Set `<vGPU>` explicitly and test both enabled and WARP paths when relevant; Microsoft's [Sandbox configuration](https://learn.microsoft.com/en-us/windows/security/application-security/application-isolation/windows-sandbox/windows-sandbox-configure-using-wsb-file) and [GPU paravirtualization](https://learn.microsoft.com/en-us/windows-hardware/drivers/display/gpu-paravirtualization#windows-sandbox) pages currently disagree about its universal default. A Sandbox pass does **not** establish physical EDID routing, HDR transport, per-connector behavior, multiple physical monitor behavior, or measured color correctness.

## Hyper-V

### Sourced facts

- Hyper-V supports Standard checkpoints (memory, disk, and device state; not a backup) and Production checkpoints (VSS/file-system-freeze data-consistent state without VM memory). Microsoft exposes `Checkpoint-VM`, `Restore-VMCheckpoint`, and checkpoint-type controls for repeatable rollback. [Using checkpoints](https://learn.microsoft.com/en-us/windows-server/virtualization/hyper-v/checkpoints)
- Enhanced Session Mode uses RDP and supplies resizable/high-DPI presentation plus host resource redirection. [Share devices with your Hyper-V virtual machine](https://learn.microsoft.com/en-us/windows-server/virtualization/hyper-v/enhanced-session-mode)
- `QueryDisplayConfig` and `DisplayConfigGetDeviceInfo` can return `ERROR_ACCESS_DENIED` when called without console-session/current-desktop access or from a remote session. [QueryDisplayConfig](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-querydisplayconfig), [DisplayConfigGetDeviceInfo](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-displayconfiggetdeviceinfo)

### Project inference

Maintain a clean VM for each Windows build under test and restore a named pre-install checkpoint before each run. Use Production checkpoints for durable clean baselines; use Standard checkpoints only when intentionally preserving a live failure state.

Run display-discovery and injection tests from the VM's **Basic/console session**, not Enhanced Session Mode. Enhanced Session is useful for UI and packaging checks but changes the session/display path. Treat `ERROR_ACCESS_DENIED` or an empty topology from an RDP/Enhanced session as invalid evidence, not as a zero-monitor result. An ordinary Hyper-V virtual adapter/display still cannot prove physical connector identity, a monitor's actual HDR mode, multi-GPU ownership, or LUT output on a real panel.

## Multi-monitor identity and coordinates

### Sourced facts

- `QueryDisplayConfig(QDC_ONLY_ACTIVE_PATHS | QDC_VIRTUAL_MODE_AWARE, ...)` returns the current active source-to-target paths and their mode table. Microsoft's sample retries if the buffer becomes insufficient because topology can change between buffer sizing and enumeration. The API does not participate in DPI virtualization; display sizes are physical pixels. [QueryDisplayConfig](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-querydisplayconfig)
- `DISPLAYCONFIG_SOURCE_MODE` provides width and height in pixels plus the source surface's upper-left position in desktop coordinates; `(0, 0)` identifies the primary source. [DISPLAYCONFIG_SOURCE_MODE](https://learn.microsoft.com/en-us/windows/win32/api/wingdi/ns-wingdi-displayconfig_source_mode)
- `EnumDisplayMonitors` enumerates monitor rectangles in virtual-screen coordinates when called without an HDC. [EnumDisplayMonitors](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-enumdisplaymonitors)
- For a target, `DISPLAYCONFIG_TARGET_DEVICE_NAME` includes adapter/target request identity, connector technology and instance, optional EDID manufacturer/product IDs, friendly name, and `monitorDevicePath`, which can be used with SetupAPI. [DISPLAYCONFIG_TARGET_DEVICE_NAME](https://learn.microsoft.com/en-us/windows/win32/api/wingdi/ns-wingdi-displayconfig_target_device_name)
- Display removal is asynchronous: an active path can transiently report `targetAvailable == FALSE` after unplug. [DISPLAYCONFIG_PATH_TARGET_INFO](https://learn.microsoft.com/en-us/windows/win32/api/wingdi/ns-wingdi-displayconfig_path_target_info)

### Project inference

Use one topology snapshot to join these domains:

1. CCD active path: adapter LUID + source ID + target ID.
2. Source mode: physical-pixel desktop rectangle.
3. Target metadata: `monitorDevicePath`, connector instance, and EDID fields when valid.
4. DWM hook observation: physical-pixel source/monitor rectangle.

Prefer `monitorDevicePath` as the persisted monitor key and keep EDID/connector data as diagnostics or fallback, not as an assumption of uniqueness. Microsoft's cited API pages describe the fields but do not promise that any one field is a forever-stable identifier across GPU-driver reinstall, docking, MST changes, or cloned displays. Validate persistence empirically across reboot, sleep/resume, unplug/replug, port changes, and dock changes. If runtime coordinates do not map unambiguously to exactly one configured active path, fail closed rather than apply another monitor's LUT.

## HDR and Advanced Color evidence

### Sourced facts

- Windows can change Advanced Color state when the user changes settings, a window moves between displays, or power state changes. Microsoft documents `DXGI_OUTPUT_DESC1`/`IDXGIOutput6::GetDesc1` for Win32 DirectX apps; its color space distinguishes conventional SDR from active HDR, but DXGI cannot distinguish an Advanced-Color SDR display from conventional SDR or report supported-but-inactive modes. [Use DirectX with Advanced Color](https://learn.microsoft.com/en-us/windows/win32/direct3darticles/high-dynamic-range)
- `DXGI_OUTPUT_DESC1` also reports the output bounds, `HMONITOR`, active-wire bits per color, primaries, white point, and minimum/maximum luminance. These values usually come from EDID or an override. [DXGI_OUTPUT_DESC1](https://learn.microsoft.com/en-us/windows/win32/api/dxgi1_6/ns-dxgi1_6-dxgi_output_desc1)
- CCD device-info request types include `DISPLAYCONFIG_DEVICE_INFO_GET_ADVANCED_COLOR_INFO` and `DISPLAYCONFIG_DEVICE_INFO_GET_SDR_WHITE_LEVEL`. [DISPLAYCONFIG_DEVICE_INFO_TYPE](https://learn.microsoft.com/en-us/windows/win32/api/wingdi/ne-wingdi-displayconfig_device_info_type)
- `DISPLAYCONFIG_SDR_WHITE_LEVEL` reports current SDR white in units of `80 nits * value / 1000`. [DISPLAYCONFIG_SDR_WHITE_LEVEL](https://learn.microsoft.com/en-us/windows/win32/api/wingdi/ns-wingdi-displayconfig_sdr_white_level)
- Microsoft's calibration workflow begins with optical measurements of known rendered values and derives a corrective transform from those results. Windows' supported hardware-calibration path is loaded through MHC2 ICC profiles rather than a direct runtime control API. [Windows hardware display color calibration pipeline](https://learn.microsoft.com/en-us/windows/win32/wcs/display-calibration-mhc)

### Project inference

Capture, per active target and at each lifecycle transition: adapter/target identity, monitor path, desktop rectangle, active color mode/HDR state, SDR white level, OS/build, DWM version/hash, GPU/driver version, requested LUT, and native hook result. Telemetry proves the environment and routing decision; it does not prove the pixels were transformed correctly.

## Recommended validation ladder

| Tier | Environment | What it can establish | What it cannot establish |
|---|---|---|---|
| 0 | Unit/native tests and package inspection | LUT parsing, interpolation, routing policy, state machine, artifact completeness | DWM integration or displayed pixels |
| 1 | Windows Sandbox | Clean install/run/uninstall, UI/CLI/IPC, missing dependency behavior, disposable smoke tests | Physical display, cross-build matrix, HDR panel, physical multi-monitor routing |
| 2 | Hyper-V Basic/console session | Per-build compatibility, injection/recovery/watchdog behavior, virtual topology cases, reboot/lock/update regressions | Real EDID/connector/GPU-output ownership or calibrated output |
| 3 | Physical Windows systems | Real DWM/GPU driver path, SDR/HDR toggles, sleep/resume, DWM restart, multi-monitor/multi-GPU/clone/rotation/dock cases | Quantitative color accuracy without measurement |
| 4 | Physical system + colorimeter/spectrometer | Objective before/after ramps, neutrality, gamma/EOTF, monitor isolation, and regression thresholds | None |

For a release claim that "the LUT is applied to the intended monitor," require Tier 3 routing evidence and Tier 4 measurements. Keep Sandbox and Hyper-V results as valuable but explicitly narrower evidence.
