# Fullscreen windowed playback

**Follow-up:** the v5.0.3 policy still lost the LUT after MPC-HC's seekbar hid on
the affected laptop. The control writes described below were insufficient.
See [the corrected policy and research](fullscreen-hidden-seekbar.md) and
[the SDR workaround](../fullscreen-playback.md).

Date: 2026-10-26 (Asia/Yangon).

## Confirmed baseline and requested behavior

The user confirmed that `v5.0.1-amd24h2-preview` works for the earlier normal
windowed LUT case on the reported AMD laptop. That confirmation is recorded on
`main` as `live-validated`, scoped to that reported setup. It does not cover
fullscreen playback or other signal modes/topologies.

The new report is that the LUT disappears when a media player enters fullscreen,
including fullscreen windowed. The renderer is unknown; the implementation
therefore targets the shared DWM presentation path used by EVR, madVR, and MPC
Video Renderer. The user asked that all three be considered.

## Online findings

[Microsoft's flip-model documentation](https://learn.microsoft.com/en-us/windows/win32/direct3ddxgi/for-best-performance--use-dxgi-flip-model)
explains that a borderless window can present through Independent Flip, bypassing
normal desktop composition. Windowed fullscreen therefore does not establish
that a compositor LUT can see the video's pixels. Disabling MPO alone does not
establish that Independent Flip is disabled.

[PresentMon's official mode definitions](https://github.com/GameTechDev/PresentMon/blob/main/README-ConsoleApplication.md)
distinguish `Composed: Flip`, `Hardware: Independent Flip`, hardware-overlay
presentation, and exclusive/legacy presentation. This is the relevant distinction
for a later Windows capture; a window style or player fullscreen label is not
enough to identify the presentation mode.

[Lauralex's project](https://github.com/lauralex/dwm_lut) describes blocking
DirectFlip/MPO for active LUTs and excludes true exclusive fullscreen from its
normal promise. Its behavior on another GPU does not prove the current driver
uses the same path.

[DesktopLUT's source](https://github.com/sup3rflyer/DesktopLUT/blob/ce843651876c65255b752e88d07fce896b54282b/dwm_hook/dllmain.cpp)
uses a separate `DisableIndependentFlip` control alongside `OverlayTestMode`.
Its code validates the target page and saves/restores the previous value.
The [release history](https://github.com/sup3rflyer/DesktopLUT/releases)
also records replacement of removed flip hooks. Its implementation labels that
path 25H2; this fork instead checks the exact DWM payload, because the matching
control and runtime readers also exist in the reporter's 24H2 binary.

The [other maintained DWM LUT fork](https://github.com/GianSegugio/dwm_lut_is_back/blob/d9680e036953adfa28f08507cfa6867aa105f7f7/DOCUMENTATION.md)
describes a composition-blocking overlay as another approach. Its documented
alpha tint is undesirable for calibration, so no transparent/topmost blocker
window was added here.

The [MPC-HC maintainer FAQ](https://github.com/clsid2/mpc-hc/issues/2869)
explains that MPC Video Renderer can change Windows HDR state when entering
fullscreen. That is a distinct cause of an SDR LUT ceasing to apply. The repair
keeps SDR/HDR matching strict and logs the unmatched signal domain; it does not
silently apply an SDR LUT to HDR video.

Searches also returned unrelated HDR/VSR and old mpv issues. They were not
treated as evidence of this report's cause. All searches, including negatives,
are in the [20-call / 41-query ledger](fullscreen-web-searches.md).

## Exact binary evidence

The implementation uses the same Microsoft payload already matched to the
reporter's log:

| Item | Value |
| --- | --- |
| DWM DLL | `10.0.26100.4946`, x64 |
| SHA-256 | `D81EFC3A05B4D4CACD424706CF06A98400136F6660806666177436EA75F99427` |
| PDB GUID / DBI age | `BDEBC2AB-1C70-4383-D1FC-2FEB9A6FE683:1` |
| `m_dwOverlayTestMode` RVA | `0x414458` |
| `m_fDisableIndependentFlip` RVA | `0x414470` |

The latter is a four-byte integer in writable, zero-initialized `.data`.
Its address comes from Microsoft's public PDB, not a guessed relative offset.
Two runtime decisions independently read it:

| Reader | Instruction RVA | Bytes |
| --- | --- | --- |
| `COverlayContext::ApplyConfiguration` | `0x8eb51` | `83 3d 18 59 38 00 00` |
| `CDirectFlipInfo::CalcIndependentFlipSupport` | `0x29453e` | `83 3d 2b ff 17 00 00` |

Both instructions compare the integer with zero. A nonzero value takes the path
that denies Independent Flip. The existing DLL wrote only `OverlayTestMode=5`;
its exact profile did not bind this second control.

The first whole-section disassembly attempt stopped on undecodable bytes and
reported zero references. A subsequent displacement search followed by aligned
function disassembly identified the actual readers above. The zero-result scan
was not treated as evidence that the global was unused.

## Implementation

- The exact `26100.4946` profile adds `disableIndependentFlipRva`; other profiles
  default to zero and keep their existing presentation behavior.
- In the first fullscreen preview, the native DLL forced `OverlayTestMode=5` and
  the verified Independent Flip control to `1` to keep fullscreen composed.
  The reporter's follow-up shows that these controls alone were insufficient;
  the newer mode-5 result correction is described in the follow-up above.
- Each address must be aligned, inside the loaded DWM image, committed, writable,
  and outside guard/no-access pages. Writes use atomic operations and SEH guards.
- Previous values are captured once. Disable/unload restores only values still
  equal to those owned by this DLL, preserving intervening changes by DWM or
  another application. Failed hook startup rolls back acquired controls.
- The code never edits persistent registry keys, changes a player's settings,
  injects into media players, or creates a color-altering overlay window.
- Missing HDR/SDR assignments, protected surfaces, unknown display identities,
  and textures belonging to another D3D11 device still fail closed.

The in-memory controls are process-wide while the LUT DLL is active. Preventing
flip/overlay optimization can reduce VRR and power-saving benefits, including on
other displays in that DWM process. This is not a per-monitor driver-policy API.

## Limits and verification boundary

True exclusive fullscreen can own scanout and bypass DWM outright. This change
does not promise correction there. Protected content remains excluded. A player
that changes HDR mode still requires a matching LUT, and a resolution/driver
change that recreates devices remains a separate lifecycle concern.

The new control path is supported by online source research and the exact
Microsoft binary's runtime readers. The native DLL is compiled for Windows x64.
Fullscreen playback on the actual laptop remains to be confirmed; no automated
test suite or player/device execution on this Mac is claimed.

Expected native log progress is `Forced OverlayTestMode to 5`, followed by
`Forced DisableIndependentFlip to 1 for fullscreen windowed composition` and
the existing exact display/LUT match. A missing domain is now logged explicitly
as `domain=SDR` or `domain=HDR`.

For later confirmation, compare ordinary windowed, borderless fullscreen, and
exclusive presentation with each renderer. A PresentMon capture can distinguish
composition bypass from a signal-domain change. The baseline's verified status
must not be used as proof that this new fullscreen change has already passed.
