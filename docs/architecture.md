# Architecture

## Components

- `src/lutdwm`: native DLL loaded into `dwm.exe`.
- `src/DwmLutGUI`: WPF GUI that stages LUTs and injects/uninjects the DLL.
- `artifacts/profiles`: generated or curated DWM profile data.
- `artifacts/packages`: per-build portable packages.
- `docs/forensics`: research reports and compatibility analysis.

## Runtime Flow

1. GUI copies `dwm_lut.dll` to `%SystemRoot%\Temp\dwm_lut.dll`.
2. GUI stages `.cube` LUTs under `%SystemRoot%\Temp\luts`.
3. GUI writes `%SystemRoot%\Temp\luts\manifest.tsv` with monitor metadata.
4. GUI injects the DLL into `dwm.exe` through `LoadLibraryA`.
5. DLL logs profile and manifest data to `%SystemRoot%\Temp\dwm_lut.log`.
6. DLL reads the loaded `dwmcore.dll` PDB identity.
7. DLL selects exact RVAs from `DwmProfiles.generated.h`.
8. DLL hooks DWM Present and related optional functions.
9. DLL applies the LUT shader to the compositor backbuffer.

## Build Profile Strategy

Windows build numbers are not trusted as the primary identity. The DLL uses the loaded `dwmcore.dll` CodeView PDB GUID/age because cumulative updates can ship the same DWM component payload across different Windows build labels.

## Multi-Monitor Strategy

Current behavior:

- GUI config now persists display path, source ID, target ID, connector, name, and desktop position.
- GUI config restores monitors by path first, then target ID, source ID, and legacy ID.
- GUI staging writes the same identity fields into `manifest.tsv`.
- Single monitor uses a fast path and ignores fragile private DWM coordinates.
- Multi-monitor logs the staged GUI display manifest.
- Exact coordinate matches still work when DWM private coordinates match GUI coordinates.
- A tight nearest-position fallback tolerates small coordinate drift.

Planned robust behavior:

- Resolve DWM display identity from `CSwapChainRealization::GetDisplayId` or adjacent symbol-derived object state.
- Map that identity to GUI display path/source ID.
- Stop using top-left coordinates as the primary key.
