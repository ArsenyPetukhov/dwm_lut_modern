# Dual AMD laptop / Windows 11 24H2 repair

Date: 2026-10-25 (Asia/Yangon). Local branch: `fix/24h2-amd-device-acquisition`.

## Scope and current status

The repair is implemented against released `v5.0.0`, commit
`6a4e5cc897c68cada194e2f5861d5b4e6d180882`. At the start of the investigation,
`main` still pointed to `3d23617920cfc83e9afa64e8fcda3e1271f0748b`; investigating only that checkout
would inspect a different product revision. The newer remote branches had no
native graphics changes relative to the release when checked.

The exact Windows payload now has a dedicated native profile, typed backbuffer
access, and adapter/target LUT routing. A Windows x64 DLL and matching PDB were
successfully cross-compiled on this Mac and bundled with the checksum-verified
official v5 portable app. Compilation/package hashes are recorded in
`dist/amd-24h2-preview/DwmLutModern-v5-amd24h2-hotfix.build.json`.
On 2026-10-26, the user reported that the published build worked on the affected
laptop. Normal windowed LUT application is therefore recorded as verified for
that reported setup. Fullscreen windowed playback is a separate open issue;
exclusive fullscreen and other GPU/topology/HDR combinations are not covered
by this confirmation.

## The actual report

[Issue #2](https://github.com/ArsenyPetukhov/dwm_lut_modern/issues/2) reports
Ryzen integrated graphics plus RX 7700S, Windows 11 24H2 build `26100.4946`, and
brief blanking after Apply followed by no correction. The reporter additionally
identified an ASUS TUF A16 FA617NT with Ryzen 7735HS, configured through its MUX
for dedicated graphics. Both panel and HDMI are described as wired to the dGPU;
the iGPU remains enumerated. Driver changes and other graphics modes reportedly
did not help. Those are reporter statements, not independently observed wiring.

The earlier [upstream issue #97](https://github.com/lauralex/dwm_lut/issues/97)
describes the same GPU pair and symptom on version 4.0.2. The older
[hybrid display issue #24](https://github.com/ledoge/dwm_lut/issues/24) describes
duplicate display numbering in hybrid mode. It is useful historical context,
but does not establish the cause of this dedicated-mode report.

## Log evidence and its limit

The local `Error logs/dwm_lut.log` and
[public attachment](https://github.com/user-attachments/files/33289078/dwm_lut.log)
were byte-identical:

```text
bytes: 17222
SHA-256: d16d34d7b3c35eb25729afaba75b3af586cdbf570ed0f88f56360fb5e749c4c4
nonempty lines: 175
PID groups: 7, each containing 25 lines
```

Every PID group identifies the same unprofiled x64 DWM payload, parses one SDR
33-point LUT, enters the Present hook, then ends at device initialization.
There is no successful device-acquisition marker, caught-exception record, or
logged failing HRESULT. The capture stages **one** HDMI display, despite the
machine having two possible connected screens. Log timestamps have no timezone.

The old path reads a private field at `IOverlaySwapChain + 0x108` and treats it
as `IDXGISwapChain`. The terminal interval covers its `GetDevice` call through
the returned device's `AddRef`, before the next success marker. The precise
faulting instruction cannot be determined from this text log. Seven PID groups
are consistent with process turnover; they do not independently prove seven
crashes, seven user actions, or seven watchdog retries. Optional hook lookup
failures did not prevent Present-hook entry in the capture.

## Exact Microsoft binary investigation

The matching DLL was downloaded from
[Microsoft's symbol server](https://msdl.microsoft.com/download/symbols/dwmcore.dll/1EB354B5447000/dwmcore.dll),
using [Winbindex metadata](https://winbindex.m417z.com/) for discovery. Its public
PDB was downloaded from
[Microsoft's matching symbol key](https://msdl.microsoft.com/download/symbols/dwmcore.pdb/BDEBC2AB1C704383D1FC2FEB9A6FE6831/dwmcore.pdb).

| Property | Verified value |
| --- | --- |
| DLL version | `10.0.26100.4946` |
| Architecture | x64 / `0x8664` |
| DLL SHA-256 | `D81EFC3A05B4D4CACD424706CF06A98400136F6660806666177436EA75F99427` |
| CodeView/PDB GUID | `BDEBC2AB-1C70-4383-D1FC-2FEB9A6FE683` |
| PE CodeView and PDB DBI age | `1` |
| PDB information-stream age | `3`; kept distinct, cause not inferred |

The GUID and DBI age match the captured log. The official
[KB5063878 page](https://support.microsoft.com/en-us/servicing/os/windows-11/2025/08/august-12-2025-kb5063878-os-build-26100-4946)
identifies the OS build; OS build alone is not the runtime profile key.

Public symbols and vtable entries establish this contract:

| Implementation | Outer vtable RVA | Slot 24: physical buffer | Buffer vtable RVA | Slot 17: D3D11 resource |
| --- | --- | --- | --- | --- |
| CDDisplay | `0x3219a0` | `0x1ff800` | `0x314200` | `0x212cc0` |
| CLegacy | `0x3229f0` | `0x12eec0` | `0x3142e0` | `0x12e8b0` |

On **both** buffer implementations, slot 19 targets `GetTransientRegion`, not a
graphics resource. Blindly reusing the newer 25H2 slot sequence would therefore
call the wrong method. The fix checks the paired vtable identities before calls.
The returned private buffer/resource pointers are borrowed; texture QI obtains
the reference owned by the hook.

Named getter bodies also prove these fields on both allowed outer objects:

| Field | Offset | Getter RVA |
| --- | --- | --- |
| Native source ID | `+0x30` | `0x226bd0` |
| Display adapter LUID | `+0x34` | `0x2094f0` |
| Target ID | `+0x3c` | `0x2d41d0`; CLegacy overrides retrieval |
| Hardware protection byte | `+0x64` | `0x1fd060` |

The display adapter is distinct from the rendering-adapter getter at `+0x50`.
The manifest already contains the display adapter and target. Its source number
is one-based presentation data, so source number is not used as output identity.
An unresolved target `UINT32_MAX` fails closed.

## Implemented changes

- Added profile `26100.4946_24h2`, selected by exact PDB identity and architecture.
- Kept the 24H2 Present ABI/layout separate from 25H2 dispatch.
- Added paired-vtable backbuffer access with guarded reads/calls and explicit
  rejection of unknown implementations. Lookup failure forwards normal Present
  without entering the `+0x108` path or scanning speculative offsets.
- Parsed the staged manifest and associated LUT files with canonical display
  adapter LUID + target ID. Runtime matching also requires the correct SDR/HDR
  domain. Missing or ambiguous metadata applies no LUT.
- Avoided private desktop-origin offsets for this profile, including multi-monitor
  output selection. Position is only used to join a staged filename to its row.
- Rejected protected/unreadable buffers, avoided resource creation for outputs
  without an applicable LUT, serialized this profile's shared rendering state,
  and prevented cross-device texture binding.
- Extended the owner generator to update the native header and **both** managed
  profile tables; the current app's model had previously been outside that output.
- Added a reproducible offline binary/symbol verifier and Windows build integration
  for the new portable helper executables.

The native resource pool remains owned by its first Direct3D device. Frames from
another device are deliberately skipped. The reporter confirmed the dedicated-GPU
path works in normal windowed use. This does not claim full calibration
on two simultaneously active render devices.
Other DWM payloads retain their previous paths; this is not a universal AMD fix.

## Checks already exercised

Before the user's instruction to stop test work, the following were completed:

| Check | Observed result | Boundary |
| --- | --- | --- |
| LUT routing executable | 26 checks, zero failures | Portable C++ logic |
| Backbuffer access executable | 33 checks, zero failures | Fake callbacks exercise the production helper |
| Manifest parser executable | 85 checks, zero failures | Real parser and ambiguity handling |
| Generated native header syntax | Passed | Host clang C++17 |
| Owner generator `-Check` | Passed | Native + both managed tables + profile table |
| Exact DLL/PDB verification | 12 vtable checks; getter body verification added | Offline ABI evidence |
| Independent backend review | Bounded pass | No Windows/MSVC/runtime acceptance implied |

Agents also exercised sanitizers on their bounded portable helpers. A parent
combined sanitizer command timed out after routing succeeded; it was not counted
as a full combined pass. No further test execution was requested after the user
steered the task to compilation.

## Windows build and hardware acceptance

Compiled local output:

```text
dist/amd-24h2-preview/DwmLutModern-v5-amd24h2-hotfix.zip
dist/amd-24h2-preview/dwm_lut.dll
dist/amd-24h2-preview/dwm_lut.pdb
```

The native build used Apple Clang 21, LLVM-MinGW 20261020 UCRT target libraries,
and the host Rust LLD linker. C++ runtime libraries are static; the resulting DLL
imports Windows system/UCRT APIs and D3DCompiler, with no separate libc++ DLL.
One libc++ header emitted its minimum compiler-version warning (21.1 versus the
host's 21.0). Compilation and linking succeeded. This is a cross-compiled preview,
not an MSVC build or an observed successful DWM injection.

The bundled GUI is the original official v5 binary, so its embedded support
table may still show a family fallback. The repaired native DLL logs the exact
profile selection. Source-generated managed tables have also been updated for
the next normal application build.

The normal project build remains:

```powershell
.\scripts\build-release.ps1 -Platform x64
```

For hardware verification, use the corrected package in the reporter's original
dedicated mode and confirm the log selects the exact profile, obtains a verified
physical buffer, matches the display adapter/target, and keeps the compositor
running with the visible LUT applied. Check panel-only, HDMI-only, and both active;
use distinct diagnostic LUTs to expose any display mix-up. Mixed SDR/HDR must keep
domains separate. Physical color accuracy still requires a measurement instrument.

If the corrected route still fails, the next useful artifact is the faulting
module/instruction and a DWM dump from that attempt, alongside the new log. It is
not necessary to ask the reporter again for the already supplied GPU models/MUX
mode. Microsoft's [WER local-dump documentation](https://learn.microsoft.com/en-us/windows/win32/wer/collecting-user-mode-dumps)
describes per-application capture; process identity needs write access to the
chosen folder. Such dumps should be reviewed privately.

## Reproduction tools and sources

The source verifier accepts the exact DLL/PDB paths:

```sh
uv run --python 3.11 --with pefile --with pdbparse python \
  scripts/verify-24h2-backbuffer-profile.py --dll /path/dwmcore.dll --pdb /path/dwmcore.pdb
```

The local research folder contains the original-log analyzer, factual log digest,
downloaded Microsoft payloads, and build receipts. Full web-search provenance is
in [the 28-call / 56-query ledger](dual-amd-web-searches.md). Search snippets were
not treated as proof of this machine's failure.

Additional primary references consulted:

- [Microsoft D3D11 device model](https://learn.microsoft.com/en-us/windows/win32/direct3d11/overviews-direct3d-11-devices-intro)
  for resource ownership.
- [Microsoft CCD target identity](https://learn.microsoft.com/en-us/windows/win32/api/wingdi/ns-wingdi-displayconfig_path_target_info)
  for adapter-scoped output identity.
- [Microsoft MSVC exception model](https://learn.microsoft.com/en-us/cpp/build/reference/eh-exception-handling-model?view=msvc-170):
  C++ catch behavior depends on compiler flags. The release project's actual
  command-line `/EH` flags were not established; blanket `/EHa` is not the repair.
- [Related fork documentation](https://github.com/GianSegugio/dwm_lut_is_back/blob/master/DOCUMENTATION.md)
  for comparison of adapter/output isolation and version-specific buffer access.
  Its claims are not hardware validation for this fork.
