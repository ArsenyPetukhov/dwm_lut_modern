# Known Issues

- DWM internals are private and can change at any Windows update.
- Canary/Future Platforms support is experimental.
- Multi-monitor persistence uses display identity, but the private DWM context is still routed by
  exact desktop position. Ambiguous/non-exact matches now fail closed and apply no LUT. True native
  DWM-context-to-display-identity mapping remains future work.
- Exclusive fullscreen or protected content may bypass DWM.
- Fullscreen windowed playback on `26100.4946` has a dedicated composition policy. Confirmation of that new path on the reported laptop is pending; the earlier normal-windowed fix is confirmed working.
- A media renderer that switches Windows to HDR requires a matching HDR LUT. An SDR LUT is deliberately not applied to HDR output.
- HDR correctness depends on LUT domain and Windows HDR state.
- The injector uses privileged process access and may trigger security software.
- Release builds are unsigned.
- Compiled PDB profiles marked `built`, `local-built`, or `experimental` are not live-validated.
