# Troubleshooting

## LUT does not apply

1. Confirm `DwmLutModern.exe` was launched with the administrator prompt accepted.
2. Confirm `dwm_lut.dll` is beside the EXE and came from the same package/architecture.
3. Run `DwmLutModern.exe --status`; record desired and observed state plus the last error.
4. Check `%SystemRoot%\Temp\dwm_lut.log` for the PDB identity, selected profile, monitor count,
   parsed LUTs, and hook initialization result.
5. A `built`/validation-pending profile is not proven stable. Try a matching Compatibility mode
   only for an explicit test and preserve the log.

Apply is rejected when no SDR/HDR LUT is assigned. A LUT path that was moved or deleted produces a
visible error and remains desired Enabled for retry after the path is repaired.

## Disable is incomplete

The app reports success only after it can verify `dwm_lut.dll` is absent from DWM. If Disable stays
available, retry it. If the desktop is unstable, reboot; Disabled intent is persisted before removal
and will not re-inject at startup. Include the app and DWM logs in a report.

## Colors are wrong or blown out

Likely causes are a LUT generated for another monitor/mode, an HDR LUT in the SDR slot (or reverse),
or a Windows HDR state change after calibration. The app does not cross-apply a missing domain. Start
with an identity LUT in each domain and verify the active-domain text on the display card.

## Multi-monitor output receives no LUT

This is the safe result when the private DWM output coordinate does not exactly match the staged
display/domain or when candidates are ambiguous. The log should show `manifest.tsv`, the connected
monitor count, DWM coordinates, and either an exact match or `applying none`. Attach it with the
topology details from `SUPPORT.md`.

## Autostart or hotkey does not stick

Settings are transactional: a rejected Task Scheduler or `RegisterHotKey` operation is rolled back
and shown as an error. For a moved portable folder, launch the app once from the new location so it
can recreate the task. If that fails, disable/re-enable Start with Windows in Settings.

## DWM crashes

Disable if possible, otherwise reboot. Collect exact Windows/DWM identity, GPU driver, package
hashes, topology, LUT format/size, `%SystemRoot%\Temp\dwm_lut.log`, and the app crash log described
in `SUPPORT.md`.
