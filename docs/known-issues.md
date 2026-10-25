# Known Issues

- DWM internals are private and can change at any Windows update.
- Canary/Future Platforms support is experimental.
- The exact `26100.4946` profile routes by display adapter and target identity. Other private
  DWM paths still use exact desktop positions; ambiguous matches apply no LUT.
- Exclusive fullscreen or protected content may bypass DWM.
- Fullscreen windowed support is specific to the matching `26100.4946` DWM payload.
- Keeping fullscreen video composed may reduce VRR/power-saving benefits or affect playback pacing on some systems.
- A media renderer that switches Windows to HDR requires a matching HDR LUT. An SDR LUT is deliberately not applied to HDR output.
- HDR correctness depends on LUT domain and Windows HDR state.
- The injector uses privileged process access and may trigger security software.
- Release builds are unsigned.
- Compiled PDB profiles marked `built`, `local-built`, or `experimental` are not live-validated.
