# Validation

## Programmatic Validation

Run:

```powershell
.\scripts\build-release.ps1
.\scripts\test-build.ps1
```

This validates that the repo builds, that artifacts exist, that profile strings are compiled into source, and that packages/checksums can be produced.

## Runtime Smoke Test

1. Launch GUI as Administrator.
2. Load a known `.cube` LUT.
3. Apply to primary display.
4. Confirm visible change.
5. Check `%SystemRoot%\Temp\dwm_lut.log`.
6. Disable LUT.
7. Confirm image returns to baseline.

## Multi-Monitor Validation

Test these cases separately:

- Single display.
- Dual display, same SDR/HDR state.
- Mixed SDR/HDR displays.
- Negative-coordinate secondary display.
- Different GPU outputs.
- Display sleep/reconnect.

The log must show the staged manifest and which LUT selection path was used.

## Regression Validation

Repeat after:

- Windows cumulative update.
- Windows Insider update.
- GPU driver update.
- DWM crash report.
