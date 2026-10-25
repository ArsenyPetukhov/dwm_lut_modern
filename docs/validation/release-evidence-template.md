# DWM LUT Modern release evidence

Copy this file for each profile/topology validation. Do not mark a profile live-validated until all
applicable rows contain real observations and the referenced logs/hashes are preserved.

## Build and package

- Date / tester:
- Git commit:
- App version:
- Portable ZIP SHA-256:
- MSI SHA-256:
- Native DLL SHA-256:
- Install source: portable / MSI

## Windows and DWM

- Windows edition, version, build, channel:
- `dwmcore.dll` file version:
- CodeView PDB GUID / age:
- Resolver mode and selected profile:
- Profile classification before / after this run:

## Hardware and topology

- GPU(s) and driver(s):
- Monitor(s), connector(s), HDR capability:
- Desktop positions (including negative coordinates):
- Clone/extend mode:
- Scaling per display:
- Multi-adapter: yes / no

## Evidence matrix

| Case | Expected | Observed | Pass | Evidence/log timestamp |
|---|---|---|---|---|
| SDR apply with obvious LUT | Exact SDR display changes | | | |
| Disable | Baseline restored; DLL absent | | | |
| HDR on with distinct HDR LUT | Exact HDR LUT, never SDR LUT | | | |
| Missing-domain LUT | No cross-domain correction | | | |
| Disabled + app restart/logon | Remains disabled | | | |
| Disabled + sleep/wake/unlock | Remains disabled | | | |
| Enabled + DWM restart | Bounded recovery succeeds or visible failure | | | |
| Display reconnect/rearrange | Assignment follows stable identity | | | |
| Ambiguous/unmatched output | No LUT applied | | | |
| Negative-coordinate display | Exact display/domain only | | | |
| Mixed SDR/HDR displays | Each output uses its own domain | | | |
| MSI install/launch | App and matching DLL present | | | |
| MSI uninstall | App removed; autostart task absent; user state retained | | | |

## Measurement and diagnostics

- Calibration/verification instrument and software:
- Before/after measurement summary:
- Sanitized `dwm_lut.log` path:
- App crash log path (if any):
- Screenshots/photos:
- Deviations, failures, follow-up issues:

## Sign-off

- Validator:
- Maintainer:
- Final classification: live-validated / experimental / unsupported
- Compatibility matrix and changelog updated: yes / no
