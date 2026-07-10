# Manual Test Plan

## DwmLutModern.App (v5 rebuild) — intent, sleep, reboot, autostart

These cover the ground-up rebuild. The whole point of the rebuild is that a **disabled LUT never
comes back on its own**. Run elevated.

### Intent across reboot (the reported bug)
1. Assign an obvious SDR LUT to a display, click **Apply** → visible change; tray shows "active".
2. Reboot → LUT re-applies automatically (intent was Enabled). ✔
3. Click **Disable**, reboot → LUT **stays off**. ✔ (previously it came back)

### Intent across sleep / display change
4. **Apply**, then sleep and wake → LUT restored automatically. ✔
5. **Disable**, then sleep and wake → LUT **stays off**. ✔
6. **Disable**, then toggle Windows HDR or change resolution → LUT **stays off**. ✔

### Autostart toggle (both surfaces)
7. Settings → turn **Start with Windows** on → `schtasks /query /tn DwmLutModern_Autostart` finds it.
8. Turn it off (Settings or the tray checkbox) → the task is gone (`schtasks /query` = not found).
9. With autostart on and LUT enabled, reboot → applied at logon. With autostart on but LUT
   disabled, reboot → not applied.

### CLI
10. `DwmLutModern.exe --status` prints JSON (enabled, status, resolverMode, monitors, dwm).
11. `--apply`, `--disable`, `--toggle`, `--startup` behave per saved intent; `--help` lists flags.
12. `--apply-monitor 1 --sdr <path>` assigns and applies to display 1.

### App / packaging
13. Delete `%AppData%\DwmLutModern` → first-run wizard appears; complete it.
14. Run the portable build from a `C:\Program Files`-style path → config still reads/writes from
    `%AppData%` (no permission error).
15. Deny the UAC prompt once → clear error info bar + Retry, never a silently disabled button.
16. Tray icon reflects on/off; toasts fire on apply/disable/failure.
17. Multi-monitor: each card shows the correct display + its assigned LUT; unplug/replug a monitor
    and confirm its LUT is remembered (assignments are keyed to display identity, not position).

## Identity LUT

- Apply identity SDR LUT.
- Confirm no visible change.
- Confirm log shows selected profile.

## Obvious LUT

- Apply an obvious SDR test LUT.
- Confirm visible change.
- Disable and confirm baseline returns.

## HDR

- Enable Windows HDR.
- Apply identity HDR LUT.
- Apply obvious HDR test LUT.
- Collect log.

## Multi-Monitor

- Test single monitor first.
- Test two monitors with same mode.
- Test mixed SDR/HDR.
- Move primary monitor position and retest.
