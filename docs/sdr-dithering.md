# SDR dithering

Choose the mode in **Settings → SDR dithering**. Existing settings use **Blue
noise**, which remains the default. Changing modes reapplies active LUTs.

## Modes

| Mode | Behavior | Tradeoff |
| --- | --- | --- |
| Blue noise (default) | Stable noise pattern with independent pixel processing | Low GPU cost; recommended for normal desktop and video playback |
| Error diffusion (experimental) | Sierra Lite diffusion within fixed 32 × 32 blocks | More GPU work and memory; possible block edges or shimmer with motion and partial redraws |

Both modes use the SDR screen buffer's precision: 255 intervals for an 8-bit
buffer or 1023 for a 10-bit buffer. This does not change the display link's bit
depth and does not turn an 8-bit screen into a 10-bit screen. sRGB render targets
are quantized in encoded sRGB and converted back for the render target's encode.

Error diffusion is a tiled approximation, not full-frame Floyd–Steinberg or a
copy of madVR's proprietary dithering modes. Its errors reset at tile and dirty
rectangle boundaries. It processes only updated pixels so retained desktop
content is not passed through the LUT twice. The changed region's edges can
therefore alter the noise pattern on later redraws.

If compute shaders, storage resources, or shader creation are unavailable, the
native DLL uses blue noise. The native log records the requested mode, selected
precision, activation or fallback. If playback stutters, choose Blue noise;
automatic fallback does not measure or guarantee playback speed.

## 4K

The tile's 12 KiB of shared error storage is independent of monitor dimensions.
The implementation accepts 4K-sized buffers in landscape or portrait without a
whole-image shared-memory allocation. Error diffusion also allocates an FP16
intermediate: about 63 MiB at 3840 × 2160, potentially more if a larger buffer
capacity was previously required. Processing cost grows with updated pixel count
and refresh rate. 4K at 120/144/240 Hz has not been benchmarked on the reporter's
GPU; no smooth-playback guarantee is made.

## HDR

These options affect **SDR only**. HDR keeps the floating-point scRGB LUT path
and requires a matching HDR LUT. No additional HDR quantization or error
diffusion is applied. DWM's floating-point surface is not the final integer
display-link encoding.

## DirectX 12 and fullscreen

The app works on DWM's composed output. DirectX 12 applications can be corrected
when Windows composites them, just like applications using DirectX 11 or other
rendering APIs. The hook uses D3D11 resources inside DWM; it does not need to
inject a D3D12 shader into each game.

DirectFlip, Independent Flip and hardware overlays can bypass the composed
buffer and therefore the LUT. Selecting either dithering mode does not remove
that boundary. The reported MPC-HC workaround remains **MPC Video Renderer →
Swap effect → Discard** for SDR; Flip and madVR still bypass the LUT on the
affected laptop. A fullscreen label does not prove which scanout path is used.

References: [Microsoft's flip-model guide](https://learn.microsoft.com/en-us/windows/win32/direct3ddxgi/for-best-performance--use-dxgi-flip-model),
[D3D12 swap chains](https://learn.microsoft.com/en-us/windows/win32/direct3d12/swap-chains),
[dithering research](research/error-diffusion-dithering.md).
