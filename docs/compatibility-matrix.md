# Compatibility Matrix

This matrix tracks build artifacts and expected validation state. A "Built" status means the profile is compiled into `dwm_lut.dll`; it does not mean HDR or MPO has been fully validated on physical hardware.

| Date | Windows version | Build | Channel | DWM payload | Status | Notes |
| --- | --- | ---: | --- | --- | --- | --- |
| 2026-06-29 | Windows 11 24H2 | 26100.8737 | Stable preview | Not exact-profiled | Fallback only | Uses legacy 24H2 byte-signature path, not an exact PDB profile. |
| 2026-06-29 | Windows 11 | 26200.8655 | Current machine | Local `dwmcore.dll` | Built | Current installed profile. |
| 2026-06-29 | Windows 11 25H2 | 26200.8737 | Stable preview | Unique | Built, needs live validation | KB5095093. |
| 2026-06-29 | Windows 11 25H2 | 26220.8754 | Beta | Unique | Built | Alias with 26300.8758. |
| 2026-06-29 | Windows 11 26H2 | 26300.8758 | Experimental | Same as 26220.8754 | Built as alias | Same Win4 feature payload. |
| 2026-06-29 | Windows 11 26H1 | 28000.2340 | Published | Unique | Built | Same RVAs as 28020/28120, distinct PDB identity. |
| 2026-06-29 | Windows 11 26H1 | 28020.2366 | Beta | Unique | Built | Has DirectFlip and CSwapChainRealization symbols. |
| 2026-06-29 | Windows 11 26H1 | 28120.2374 | Experimental | Same as 28020.2366 | Built as alias | Same Win4 feature payload. |
| 2026-06-29 | Future Platforms | 29617.1000 | Canary | Unique | Experimental built | Needs VM/secondary install validation. |

ARM64 rows are discoverable through UUP for the same public build set, but are not built yet. ARM64 requires a separate native DLL and separate ARM64 `dwmcore.dll` profiles; see [build-catalog.md](build-catalog.md) and [arm64-roadmap.md](arm64-roadmap.md).

## Source Links

- [Windows 11 release information](https://learn.microsoft.com/en-us/windows/release-health/windows11-release-information)
- [KB5095093 release notes](https://support.microsoft.com/en-us/topic/june-23-2026-kb5095093-os-builds-26200-8737-and-26100-8737-preview-0e2a20f2-cf9e-46f8-9f08-e6996220882d)
- [Windows Insider build announcement, 2026-06-26](https://blogs.windows.com/windows-insider/2026/06/26/announcing-new-builds-for-26-june-2026-retail-launch-of-new-wip-improvements/)
- [Future Platforms build 29617.1000](https://learn.microsoft.com/en-us/windows-insider/release-notes/experimental-future-platforms/preview-build-29617-1000)

## Status Definitions

- **Built**: DWM PDB identity and RVAs are compiled into the DLL.
- **Pass**: LUT applies and survives normal desktop usage on a live machine.
- **Partial**: LUT applies but has known limitations.
- **Fail**: LUT does not apply or crashes DWM.
- **Experimental**: Best-effort profile for Insider/Canary builds.
