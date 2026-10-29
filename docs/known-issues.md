# Known Issues

- DWM internals are private and can change at any Windows update.
- Canary/Future Platforms support is experimental.
- The exact `26100.4946` profile routes by display adapter and target identity. Other private
  DWM paths still use exact desktop positions; ambiguous matches apply no LUT.
- Exclusive fullscreen or protected content may bypass DWM.
- Fullscreen windowed support is specific to the matching `26100.4946` DWM payload.
- The v5.0.3 fullscreen preview did not keep MPC-HC corrected after its seekbar hid on the reported laptop. v5.0.4 changes that policy; see [fullscreen playback](fullscreen-playback.md) for the SDR fallback.
- Keeping fullscreen video composed may reduce VRR/power-saving benefits or affect playback pacing on some systems.
- A media renderer that switches Windows to HDR requires a matching HDR LUT. An SDR LUT is deliberately not applied to HDR output.
- HDR correctness depends on LUT domain and Windows HDR state.
- SDR error diffusion is an optional tiled mode. It can show block boundaries or shimmer and costs more GPU time and memory; blue noise remains the default. See [SDR dithering](sdr-dithering.md).
- The reporter confirmed MPC Video Renderer's Discard workaround. Flip and madVR still bypass the LUT with the seekbar hidden; dithering modes do not change this.
- The injector uses privileged process access and may trigger security software.
- Release builds are unsigned.
- Compiled PDB profiles marked `built`, `local-built`, or `experimental` are not live-validated.
