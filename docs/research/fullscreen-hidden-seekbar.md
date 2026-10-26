# LUT disappears when MPC-HC hides its seekbar

Date: 2026-10-26, Asia/Yangon.

## New evidence and correction to the earlier conclusion

The user reports that v5.0.3 still loses the LUT in fullscreen, but the LUT
returns while MPC-HC's seekbar is visible. The original v5.0.1 normal-windowed
repair remains confirmed. The fullscreen request is **not runtime resolved**.

The earlier review correctly identified unsafe register handling and resource
lifetimes. It incorrectly treated the native mode-5 **false** result as sufficient
to keep fullscreen video composed. That assumption is contradicted by
[DesktopLUT's corrected investigation](https://github.com/sup3rflyer/DesktopLUT/blob/ce843651876c65255b752e88d07fce896b54282b/HANDOFF_HAGS_FLIPQUEUE_2026-09-06.md)
and its
[current native policy](https://github.com/sup3rflyer/DesktopLUT/blob/ce843651876c65255b752e88d07fce896b54282b/dwm_hook/dllmain.cpp#L1670).
The investigation's early sections describe `4od` as a proposed solution; its
updated opening and later findings supersede that: leaving OverlaysEnabled
original can let fullscreen clients bypass the LUT. Production uses the true
result with Independent Flip disabled. This is evidence from a related build,
not playback proof for this AMD laptop.

## Ranked explanations

1. **The remaining native policy permits a bypass.** Prediction: changing the
   mode-5 result to true while keeping `DisableIndependentFlip=1` makes video
   remain corrected after the seekbar hides. This is the implemented correction.
2. **The renderer enters true exclusive mode after its controls hide.** Prediction:
   disabling exclusive presentation and selecting the SDR Discard path keeps it
   composed. A DWM hook cannot correct genuine exclusive scanout.
3. **Fullscreen changes the HDR/output path.** Prediction: the output switches
   SDR/HDR or HDR metadata while the assigned LUT no longer matches. Stabilizing
   the output and using a matching LUT addresses that separate case.

These predictions have not been exercised on the reporter's laptop. They avoid
equating a seekbar correlation with proof of a particular DXGI presentation mode.

## What primary sources establish

- [Microsoft's flip-model documentation](https://learn.microsoft.com/en-us/windows/win32/direct3ddxgi/for-best-performance--use-dxgi-flip-model)
  says fullscreen-sized windowed buffers can bypass composition. Other visible
  content can bring composition back, use reverse composition, or remain on MPO.
  Therefore a seekbar returning the LUT is strong evidence of a presentation
  path change, but is not an exact PresentMon classification.
- [Lauralex's original behavior](https://github.com/lauralex/dwm_lut) explicitly
  suppresses DirectFlip/MPO and excludes exclusive fullscreen. Matching its goal
  on a new private DWM build requires more than copying its old hook signatures.
- [DesktopLUT](https://github.com/sup3rflyer/DesktopLUT/blob/ce843651876c65255b752e88d07fce896b54282b/dwm_hook/dllmain.cpp#L1676)
  uses the true leaf result with its Independent Flip control successfully acquired;
  leaving the leaf original is explicitly its fullscreen bypass option.
- [The related fork](https://github.com/GianSegugio/dwm_lut_is_back/blob/d9680e036953adfa28f08507cfa6867aa105f7f7/DOCUMENTATION.md)
  documents a composition-blocker window, but also its full-screen color tint
  and topmost-window costs. This patch does not add that overlay.
- [MPC Video Renderer's settings](https://github.com/clsid2/MPCVideoRenderer/blob/d6674bbc21f8eb777c16ccbb302cc3c986c87e5f/Source/PropPage.cpp#L285)
  expose Discard and Flip. Its
  [actual swap-chain creation](https://github.com/clsid2/MPCVideoRenderer/blob/d6674bbc21f8eb777c16ccbb302cc3c986c87e5f/Source/DX11VideoProcessor.cpp#L1845)
  uses `DXGI_SWAP_EFFECT_DISCARD` for SDR Discard, but forces Flip for HDR output.
- [Microsoft's swap-effect contract](https://learn.microsoft.com/en-us/windows/win32/api/dxgi/ne-dxgi-dxgi_swap_effect)
  distinguishes Discard's copying/redirection path from Flip and explains HDR's
  need for Flip. Discard is an SDR workaround, not a universal HDR solution.
- [Windows' per-app graphics settings](https://support.microsoft.com/en-us/windows/optimizations-for-windowed-games-in-windows-11-3f006843-2c7e-4ed0-9a5e-f9389e535952)
  can disable promotion of legacy windowed presentation to Flip. Auto HDR can
  turn the optimization back on; restart the player after changing the setting.
- A superficially identical
  [MPCVR seekbar report](https://github.com/Aleksoid1978/VideoRenderer/issues/177)
  concerns Dolby Vision versus HDR10 metadata. Maintainer comments distinguish
  renderer output from Windows' desktop output. It is a useful counterexample:
  the same visible trigger does not prove the same LUT bug.
- [The MPCVR FAQ](https://github.com/clsid2/mpc-hc/issues/2869) documents fullscreen
  HDR switching and renderer diagnostics. Switching to an SDR LUT on HDR output
  is not an acceptable fix.

## Exact binary and implementation

The inspected Microsoft `dwmcore.dll` remains the reporter's matched x64
`26100.4946` payload, SHA-256
`d81efc3a05b4d4cacd424706cf06a98400136f6660806666177436ea75f99427`.

| Item | Exact value |
| --- | --- |
| `COverlayContext::OverlaysEnabled` | RVA `0x33a00` |
| Entire leaf bytes | `833d510a3e00057409837928010f97c0c3cc32c0c3` |
| Mode comparison | RIP-relative reference to `m_dwOverlayTestMode` at `0x414458` |
| Mode-5 return | `xor al,al; ret` at `0x33a12` |
| Replacement instruction | `mov al,1` (`b0 01`), followed by the existing `ret` |
| Independent Flip control | `0x414470`, acquired as `1` before the code change |

Only this exact profile opts into the new strategy. Runtime checks require the
whole original leaf to match, both global controls to have been acquired, the
expected current values, an executable committed image range, and aligned
two-byte storage. Unknown code or missing controls rejects activation and rolls
back the acquired settings.

The update exchanges one aligned two-byte instruction, preserving the native
branch layout, return instruction, other modes, and registers such as R8/DL.
It does not route the call through C++ or overwrite a multi-instruction prologue.
[InterlockedCompareExchange16](https://learn.microsoft.com/en-us/windows/win32/api/winnt/nf-winnt-interlockedcompareexchange16)
supplies the aligned atomic exchange; memory protection is restored and
[FlushInstructionCache](https://learn.microsoft.com/en-us/windows/win32/api/processthreadsapi/nf-processthreadsapi-flushinstructioncache)
is called after modification. The protection snapshot is retained for rollback
even if the first attempt to restore page protection fails.

Disable restores the instruction before releasing the Independent Flip control.
Restoration compares against the exact instruction owned by this DLL, so it does
not overwrite a later instruction change from another component. Hook-startup
failure rolls back the policy. Startup logs distinguish successful activation
from a rejected policy.

There is no new frame loop, timer, settings upload, overlay window, player
injection, or persistent registry edit. Policy changes occur on attach/detach.
Forcing composed fullscreen itself still costs GPU work and can affect VRR,
power, or pacing. The HAGS history does not establish a pacing guarantee.

## Delivered workaround and proof boundary

[Fullscreen playback instructions](../fullscreen-playback.md) give an immediate
SDR fallback: select MPC Video Renderer, retain D3D11, use Discard, disable
exclusive fullscreen, and restart the player. They explain HDR overriding that
choice and Windows' optional promotion of older presentation paths.

The native Windows x64 DLL is cross-compiled on macOS. As requested, no automated
test suite was run. Binary bytes and upstream behavior were inspected; the actual
MPC-HC reproduction cannot be executed here. v5.0.4 must not be described as
confirmed on the affected laptop until playback with the seekbar hidden succeeds.

## Search record

Ten fresh web-search calls, 38 query strings, plus GitHub API/source reads,
Microsoft documentation, the original reporter issue/log identity, and the three
earlier fullscreen research documents:

| Call | Queries / purpose |
| --- | --- |
| 1 | MPC-HC fullscreen seekbar DWM LUT disappears; DesktopLUT 4ode/4od; AMD fullscreen MPO |
| 2 | DesktopLUT true/original policy; related fork overlay ABI; Microsoft visible desktop content and Independent Flip |
| 3 | Microsoft reverse composition; MPC-HC exclusive D3D11; MPCVR Flip/LUT; original LUT seekbar reports |
| 4 | MPCVR Discard/Flip settings; renderer known issues; Microsoft Discard versus bitblt; DesktopLUT full-monitor true |
| 5 | Lauralex fullscreen suppression; original OverlaysEnabled; atomic exchange/cache contracts; DesktopLUT disappearing LUT |
| 6 | Executable-code cache flushing; MPC-HC hidden controls; MPCVR HDR overriding Discard; Lauralex 24H2 reports |
| 7 | Corrected DesktopLUT production policy; MPCVR HDR/Discard; Windows legacy-presentation promotion; player exclusive setting |
| 8 | Official Windows windowed optimizations; current MPC-HC renderer selection; original MPO reports; DesktopLUT original-leaf bypass |
| 9 | MPC-HC seekbar/exclusive transitions; MPCVR seekbar report; Microsoft fullscreen/flip; per-app optimization instructions |
| 10 | MPCVR issue 177 follow-up; fullscreen seekbar/LUT reports; DV versus HDR counterexample; AMD fullscreen Discard reports |

Broad web results included irrelevant reports and mirrors; those were not used to
establish native addresses. The original MPC-HC FAQ fetch failed certificate
validation; its search excerpt was not used as proof of the current renderer.
Native policy conclusions rely on pinned source and the exact Microsoft binary.
