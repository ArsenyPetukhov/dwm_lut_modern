# Product
<!-- impeccable:product-schema v1 -->

## Platform
Native Windows desktop application. WPF on .NET 8, with a C++ D3D11 DLL inside DWM.

## Stack
Keep WPF, WPF-UI controls and the native DWM hook. React liquid-glass components
are design references; this task does not migrate the application to a web view.

## Users
People applying monitor-calibration LUTs, including media-player users and
developers. Explanations must be readable by a junior developer and precise
enough for technical users.

## Product Purpose
Apply per-display SDR/HDR 3D LUTs to Windows' composed desktop output. Let users
assign LUTs, enable/disable correction, choose SDR dithering, and inspect support.

## Capabilities and Constraints
- Separate SDR and HDR LUT assignments. HDR requires its matching LUT.
- Blue noise is the default SDR dither; tiled Sierra Lite is optional.
- Dithering follows the 8/10-bit SDR buffer format, not panel marketing data.
- Direct scanout can bypass correction. MPCVR Discard is confirmed as an SDR
  workaround; madVR Flip remains affected and can use its own fullscreen LUT.
- Current hardware confirmation is narrow. Compilation is not playback evidence.
- Preserve disabled intent, atomic state saving, monitor identity and recovery.

## Brand Commitments
User-selected direction: Apple-like liquid-glass restraint, white in light mode
and dark gray in dark mode. No purple/blue ambient glows or slogans. A generated
monitor/LUT icon replaces the palette mark. Keep the product name DWM LUT.

## Product Principles
Use literal page names and actions. Explain only what helps a decision; put
longer technical material in Documentation. Never claim a workaround is a native
fullscreen fix or promise unmeasured 4K performance.

## Accessibility & Inclusion
Keyboard operation, visible focus, Windows reduced-motion settings, clear labels,
stable navigation and complete UI/help coverage in the existing ten languages:
en, ru, es, fr, de, pt, it, zh, ja, ko.

## Evidence on Hand
Windows captures for v5.0.5 under Research/2026-10-29-dithering/build-f870854,
plus source and linked research in docs/research. DWM injection/playback cannot
be exercised on this Mac; Windows CI can compile and render the application.
