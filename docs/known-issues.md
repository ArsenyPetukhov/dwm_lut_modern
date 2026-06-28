# Known Issues

- DWM internals are private and can change at any Windows update.
- Canary/Future Platforms support is experimental.
- Multi-monitor persistence and diagnostics are improved, but DWM context matching is still coordinate-backed until DWM display identity mapping is implemented.
- Exclusive fullscreen or protected content may bypass DWM.
- HDR correctness depends on LUT domain and Windows HDR state.
- The injector uses privileged process access and may trigger security software.
- Release builds are unsigned.
