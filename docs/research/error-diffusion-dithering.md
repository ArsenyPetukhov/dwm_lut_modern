# Error diffusion dithering feasibility

Research date: 2026-10-29, Asia/Yangon.
Source checkout: `578dbf497c4579e4a122e0def8d876b962a46a3e` (v5.0.4).

## Conclusion

Error diffusion is feasible as an optional GPU processing mode. It is a separate
rendering feature, not a small replacement noise texture. It is not necessarily
visually better than the existing blue noise on moving video. Keep blue noise as
the default, begin with SDR output, and measure the new mode before offering it
as a desktop-wide quality upgrade. HDR needs its own quantization design.

This document records research and an implementation direction. Error diffusion
has not been implemented, compiled, benchmarked, or enabled in v5.0.4.

## Reporter follow-up

The reporter now considers fullscreen playback resolved through workarounds:

- MPC Video Renderer with **Discard** keeps DWM's LUT active in fullscreen.
- MPCVR **Flip** still loses it when the seekbar hides.
- madVR still exhibits the bypass; the reporter can activate madVR's own 3D LUT
  in fullscreen through its profile system.

That confirms the Discard workaround, not the native fullscreen policy for Flip
or universal HDR coverage. The renderer's exclusive-mode label does not prove
true exclusive scanout. No PresentMon capture was provided. The user's present
question is whether error diffusion could be added, not a request for another
fullscreen repair.

## Current implementation

Checked directly in `src/lutdwm/dllmain.cpp` and `src/lutdwm/noise.h`:

- `OrderedDither` at line 527 uses a static 64 by 64 noise texture. The inherited
  project describes it as blue noise; the function name does not imply a Bayer
  matrix.
- It selects between adjacent values on an **8-bit grid** (`255`), with a gamma
  2.2 approximation for the threshold decision. The same noise sample is used
  for the three channels.
- The SDR branch calls that function after the tetrahedral LUT transform.
- The HDR branch transforms scRGB through the PQ/BT.2100 LUT domain, converts
  back to scRGB, and returns without calling the explicit dither function.
- `BackBufferDomain` accepts both 8-bit UNORM and 10-bit UNORM surfaces as SDR.
  The dither step does not change from 255 to 1023 for the latter. This should be
  addressed before comparing expensive algorithms: forcing a 10-bit destination
  onto an 8-bit grid can discard useful precision.
- `RenderLUT` copies the screen buffer but draws only the supplied update
  rectangles. It is a pixel-shader path, compiled as Shader Model 5.0.

The quantization decision must account for the actual render target and color
space. Panel bit depth, display-link bit depth, and DWM surface format are
different facts; a monitor's advertised bit depth alone is insufficient.

## Evidence from existing implementations

### libplacebo / mpv

[libplacebo's public API](https://github.com/haasn/libplacebo/blob/275f7dea27924cdd34ab2e6c20e448b6bc4fab65/src/include/libplacebo/shaders/dithering.h#L106)
provides error diffusion with an explicit output depth, sampleable input and
storable output textures. It requires compute shaders, exposes a shared-memory
size query, and warns about its cost in performance-critical real-time use.
Blue noise is its ordinary default.

The [GPU implementation](https://github.com/haasn/libplacebo/blob/275f7dea27924cdd34ab2e6c20e448b6bc4fab65/src/shaders/dithering.c#L329)
uses one workgroup, shifted scan coordinates, a shared ring buffer, barriers,
and packed integer error accumulation. It is a usable algorithm reference,
not a drop-in HLSL pixel shader.

[mpv's manual](https://mpv.io/manual/master/#options-dither) documents
Sierra Lite, Floyd–Steinberg, Burkes and other kernels. Insufficient shared memory
causes a fallback to ordinary dithering. Kernel choice affects resource demand.

In [mpv issue 10691](https://github.com/mpv-player/mpv/issues/10691),
maintainer Niklas Haas describes moving-image flicker as a reason he personally
prefers other dithering methods and sees little difference at higher bit depths.
That is an expert's stated preference, not proof that error diffusion never
helps or that its artifacts are identical on every display.

### madVR

In the [author's tracker, issue 728](https://bugs.madshi.net/view.php?id=728),
madshi confirms that madVR's error diffusion uses DirectCompute. Users report
startup delays tied to error-diffusion settings on some AMD drivers; later
comments report differing outcomes after player/driver updates. These are
historical, configuration-specific reports. They neither identify the reporter's
RX 7700S as affected nor justify predicting the same failure for a new shader.

The reviewed material does not establish madVR's exact proprietary kernel or
provide reusable source for its Option 1/Option 2 algorithms. Do not advertise a
new implementation as identical to either option.

### Parallel algorithms and blue noise

The authors of [A Parallel Error Diffusion Implementation on a GPU (2011)](https://cv.ulichney.com/papers/2011-GPU-error-diffusion-EI.pdf)
demonstrate block-interlaced pinwheel processing. It creates more parallelism
than a global wavefront, with performance/quality tradeoffs. Its printing-oriented
measurements are not a prediction for this desktop compositor or modern laptop.

[NVIDIA's spatiotemporal blue-noise research](https://research.nvidia.com/publication/2022-07_spatiotemporal-blue-noise-masks)
is an alternative reference for improved noise distribution. Its temporal-filtering
benefits do not prove that animating a desktop dither will look better. A pattern
that changes every frame can conflict with partial redraws or display behavior.

## A concrete D3D11 limit

Microsoft specifies [8192 32-bit shared-memory elements for cs_5_0](https://learn.microsoft.com/en-us/windows/win32/direct3dhlsl/registers---cs-5-0),
which is 32768 bytes per workgroup. It also documents
[group-scoped barriers](https://learn.microsoft.com/en-us/windows/win32/direct3dhlsl/groupmemorybarrierwithgroupsync).
A group barrier does not synchronize all workgroups in a dispatch.

The following values are **calculations from the pinned libplacebo source**,
not timings or GPU executions. Its ring-buffer requirement is:

```text
(image_height + 2) * shifted_column_count * 4 bytes
```

The kernel coefficients give 3 columns for Sierra Lite, 4 for Floyd–Steinberg,
and 6 for Burkes. See the
[kernel definitions](https://github.com/haasn/libplacebo/blob/275f7dea27924cdd34ab2e6c20e448b6bc4fab65/src/dither.c#L212).

| Output height | Sierra Lite | Floyd–Steinberg | Burkes |
| --- | ---: | ---: | ---: |
| 1080 | 12984 B | 17312 B | 25968 B |
| 2160 (landscape 4K) | 25944 B | **34592 B** | **51888 B** |
| 3840 (portrait 4K) | **46104 B** | **61472 B** | **92208 B** |

Bold entries exceed cs_5_0's 32768-byte budget. Sierra Lite fits landscape 4K
in this formulation; fitting does not establish speed. A direct HLSL translation
of the existing whole-image algorithm therefore needs explicit fallback checks.
Another scheduling or storage design can avoid these limits, with different costs.

## DWM-specific implementation constraints

These are engineering deductions from the local renderer and API contracts:

1. **Apply diffusion after the LUT, before integer quantization.** Writing the
   LUT result to an 8-bit intermediate first would already lose the fractional
   information the new dither needs. Use a higher-precision intermediate or fuse
   LUT evaluation into the compute step.
2. **Preserve incremental redraw behavior.** Error diffusion depends on earlier
   pixels. Restarting it independently for changing dirty rectangles changes
   edge conditions and can produce unstable seams. Full-frame processing needs
   a coherent uncorrected source; it must not apply the LUT twice to retained
   regions or silently modify pixels outside DWM's update contract. Microsoft's
   [dirty-rectangle documentation](https://learn.microsoft.com/en-us/windows/win32/direct3ddxgi/dxgi-1-2-presentation-improvements)
   explains the need for coherent retained content. Exact private-hook behavior
   still needs Windows verification.
3. **Avoid unscheduled cross-group waits.** A naive GPU-wide spin barrier is not
   a substitute for a supported scheduling design. Use a bounded group method,
   separate dispatches, or a justified block algorithm.
4. **Own new GPU assets correctly.** Compute shaders, intermediate textures,
   views and any error storage must follow device replacement/Disable cleanup
   and DWM graphics-state restoration already required by the renderer.
5. **Keep fallback independent of a watchdog.** Unsupported resources, formats,
   or memory requirements should select blue noise before dispatch. Async GPU
   timing can inform a budget; a hung dispatch cannot be repaired by a CPU timer.
6. **Do not infer desktop performance from movie playback.** Desktop resolution,
   refresh rate, number of displays and other GPU work affect the cost. Total
   frame budgets are 16.67 ms at 60 Hz, 8.33 ms at 120 Hz, and 4.17 ms at 240 Hz;
   dithering gets only part of that budget. No millisecond cost is measured here.

## HDR is a separate decision

[Microsoft's Advanced Color guide](https://learn.microsoft.com/en-us/windows/win32/direct3darticles/high-dynamic-range)
describes FP16/scRGB composition and subsequent display conversion. Our HDR
branch writes that floating-point domain rather than the final integer link
encoding.

Inference: copying the SDR 1/255 quantizer into HDR is incorrect. A 1/1023 PQ
quantizer added before conversion back to scRGB also cannot be assumed to match
the final quantization after Windows' display processing. HDR could be researched
later, but needs the target encoding, transform order and measured output behavior
to be established. Preserve HDR precision while that remains unresolved.

This differs from the earlier statement that the fullscreen LUT fix targets both
SDR and HDR. Existing HDR LUT support does not establish HDR error-diffusion support.

## Recommended implementation direction

- First correct format-aware SDR precision: use the actual supported integer
  target grid instead of forcing all SDR surfaces to 8 bits.
- Preserve blue noise as the default and fallback.
- Prototype an **optional SDR error-diffusion mode** using a bounded compute path.
  Sierra Lite is a practical initial reference; compare it with other kernels
  where resources permit. Do not call a tile approximation full-frame
  Floyd–Steinberg.
- Resolve dirty-region behavior before integrating the mode into normal DWM
  rendering. Inspect gradients, neutral ramps, text, moving video, retained
  regions, output bias and temporal stability, alongside GPU frame time.
- Defer HDR error diffusion until its output-domain assumptions are established.

The user requested research. No runtime code, build or public release was changed
for this question, and no automated test suite was run.

## Research record

The pass used ten fresh search calls (31 query strings), primary documentation,
pinned source reads and issue comments. Main topics: GPU parallel error diffusion;
madVR implementation and driver history; mpv/libplacebo kernels and resource
limits; D3D11 compute synchronization; blue-noise alternatives; HDR composition;
dirty rectangles; and the existing DWM LUT shader. Some searches returned no
relevant result; Doom9 was blocked for direct browsing. Secondary settings guides
and anecdotes were not used to claim a quality ranking or measured performance.

Related earlier research: `fullscreen-hidden-seekbar.md`,
`fullscreen-logic-review.md`, `fullscreen-windowed.md`,
`fullscreen-web-searches.md`. The current reporter follow-up supersedes their
earlier pending-workaround status.
