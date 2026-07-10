# Support

Before opening an issue, collect:

- DWM LUT Modern version, portable/MSI source, and package SHA-256;
- Windows edition, exact build number, and Stable/Preview/Insider channel;
- `dwmcore.dll` profile/PDB line from `%SystemRoot%\Temp\dwm_lut.log`;
- GPU model and driver version;
- each monitor model, connector, desktop position, and adapter/GPU;
- HDR enabled per monitor and whether the topology mixes SDR/HDR;
- LUT format, grid size, and whether the failing slot is SDR or HDR;
- desired state shown by the app and observed injection status;
- reproduction steps and sanitized `%SystemRoot%\Temp\dwm_lut.log`;
- `%AppData%\DwmLutModern\crash.log`, if present.

Run `DwmLutModern.exe --status` while the tray owner is running and include the JSON response.
Remove personal paths or monitor serial information if needed, but keep profile IDs and errors.

The easiest route is to run the collector from an elevated PowerShell terminal:

```powershell
.\scripts\validation\Collect-ValidationEvidence.ps1
```

It writes `compatibility-report.md` beside the detailed JSON and logs. Review it for private data,
then paste it into the
[Windows compatibility report](https://github.com/ArsenyPetukhov/dwm_lut_modern/issues/new?template=compatibility_report.yml).

Reports cannot be diagnosed reliably without an exact Windows/DWM identity and logs. Calibration
accuracy claims also need measurement data. If using modified binaries, include the source diff and
matching binary hashes.
