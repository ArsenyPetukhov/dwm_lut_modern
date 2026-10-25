# Web search ledger — dual AMD DWM LUT investigation

Date: 2026-10-25 (Asia/Yangon). **28 separate web search calls, 56 query strings.** GitHub API requests and direct documentation fetches are additional and are not counted as searches.

Search output can be incomplete or stale. Source code at a pinned revision and the attached log take precedence. Empty and irrelevant searches are retained below. No third-party mirror or forum result was used to establish the repair.

## Search 01

- `site:github.com/ArsenyPetukhov/dwm_lut_modern AMD dual GPU`
- `"dwm_lut_modern" AMD bug`

Outcome: Located this fork, upstream hybrid-GPU issue #24, and maintained forks documenting graphics-device isolation.

Returned links (discovery, not automatic endorsement): [1](https://github.com/ArsenyPetukhov/dwm_lut_modern), [2](https://github.com/ledoge/dwm_lut/blob/master/README.md?plain=1), [3](https://github.com/lauralex/dwm_lut), [4](https://github.com/ledoge/dwm_lut/releases/latest/download/release.zip).

## Search 02

- `site:github.com/ArsenyPetukhov/dwm_lut_modern/issues`
- `site:github.com/ArsenyPetukhov/dwm_lut_modern "AMD" "GPU"`

Outcome: Search indexing mostly returned the default branch and upstream issues; the exact issue was subsequently read with GitHub API.

Returned links (discovery, not automatic endorsement): [1](https://github.com/ArsenyPetukhov/dwm_lut_modern), [2](https://github.com/ledoge/dwm_lut/issues), [3](https://github.com/sup3rflyer/DesktopLUT), [4](https://github.com/GianSegugio/dwm_lut_is_back/blob/master/DOCUMENTATION.md).

## Search 03

- `"dwm_lut" "dual" "AMD"`
- `"dwm_lut" "two" "AMD"`

Outcome: Discovered upstream lauralex/dwm_lut issue #97, titled Not working with dual AMD GPU laptop. Search also returned unrelated calibration discussion; it was not used as technical evidence.

Returned links (discovery, not automatic endorsement): [1](https://112.cachefly.net/lauralex/dwm_lut/issues), [2](https://forum.pcmonitors.info/topic/driver-level-srgb-emulation-gamut-clamp/page/2/), [3](https://note.com/yuyu_7731/n/n3366eec8356f?hl=en), [4](https://hub.displaycal.net/forums/topic/i-made-a-tool-for-applying-3d-luts-to-the-windows-desktop/page/8/).

## Search 04

- `"dwm_lut" "multi-GPU" bug`
- `"dwm_lut" "RX" "integrated"`

Outcome: Found dwm_lut_is_back documentation describing adapter/output resource isolation and device teardown handling.

Returned links (discovery, not automatic endorsement): [1](https://github.com/GianSegugio/dwm_lut_is_back), [2](https://github.com/Microsoft/vcpkg.git), [3](https://github.com/ed1ii/dwm_lut_fixed), [4](https://github.com/GianSegugio/dwm_lut_is_back/blob/master/README.md).

## Search 05

- `site:github.com/ledoge/dwm_lut/issues/24 "same number"`
- `site:github.com/lauralex/dwm_lut/issues "AMD"`

Outcome: Read the older ledoge issue #24: hybrid mode, repeated monitor numbers, black screen; this is a different topology from the current log.

Returned links (discovery, not automatic endorsement): [1](https://github.com/ledoge/dwm_lut/issues/24), [2](https://github.com/GianSegugio/dwm_lut_is_back), [3](https://github.com/Microsoft/vcpkg.git), [4](https://github.com/sup3rflyer/DesktopLUT/issues/1).

## Search 06

- `site:github.com/zkippp/dwm_lut_fixed "GPU"`
- `site:github.com/ed1ii/dwm_lut_fixed "multi" "device"`

Outcome: Found ed1ii release history documenting multiple stages of multi-GPU and multi-monitor repairs.

Returned links (discovery, not automatic endorsement): [1](https://github.com/ed1ii/dwm_lut_fixed/releases), [2](https://github.com/GianSegugio/dwm_lut_is_back/blob/master/DOCUMENTATION.md), [3](https://github.com/GianSegugio/dwm_lut_is_back), [4](https://github.com/lauralex/dwm_lut/issues/65).

## Search 07

- `site:github.com/lauralex/dwm_lut/issues/97`
- `"Not working with dual AMD GPU laptop"`

Outcome: Direct search did not expose the full AMD issue; fetched issue #97 through GitHub API and indexed its page.

Returned links (discovery, not automatic endorsement): [1](https://github.com/lauralex/dwm_lut), [2](https://github.com/Microsoft/vcpkg.git), [3](https://www.techpowerup.com/download/visual-c-redistributable-runtime-package-all-in-one/), [4](https://112.cachefly.net/lauralex/dwm_lut/issues).

## Search 08

- `site:learn.microsoft.com D3D11 resources device created different device context`
- `site:learn.microsoft.com ID3D11DeviceChild GetDevice IDXGISwapChain GetDevice`

Outcome: Located Microsoft documentation defining Direct3D device and device-child ownership.

Returned links (discovery, not automatic endorsement): [1](https://learn.microsoft.com/en-us/windows/win32/direct3d11/overviews-direct3d-11-devices-intro), [2](https://learn.microsoft.com/en-us/windows/desktop/api/d3d11/nn-d3d11-id3d11devicechild), [3](https://learn.microsoft.com/en-us/windows/uwp/gaming/tutorial--assembling-the-rendering-pipeline), [4](https://learn.microsoft.com/en-us/windows/win32/api/d3d11/nf-d3d11-id3d11devicechild-getdevice).

## Search 09

- `site:learn.microsoft.com DISPLAYCONFIG_PATH_SOURCE_INFO adapterId id unique adapter`
- `site:learn.microsoft.com DISPLAYCONFIG_PATH_TARGET_INFO adapterId id unique target`

Outcome: Located Microsoft CCD source and target identity definitions; IDs are adapter-scoped.

Returned links (discovery, not automatic endorsement): [1](https://learn.microsoft.com/en-us/windows/win32/api/wingdi/ns-wingdi-displayconfig_path_source_info), [2](https://learn.microsoft.com/en-us/windows/win32/api/wingdi/ns-wingdi-displayconfig_path_target_info), [3](https://learn.microsoft.com/en-us/windows-hardware/drivers/display/relationship-of-mode-information-to-path-information), [4](https://learn.microsoft.com/en-us/windows/win32/api/wingdi/ns-wingdi-displayconfig_device_info_header).

## Search 10

- `site:learn.microsoft.com D3D11 immediate context thread safe multithread protection`
- `site:learn.microsoft.com DXGI_ERROR_DEVICE_REMOVED GetDeviceRemovedReason Direct3D 11`

Outcome: Located Microsoft immediate-context thread-safety and DXGI device-removal documentation.

Returned links (discovery, not automatic endorsement): [1](https://learn.microsoft.com/en-us/windows/win32/api/d3d11_4/nn-d3d11_4-id3d11multithread), [2](https://learn.microsoft.com/en-us/windows/win32/direct3ddxgi/dxgi-error), [3](https://learn.microsoft.com/en-us/windows/uwp/gaming/handling-device-lost-scenarios), [4](https://msdn.microsoft.com/library/mt125501.aspx#InstallGraphicsTools--------------------------------------------------------------------------------).

## Search 11

- `site:github.com/ArsenyPetukhov/dwm_lut_modern/issues/2`
- `"dwm_lut" "7700S"`

Outcome: Search did not surface exact target issue content reliably. GitHub API found issue #2 and its log attachment.

Returned links (discovery, not automatic endorsement): [1](https://github.com/ArsenyPetukhov/dwm_lut_modern), [2](https://github.com/ledoge/dwm_lut/blob/master/README.md?plain=1), [3](https://github.com/lauralex/dwm_lut), [4](https://github.com/ledoge/dwm_lut/releases/latest/download/release.zip).

## Search 12

- `site:github.com/ArsenyPetukhov/dwm_lut_modern "v5.0.0"`
- `"dwm_lut" "26100.4946"`

Outcome: Release/build search was noisy and returned unrelated releases. GitHub releases/tags API established the actual v5 source revision instead.

Returned links (discovery, not automatic endorsement): [1](https://github.com/actions/upload-pages-artifact/releases), [2](https://github.com/googlemaps/android-maps-utils/releases), [3](https://github.com/ShiinaSaku/Hayate/releases), [4](https://github.com/HEnquist/camillagui-backend/releases).

## Search 13

- `site:learn.microsoft.com ID3D11DeviceContext CopyResource restrictions same device`
- `site:learn.microsoft.com D3D11_MESSAGE_ID_DEVICE_SETSHADERRESOURCES_VIEWS_EMPTY DEVICE_MISMATCH`

Outcome: Located Microsoft CopyResource documentation. Its dimension/format rules do not establish cross-device ownership safety; ownership evidence was taken from device documentation.

Returned links (discovery, not automatic endorsement): [1](https://learn.microsoft.com/en-us/windows/win32/api/d3d11/nf-d3d11-id3d11devicecontext-copyresource), [2](https://learn.microsoft.com/en-us/windows/win32/api/d3d11/ne-d3d11-d3d11_resource_misc_flag), [3](https://learn.microsoft.com/en-us/windows/win32/api/d3d11/nf-d3d11-id3d11devicecontext-copysubresourceregion), [4](https://learn.microsoft.com/en-us/windows/win32/api/d3d11/nn-d3d11-id3d11devicecontext).

## Search 14

- `site:learn.microsoft.com ID3D11Device GetDeviceRemovedReason DXGI_ERROR_DEVICE_HUNG`
- `site:learn.microsoft.com DXGI HybridPresent integrated discrete graphics DWM`

Outcome: Located Microsoft GetDeviceRemovedReason and DXGI error-code references. No such HRESULT appears in the reporter log.

Returned links (discovery, not automatic endorsement): [1](https://learn.microsoft.com/en-us/windows/win32/api/d3d11/nf-d3d11-id3d11device-getdeviceremovedreason), [2](https://learn.microsoft.com/en-us/windows/win32/direct3ddxgi/dxgi-error), [3](https://learn.microsoft.com/de-de/windows/win32/api/d3d11/nf-d3d11-id3d11device-getdeviceremovedreason), [4](https://learn.microsoft.com/en-us/windows/win32/api/d3d11/nn-d3d11-id3d11device).

## Search 15

- `"CWindowContext::IsCandidateDirectFlipCompatible" "24H2"`
- `"BDEBC2AB" "dwmcore"`

Outcome: Exact PDB/signature query did not locate this payload. A related DesktopLUT handoff was discovery only, not evidence for this machine.

Returned links (discovery, not automatic endorsement): [1](https://github.laiyagushi.com/sup3rflyer/DesktopLUT/blob/main/HANDOFF_HAGS_FLIPQUEUE_2026-09-06.md), [2](https://github.com/GianSegugio/dwm_lut_is_back/blob/master/DOCUMENTATION.md).

## Search 16

- `"dwm_lut" "FAILED to find" "CWindowContext"`
- `"dwm_lut" "26100" "4946"`

Outcome: No results for the exact failure-marker/build combinations.

## Search 17

- `site:learn.microsoft.com GetModuleInformation dwmcore private symbols PDB GUID age`
- `site:learn.microsoft.com PE debug directory CodeView RSDS GUID age PDB`

Outcome: Located Microsoft PE/CodeView debug-directory and module-information references.

Returned links (discovery, not automatic endorsement): [1](https://learn.microsoft.com/en-us/windows/win32/psapi/module-information), [2](https://learn.microsoft.com/en-us/windows/win32/debug/pe-format), [3](https://learn.microsoft.com/mt-mt/dotNet/API/system.reflection.portableexecutable.codeviewdebugdirectorydata?view=net-5.0), [4](https://learn.microsoft.com/en-us/windows/win32/debug/symbol-files).

## Search 18

- `site:amd.com RX 7700S Ryzen integrated SmartAccess Graphics MUX laptop`
- `site:learn.microsoft.com display only hybrid rendering discrete integrated GPU DWM adapter`

Outcome: Located Microsoft hybrid cross-adapter presentation documentation. It explains possible multi-device paths, not proof the reporter uses one.

Returned links (discovery, not automatic endorsement): [1](https://learn.microsoft.com/en-us/windows-hardware/drivers/display/using-cross-adapter-resources-in-a-hybrid-system), [2](https://learn.microsoft.com/en-us/windows-hardware/drivers/display/rendering-on-a-discrete-gpu-using-cross-adapter-resources), [3](https://learn.microsoft.com/en-us/windows-hardware/drivers/display/validating-a-hybrid-system-configuration), [4](https://learn.microsoft.com/en-us/windows-hardware/drivers/display/supporting-caso).

## Search 19

- `site:learn.microsoft.com IDXGIDeviceSubObject GetDevice HRESULT IUnknown`
- `site:learn.microsoft.com IDXGISwapChain GetBuffer ID3D11Texture2D GetDevice example`

Outcome: Located Microsoft IDXGIDeviceSubObject::GetDevice documentation, confirming a typed COM method and HRESULT contract.

Returned links (discovery, not automatic endorsement): [1](https://learn.microsoft.com/en-us/windows/win32/api/dxgi/nf-dxgi-idxgidevicesubobject-getdevice), [2](https://learn.microsoft.com/en-us/windows/desktop/api/dxgi/nn-dxgi-idxgidevicesubobject), [3](https://learn.microsoft.com/en-us/windows/desktop/direct3ddxgi/d3d10-graphics-programming-guide-dxgi), [4](https://learn.microsoft.com/en-us/windows/win32/api/dxgi/nn-dxgi-idxgiswapchain).

## Search 20

- `site:learn.microsoft.com desktop app C++ try catch access violation SEH /EHsc`
- `site:learn.microsoft.com DllMain loader lock Direct3D deadlock best practices`

Outcome: Located Microsoft DLL loader-lock guidance; that concern is separate from the captured Present-time terminal interval.

Returned links (discovery, not automatic endorsement): [1](https://learn.microsoft.com/en-us/windows/win32/Dlls/dynamic-link-library-best-practices), [2](https://learn.microsoft.com/en-us/cpp/dotnet/initialization-of-mixed-assemblies?view=msvc-170), [3](https://learn.microsoft.com/en-us/windows/win32/Dlls/dllmain), [4](https://learn.microsoft.com/en-us/dotnet/framework/debug-trace-profile/loaderlock-mda).

## Search 21

- `site:amd.com "7735HS" "680M"`
- `site:asus.com "FA617NT" "7700S" "MUX"`

Outcome: Located AMD official Ryzen 7735HS specifications identifying Radeon 680M integrated graphics; hardware topology still comes from the reporter.

Returned links (discovery, not automatic endorsement): [1](https://www.amd.com/en/support/downloads/drivers.html/processors/ryzen/ryzen-7000-series/amd-ryzen-7-7735hs.html), [2](https://www.amd.com/content/dam/amd/en/documents/partner-hub/ryzen/amd-ryzen-7000-series-mobile-processors-quick-reference-guide.pdf), [3](https://www.amd.com/content/dam/amd/en/documents/partner-hub/ryzen/ryzen-consumer-master-quick-reference-competitive.pdf), [4](https://www.amd.com/content/dam/amd/en/documents/partner-hub/ryzen/amd-ryzen-7000-series-mobile-how-to-sell-guide-generational.pdf).

## Search 22

- `site:learn.microsoft.com Windows Error Reporting LocalDumps dwm.exe dump`
- `site:learn.microsoft.com WinDbg !analyze exception c0000005 vtable`

Outcome: Located Microsoft WER per-application dump collection guidance.

Returned links (discovery, not automatic endorsement): [1](https://learn.microsoft.com/en-us/troubleshoot/windows-client/shell-experience/scenario-guide-black-screen-after-sign-in), [2](https://learn.microsoft.com/en-us/windows/win32/wer/collecting-user-mode-dumps), [3](https://learn.microsoft.com/en-us/windows-hardware/drivers/debugger/bug-check-0x1aa-exception-on-invalid-stack), [4](https://learn.microsoft.com/en-us/windows-hardware/drivers/debugger/getting-started-with-windbg).

## Search 23

- `"dwm_lut" "swapChain" "AMD"`
- `"dwm_lut" "Initializing stuff in ApplyLUT"`

Outcome: No specific AMD swap-chain failure report found; unrelated forum output excluded.

Returned links (discovery, not automatic endorsement): [1](https://forum.doom9.org/archive/index.php/t-172783.html), [2](https://www.reddit.com/r/Windows11/comments/qg8dcg/for_those_of_you_going_crazy_over_icc_profiles/), [3](http://bugs.madshi.net/view.php?id=462), [4](https://ibb.co/QcfSWm5).

## Search 24

- `"dwm_lut" "24H2" "GetDevice"`
- `"dwm_lut" "7700S" crash`

Outcome: No results for the exact GetDevice/build/GPU combinations.

## Search 25

- `site:learn.microsoft.com "Cannot catch" "asynchronous" "/EHsc"`
- `site:learn.microsoft.com /EHsc access violations catch ellipsis structured exceptions`

Outcome: Located Microsoft exception-handling guidance; actual release /EH compiler flags were not established from the project alone.

Returned links (discovery, not automatic endorsement): [1](https://learn.microsoft.com/en-us/windows/win32/debug/exception-handling), [2](https://learn.microsoft.com/sr-cyrl-rs/cpp/build/reference/eh-exception-handling-model?view=msvc-170), [3](https://learn.microsoft.com/en-us/cpp/cpp/try-throw-and-catch-statements-cpp?view=msvc-160), [4](https://learn.microsoft.com/en-us/cpp/cpp/exception-handling-differences?view=msvc-170).

## Search 26

- `site:asus.com FA617NT MUX switch Smart Access Graphics 2023 TUF A16`
- `site:github.com/GianSegugio/dwm_lut_is_back "24H2" "swapchain"`

Outcome: Located fork documentation explaining version-specific backbuffer traversal and removal of speculative pointer probing.

Returned links (discovery, not automatic endorsement): [1](https://github.com/GianSegugio/dwm_lut_is_back/blob/master/DOCUMENTATION.md), [2](https://www.asus.com/us/laptops/for-gaming/tuf-gaming/asus-tuf-gaming-a16-advantage-edition-2023/techspec), [3](https://www.asus.com/laptops/for-gaming/tuf-gaming/asus-tuf-gaming-a16-advantage-edition-2023/techspec/), [4](https://www.asus.com/ca-en/laptops/for-gaming/tuf-gaming/asus-tuf-gaming-a16-advantage-edition-2023/techspec/).

## Search 27

- `ASUS TUF Gaming A16 Advantage Edition 2023 FA617NT MUX switch official` (domains: asus.com)
- `FA617NT 7735HS RX 7700S tech specs` (domains: asus.com)

Outcome: Located official ASUS A16 family/global specifications including FA617NT and MUX/Smart Access Graphics. Actual active wiring remains reporter-provided.

Returned links (discovery, not automatic endorsement): [1](https://www.asus.com/us/laptops/for-gaming/tuf-gaming/asus-tuf-gaming-a16-advantage-edition-2023/techspec/), [2](https://www.asus.com/laptops/for-gaming/tuf-gaming/asus-tuf-gaming-a16-advantage-edition-2023/techspec/), [3](https://www.asus.com/us/laptops/for-gaming/tuf-gaming/asus-tuf-gaming-a16-advantage-edition-2023/techspec), [4](https://www.asus.com/laptops/for-gaming/all-series/asus-tuf-gaming-a16-advantage-edition-2023/).

## Search 28

- `Windows 11 26100.4946 August 2025 KB5063878` (domains: support.microsoft.com)
- `dwmcore.dll BDEBC2AB 26100.4946`

Outcome: Located official Microsoft KB5063878 for OS build 26100.4946. OS build alone does not identify the exact dwmcore.dll payload.

Returned links (discovery, not automatic endorsement): [1](https://support.microsoft.com/en-us/servicing/os/windows-11/2025/08/august-12-2025-kb5063878-os-build-26100-4946), [2](https://support.microsoft.com/de-de/servicing/os/windows-11/2025/08/august-12-2025-kb5063878-os-build-26100-4946), [3](https://support.microsoft.com/es-es/servicing/os/windows-11/2025/08/august-12-2025-kb5063878-os-build-26100-4946), [4](https://support.microsoft.com/ro-ro/servicing/os/windows-11/2025/08/august-12-2025-kb5063878-os-build-26100-4946).
