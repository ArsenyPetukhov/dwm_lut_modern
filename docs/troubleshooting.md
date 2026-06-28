# Troubleshooting

## LUT Does Not Apply

1. Confirm `DwmLutGUI.exe` was run as Administrator.
2. Confirm `dwm_lut.dll` is beside the EXE.
3. Check `%SystemRoot%\Temp\dwm_lut.log`.
4. Look for `Selected DWM symbol profile`.
5. If profile is missing, the build needs a new profile.

## Colors Are Blown Out

Likely causes:

- Wrong LUT domain.
- HDR LUT applied to SDR path or vice versa.
- LUT generated for another display mode.
- Windows HDR state changed after LUT generation.

Start again with an identity LUT.

## DWM Crashes

Disable the LUT if possible. If the desktop is unstable, reboot. Collect:

- Windows build number
- DWM log
- GPU driver version
- Package name
- LUT file type and size

## Multi-Monitor Applies Wrong LUT

Collect `%SystemRoot%\Temp\dwm_lut.log`. The log should include `manifest.tsv` rows and the selected LUT path. Attach that log to a compatibility report.
