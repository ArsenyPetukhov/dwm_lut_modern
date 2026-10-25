# Fullscreen windowed playback — online search ledger

Date: 2026-10-26 (Asia/Yangon). **20 separate online search calls, 41 query strings.** Direct source/document fetches and GitHub API requests are additional. Negative and irrelevant results are retained; search results alone were not used to choose memory addresses.

## Search 01

- `site:learn.microsoft.com dxgi flip model independent flip reverse composition occlusion overlay fullscreen video`
- `site:github.com/lauralex/dwm_lut fullscreen MPC-HC`
- `site:github.com/GianSegugio/dwm_lut_is_back fullscreen composition blocker`

Outcome: Microsoft documents borderless Independent Flip bypassing DWM; Lauralex documents its composition-based fullscreen boundary. Related fork describes a separate fullscreen workaround.

Discovery links: [1](https://learn.microsoft.com/en-us/windows/win32/direct3ddxgi/for-best-performance--use-dxgi-flip-model), [2](https://github.com/lauralex/dwm_lut), [3](https://github.com/GianSegugio/dwm_lut_is_back/blob/master/DOCUMENTATION.md).

## Search 02

- `site:github.com "dwm_lut" "MPC" fullscreen`
- `site:github.com "dwm_lut" "m_fDisableIndependentFlip"`

Outcome: No exact indexed matches for the MPC-HC/private-global queries. No root-cause claim was based on an empty result.

## Search 03

- `site:github.com "dwm_lut" "IsCandidateIndependentFlipCompatible"`
- `site:github.com "dwm_lut" "fullscreen" "AMD"`

Outcome: Related maintained fork documents Independent Flip bypass and version-sensitive fullscreen behavior; this does not establish this build's offsets.

Discovery links: [1](https://github.com/GianSegugio/dwm_lut_is_back), [2](https://github.com/ledoge/dwm_lut/issues/5), [3](https://github.com/TheG-VAN/dwm_frame_interpolator/blob/main/README.md), [4](https://github.com/GianSegugio/dwm_lut_is_back/blob/master/README.md).

## Search 04

- `site:github.com/clsid2/mpc-hc fullscreen D3D11 independent flip LUT`
- `site:github.com/Aleksoid1978/VideoRenderer fullscreen MPO DWM LUT`

Outcome: Located MPC Video Renderer source and older MPC-HC exclusive-fullscreen reports. These establish renderer/mode variety, not this laptop's cause.

Discovery links: [1](https://github.com/Aleksoid1978/VideoRenderer/blob/master/Source/VideoRenderer.cpp), [2](http://www.gnu.org/licenses/), [3](https://github.com/clsid2/mpc-hc/issues/546), [4](https://github.com/clsid2/mpc-hc/issues/2869).

## Search 05

- `site:learn.microsoft.com "Independent Flip" "video" "MPO"`
- `site:learn.microsoft.com "multiplane overlay" "composition" "color"`

Outcome: Microsoft explains MPO and Independent Flip as separate ways to avoid normal composition, including video playback.

Discovery links: [1](https://learn.microsoft.com/en-us/windows/win32/direct3ddxgi/for-best-performance--use-dxgi-flip-model), [2](https://learn.microsoft.com/en-us/shows/defrag-tools/150-media-experience-analyzer-part-2-video-playback-power-saving), [3](https://learn-video.azurefd.net/vod/player?show=defrag-tools&ep=150-media-experience-analyzer-part-2-video-playback-power-saving]), [4](https://learn-video.azurefd.net/vod/player?show=defrag-tools&ep=150-media-experience-analyzer-part-2-video-playback-power-saving).

## Search 06

- `"m_fDisableIndependentFlip" dwm`
- `site:github.com/sup3rflyer/DesktopLUT fullscreen composition`

Outcome: DesktopLUT documents fullscreen/borderless correction through disabling DirectFlip/iFlip, without a visible overlay.

Discovery links: [1](https://github.com/sup3rflyer/DesktopLUT/), [2](https://github.com/sup3rflyer/DesktopLUT), [3](https://github.com/sup3rflyer/DesktopLUT/releases), [4](https://github.laiyagushi.com/sup3rflyer/DesktopLUT/blob/main/HANDOFF_HAGS_FLIPQUEUE_2026-09-06.md).

## Search 07

- `site:github.com/Aleksoid1978/VideoRenderer/issues "MPO"`
- `site:github.com/Aleksoid1978/VideoRenderer/issues "LUT" fullscreen`

Outcome: Renderer issue #279 describes a distinct HDR/VSR problem and shows that fullscreen-only color changes are not sufficient proof of MPO.

Discovery links: [1](https://github.com/Aleksoid1978/VideoRenderer/issues/279), [2](https://we.tl/t-sNJconM5T4PozBvy), [3](https://github.com/Aleksoid1978/VideoRenderer/commits?after=a738e720eaa88b55c6087ad4ca11992c2a0c5190+139), [4](https://github.com/ledoge/dwm_lut/issues/5).

## Search 08

- `site:github.com/clsid2/mpc-hc "exclusive fullscreen" "renderer"`
- `site:github.com/lauralex/dwm_lut/issues "fullscreen"`

Outcome: Located Lauralex's documented fullscreen limitation and the MPC-HC maintainer's renderer FAQ, including fullscreen HDR switching.

Discovery links: [1](https://github.com/lauralex/dwm_lut), [2](https://github.com/Microsoft/vcpkg.git), [3](https://www.techpowerup.com/download/visual-c-redistributable-runtime-package-all-in-one/), [4](https://github.com/clsid2/mpc-hc/issues/2869).

## Search 09

- `site:learn.microsoft.com "fullscreen" "multiplane" "bypass"`
- `site:github.com/GameTechDev/PresentMon "Hardware: Independent Flip"`

Outcome: PresentMon's official documentation distinguishes composed, independent, hardware-overlay, and exclusive presentation modes.

Discovery links: [1](https://github.com/GameTechDev/PresentMon/blob/main/README-ConsoleApplication.md), [2](https://www.youtube.com/watch?v=E3wTajGZOsA), [3](https://software.intel.com/content/www/us/en/develop/articles/sample-application-for-direct3d-12-flip-model-swap-chains.html--------------------------------------------------------------------------------), [4](https://github.com/PubFork/GameTechDev-PresentMon/blob/main/README.md).

## Search 10

- `site:github.com/clsid2/mpc-hc/issues/2869 "fullscreen" "colors"`
- `site:github.com/Aleksoid1978/VideoRenderer "MPO" "fullscreen"`

Outcome: Further renderer search returned HDR/VSR discussion. Treated as an alternative explanation, not evidence of this report's cause.

Discovery links: [1](https://github.com/Aleksoid1978/VideoRenderer/issues/279), [2](https://we.tl/t-sNJconM5T4PozBvy), [3](https://github.com/clsid2/MPCVideoRenderer/blob/master/history.txt), [4](https://github.com/clsid2/mpc-hc/issues/3790).

## Search 11

- `"dwm_lut" "EVR" fullscreen`
- `"dwm_lut" "madVR" fullscreen`

Outcome: MadVR/EVR searches returned forum anecdotes and double-correction discussions; excluded as primary evidence for the implementation.

Discovery links: [1](https://hub.displaycal.net/forums/topic/i-made-a-tool-for-applying-3d-luts-to-the-windows-desktop/page/20/?SuperSocializerAuth=LiveJournal), [2](https://hub.displaycal.net/forums/topic/help-with-greyed-blacks-when-calibrating-for-srgb/page/2/), [3](https://forum.doom9.org/archive/index.php/t-165890-p-11.html), [4](https://www.overclockers.ge/topic/54222-mpc-hc-madvr/page/5/).

## Search 12

- `site:github.com/sup3rflyer/DesktopLUT "DisableIndependentFlip"`
- `site:github.com/GianSegugio/dwm_lut_is_back "DisableIndependentFlip"`

Outcome: DesktopLUT release history identifies missing DirectFlip functions and its DisableIndependentFlip replacement and restoration behavior.

Discovery links: [1](https://github.com/sup3rflyer/DesktopLUT/releases), [2](https://github.com/djdallmann/GamingPCSetup/blob/master/CONTENT/RESEARCH/FINDINGS/registrykeys_dwm.txt), [3](https://github.com/shoober420/windows11-scripts/blob/main/DWM_Tweaks.bat), [4](https://github.com/AlchemyTweaks/Verified-Tweaks/blob/main/DWM/DWMAdjustablesd-jdallmann.bat).

## Search 13

- `site:learn.microsoft.com DXGI fullscreen windowed independent flip reverse composition latency`
- `site:learn.microsoft.com "exclusive full-screen" "DWM"`

Outcome: Microsoft flip-model documentation confirms windowed fullscreen can bypass composition; exclusive fullscreen remains a distinct limitation.

Discovery links: [1](https://learn.microsoft.com/en-us/windows/win32/direct3ddxgi/for-best-performance--use-dxgi-flip-model), [2](https://learn.microsoft.com/en-us/windows/win32/direct3darticles/dxgi-best-practices), [3](https://learn.microsoft.com/en-us/windows-hardware/drivers/display/dxgi-presentation-path), [4](https://learn.microsoft.com/windows/win32/api/dxgi1_6/ne-dxgi1_6-dxgi_hardware_composition_support_flags).

## Search 14

- `site:github.com "DisableIndependentFlip" "OverlayTestMode"`
- `site:github.com "m_fDisableIndependentFlip" "restore"`

Outcome: Registry research lists exist, but no Windows registry change or unverified address was adopted.

Discovery links: [1](https://github.com/djdallmann/GamingPCSetup/blob/master/CONTENT/RESEARCH/FINDINGS/registrykeys_dwm.txt).

## Search 15

- `site:github.com/lauralex/dwm_lut "COverlayContext_OverlaysEnabled_hook"`
- `site:github.com/ledoge/dwm_lut "fullscreen" "DirectFlip"`

Outcome: Original ledoge documentation confirms that LUT correction historically depended on blocking DirectFlip/MPO.

Discovery links: [1](https://github.com/ledoge/dwm_lut/blob/master/README.md?plain=1), [2](https://github.com/ledoge/dwm_lut/releases/latest/download/release.zip), [3](https://github.com/lauralex/dwm_lut), [4](https://github.com/ledoge/dwm_lut).

## Search 16

- `site:github.com/clsid2/mpc-hc/issues fullscreen "EVR" "LUT"`
- `site:github.com/clsid2/mpc-hc/issues fullscreen "madVR" "DWM"`

Outcome: No current MPC-HC EVR/madVR issue matching this report was found. Unrelated mpv results were excluded.

Discovery links: [1](https://github.com/mpv-player/mpv/issues/102), [2](https://github.com/mpv-player/mpv/issues/1505), [3](https://github.com/leolin-hub/ZeroSplitVerifier), [4](https://gist.github.com/ilowkey/6382046).

## Search 17

- `site:github.com/sup3rflyer/DesktopLUT/releases "independent flip" "removed"`
- `site:github.com/sup3rflyer/DesktopLUT "m_fDisableIndependentFlip"`

Outcome: DesktopLUT's release notes and source support investigating the missing Independent Flip control rather than copying fragile short signatures.

Discovery links: [1](https://github.com/sup3rflyer/DesktopLUT/releases), [2](https://github.com/rubxngdev/FlipConfigTool), [3](https://github.com/valleyofdoom/PC-Tuning), [4](https://github.com/AzimsTech/amitxv-PC-Tuning/blob/main/docs/post-install.md).

## Search 18

- `site:learn.microsoft.com "flip" "DRM" "composition"`
- `site:learn.microsoft.com "protected" "multiplane overlays"`

Outcome: Microsoft graphics/protected-overlay material supports retaining protected-content guards; it does not justify processing protected buffers.

Discovery links: [1](https://learn.microsoft.com/en-us/windows-hardware/drivers/ddi/_display/), [2](https://learn.microsoft.com/en-us/answers/questions/5848104/critical-system-failures-game-crashes-after-the-in), [3](https://learn.microsoft.com/en-in/answers/questions/5848104/critical-system-failures-game-crashes-after-the-in), [4](https://learn.microsoft.com/de-de/windows-hardware/drivers/ddi/_display/).

## Search 19

- `site:github.com/Aleksoid1978/VideoRenderer "exclusive fullscreen" "Use Direct3D 11"`
- `site:github.com/clsid2/mpc-hc "madVR" "fullscreen exclusive"`

Outcome: Renderer search again showed fullscreen HDR/passthrough changes as a separate issue; coverage uses the shared compositor path.

Discovery links: [1](https://github.com/Aleksoid1978/VideoRenderer/issues/279), [2](https://github.com/clsid2/mpc-hc/releases?after=release_v1_5_SANDY_BRIDGE_PATCH), [3](https://github.com/clsid2/mpc-hc/releases/tag/2.8.1), [4](https://github.com/clsid2/mpc-hc/issues/27).

## Search 20

- `site:github.com/GameTechDev/PresentMon "Composed: Flip" "Hardware: Legacy Flip"`
- `site:learn.microsoft.com "DisableIndependentFlip"`

Outcome: Official PresentMon mode definitions provide a way to distinguish bypass from composed playback. No public Microsoft DisableIndependentFlip API documentation was found; it is treated as private and exact-profile-only.

Discovery links: [1](https://github.com/GameTechDev/PresentMon/blob/main/README-ConsoleApplication.md), [2](https://www.youtube.com/watch?v=E3wTajGZOsA), [3](https://software.intel.com/content/www/us/en/develop/articles/sample-application-for-direct3d-12-flip-model-swap-chains.html--------------------------------------------------------------------------------), [4](https://github.com/PubFork/GameTechDev-PresentMon/blob/main/README.md).
