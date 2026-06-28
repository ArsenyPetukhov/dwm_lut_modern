# Roadmap

## Immediate

- Validate identity LUT on current machine.
- Validate obvious SDR and HDR test LUTs.
- Collect `%SystemRoot%\Temp\dwm_lut.log` from 25H2, 26H1, and Canary VMs.
- Replace private vtable ordinal texture retrieval if live logs prove it changed.

## Near Term

- Add symbol-driven helpers for `CSwapChainRealization::GetDeviceTexture`.
- Add DWM display identity logging from public-symbol-derived object paths.
- Replace coordinate-based multi-monitor matching with display identity matching.
- Add a diagnostic mode that logs object/vtable RVAs without applying a LUT.

## Long Term

- Generate `DwmProfiles.generated.h` from checked-in UUP/PDB metadata.
- Add signed release artifacts or at least reproducible checksums.
- Split native monolith into focused modules after behavior is stable.
- Build a safe VM-first Canary validation guide.
