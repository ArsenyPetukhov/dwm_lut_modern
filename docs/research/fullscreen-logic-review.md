# Fullscreen logic review

Date: 2026-10-26 (Asia/Yangon). This review challenges the first fullscreen
preview rather than treating its compilation as proof of correct playback.

**Update:** the reporter subsequently confirmed that v5.0.3 still loses the LUT
when MPC-HC's seekbar hides. The assumption in section 3 that the native false
result was sufficient is superseded by the
[hidden-seekbar investigation](fullscreen-hidden-seekbar.md). Resource/state
corrections below remain separate from that failed fullscreen policy.

## What was missed

### 1. Screen-buffer references survived transitions

The renderer kept up to sixteen render-target views. Each view indirectly kept
its backbuffer alive, and the immediate context retained additional bindings.
[Microsoft requires those references to be released before ResizeBuffers](https://learn.microsoft.com/en-us/windows/win32/api/dxgi/nf-dxgi-idxgiswapchain-resizebuffers).
Fullscreen/mode transitions can therefore fail even with Independent Flip blocked.

The copy texture was also rebuilt only when dimensions grew. A change from one
pixel format to another at the same size could reuse an incompatible resource.
[CopySubresourceRegion requires compatible resource formats](https://learn.microsoft.com/en-us/windows/win32/api/d3d11/nf-d3d11-id3d11devicecontext-copysubresourceregion).

Correction: render-target views are scoped to a frame, copy resources are replaced
transactionally when format/sample requirements change, and the draw runs in its
own [device context state](https://learn.microsoft.com/en-us/windows/win32/api/d3d11_1/nf-d3d11_1-id3d11devicecontext1-swapdevicecontextstate).
That state is cleared before restoring DWM's complete previous state, so the
saved LUT state does not retain a screen buffer or overwrite DWM's bindings.
The context-state creation flags match the original device's single-thread flag.

### 2. SDR/HDR settings were cached against the wrong object

There is one shared constant buffer but there were two last-value caches, one
per signal domain. After SDR → HDR → SDR, the SDR cache could say "unchanged"
while the actual buffer still contained HDR settings. Different LUT sizes could
produce the same error across outputs.

Correction: each LUT owns an immutable sixteen-byte settings buffer containing
its size and signal domain. Rendering binds the selected LUT's buffer; there is
no shared contents cache or per-frame settings upload. This is separate from
the rule that an HDR output requires an HDR LUT.

### 3. The overlay hook did not preserve the native calling behavior

The [maintained related fork](https://github.com/GianSegugio/dwm_lut_is_back/blob/master/DOCUMENTATION.md)
describes a register-preserving hook for newer payloads. Checking the exact
Microsoft `26100.4946` DLL showed that this issue also applies here, regardless
of the Windows marketing version:

- `COverlayContext::OverlaysEnabled`, RVA `0x33a00`, returns false directly when
  `OverlayTestMode == 5`.
- `OverlayPlaneInfo::IsDFlipOnMPO`, RVA `0x282f80`, puts its object in `R8`, then
  calls that leaf function at `0x282f94` and reads `R8` at `0x282f9d` afterwards.
- The caller also keeps `DL` live across that call. A normal C++ detour may
  overwrite these volatile registers, even when its parameters/return type match.

v5.0.3 correction: the exact profile kept the native leaf untouched to avoid the
unsafe C++ detour. Its false result was not sufficient for the reported fullscreen
playback. v5.0.4 changes the exact mode-5 return instruction while retaining the
native function and register behavior; see the follow-up linked above.

### 4. GPU-device replacement had no recovery path

The old pool retained its first device until DLL unload and freed the CPU LUT
immediately after uploading it. That left no way to rebuild resources after
the staging folder had been deleted. Fullscreen resolution/refresh changes can
cause DWM to replace a device; the related fork documents an additional device
leak-check failure when external resources outlive removal.

The exact binary supplies a bounded interception point:

| Item | Evidence |
| --- | --- |
| `CDeviceManager::DeleteUnusedDevices` | RVA `0x1cb30` |
| Actual `vector<DeviceInfo>::erase` | RVA `0x1b204` |
| Sole direct call to that erase | RVA `0x1cb77` |
| Erase ABI | Three pointer arguments: vector, hidden iterator-result storage, position |
| Position | First pointer is the private `CD3DDevice` being removed |
| Overlay's device getter | RVA `0x1f08e0`, slot 5 on both allowed vtables, reads `this+0x28` |

Correction: the exact profile hooks the real erase, not the per-frame unused-device
scan. It matches the removed private device against the active LUT pool, releases
its GPU assets before final device destruction, and retains CPU LUT data for
rebuilding on the next valid frame. It clears the host pipeline only for that
device at destruction. Frame rendering and removal share a lock; temporary
backbuffer references are acquired and released inside it. Stale active-context
entries are cleared and their table is synchronized.

Partial initialization now releases its resources, failed shader creation is
checked, and temporary COM objects are released on exception paths.

## Performance

The frame copy and LUT shader already existed during ordinary composed playback.
Fullscreen support keeps that processing active when the video fills the screen.
That can increase GPU work compared with direct scanout; it is the main reason
this feature cannot promise unchanged frame pacing or power usage.

Normal frames reuse the textures, shaders and per-LUT settings buffers. No
Map/Unmap is needed for those settings. Compiled HLSL bytecode is
retained across device replacement, so recovery does not rerun the HLSL compiler.
The driver still needs to create device-specific shader objects and upload LUT
textures for a new device; a display-mode change can therefore produce a hitch.

Per-frame render-target views are small driver objects, not new screen-sized
images. Creating/releasing them and swapping graphics state adds CPU/driver work.
It is a deliberate lifetime-correctness tradeoff: retaining them without a
reliable pre-resize release point can block the transition. No timings have been
measured on the reported laptop, so this review does not label that cost zero
or guarantee smooth playback.

## Things this does not prove or solve

- The affected laptop has not supplied a fullscreen capture for these changes.
  These code defects are real; they are not proof of the reported symptom's cause.
- True exclusive fullscreen and protected content remain outside this promise.
- A renderer switching Windows into HDR needs a matching HDR LUT. The
  [MPC-HC maintainer FAQ](https://github.com/clsid2/mpc-hc/issues/2869) confirms
  that fullscreen can trigger such a switch.
- The pool still handles one active Direct3D device at a time. A different live
  device is deliberately skipped; this is not full simultaneous multi-GPU support.
- Forcing composition has system-dependent power/VRR/pacing costs. DesktopLUT's
  [HAGS investigation](https://github.com/sup3rflyer/DesktopLUT/blob/main/HANDOFF_HAGS_FLIPQUEUE_2026-09-06.md)
  corrected its initial hook diagnosis and attributes its observed mpv pacing
  issue to composed playback under HAGS. That is a reported risk, not evidence
  that the same problem exists on this AMD laptop.
- The private ABI/removal addresses above are enabled only for the matching
  Microsoft payload. Other profiles are not covered by this exact-binary proof.
- Native DLL unload and graphics-driver behavior still require real Windows
  confirmation; source review cannot provide a universal reliability guarantee.

No automated test suite or media-player execution was run on this Mac for this
review. The corrected native DLL was cross-compiled for Windows x64. Compiler
success and binary inspection are kept separate from laptop playback acceptance.

## Fresh online searches

Ten web-search calls (twenty query strings), plus direct primary-source reads:

1. DesktopLUT `DisableIndependentFlip`/HAGS and `OverlaysEnabled` combinations.
2. Microsoft Independent Flip/MPO composition; related fork's register-preserving hook.
3. MPC-HC renderer FAQ; MPC Video Renderer fullscreen/display-mode handling.
4. Microsoft ResizeBuffers reference lifetime and copy-format requirements.
5. Related fork's leak checker/removal handling; DesktopLUT hardware flip-queue pacing.
6. DesktopLUT fallback behavior and related fork's fullscreen teardown.
7. Microsoft CreateDeviceContextState and SwapDeviceContextState contracts.
8. Microsoft OMSetRenderTargets and PSSetShaderResources reference ownership.
9. Related fork's exact removal timing; DesktopLUT HAGS configuration follow-up.
10. Microsoft constant-buffer update frequency and compiled shader bytecode reuse.

Empty results and unrelated renderer/HDR anecdotes were not used to establish
addresses or a confirmed cause. The implementation decisions use Microsoft API
contracts and the exact DLL/PDB already matched to the reporter's log.
