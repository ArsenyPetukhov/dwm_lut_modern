// Generated from artifacts/profiles/compiled_dwm_profiles.json.
// Run scripts/generate-dwm-profile-header.ps1 after editing profiles.
namespace DwmLutGUI
{
    internal sealed class DwmKnownProfile
    {
        public DwmKnownProfile(string id, string machine, string pdbGuid, uint pdbAge, string sha256, string engineFamily, string presentAbi, string swapchainStrategy, string backbufferStrategy, string overlayStrategy, string monitorIdentityStrategy, string validationState)
        {
            Id = id;
            Machine = machine;
            PdbGuid = pdbGuid;
            PdbAge = pdbAge;
            Sha256 = sha256;
            EngineFamily = engineFamily;
            PresentAbi = presentAbi;
            SwapchainStrategy = swapchainStrategy;
            BackbufferStrategy = backbufferStrategy;
            OverlayStrategy = overlayStrategy;
            MonitorIdentityStrategy = monitorIdentityStrategy;
            ValidationState = validationState;
        }

        public string Id { get; }
        public string Machine { get; }
        public string PdbGuid { get; }
        public uint PdbAge { get; }
        public string Sha256 { get; }
        public string EngineFamily { get; }
        public string PresentAbi { get; }
        public string SwapchainStrategy { get; }
        public string BackbufferStrategy { get; }
        public string OverlayStrategy { get; }
        public string MonitorIdentityStrategy { get; }
        public string ValidationState { get; }
    }

    internal static class DwmKnownProfiles
    {
        public static readonly DwmKnownProfile[] All =
        {
            new DwmKnownProfile("26200.8655_current", "x64", "35D945A7-1171-77C7-B6CD-6D7D9ED0A2E8", 1u, "03C820B55D3CF470C9E80C9905186F43937A7E79CC1D81BCE524DA823B6FCA8D", "modern-profiled", "overlay-present-modern7", "profiled-display-swapchain", "profiled-get-backbuffer", "overlays-enabled-plus-overlay-test-mode", "single-monitor-fast-path-or-coordinate-fallback", "local-built"),
            new DwmKnownProfile("26200.8737_stable", "x64", "AE59991D-5182-9D46-C2DF-79FD96B670FD", 1u, "38225FEBFF2B43F6F19DD12E4F6833EBE12A2D68DEE631A9552D7D402E1991AE", "modern-profiled", "overlay-present-modern7", "profiled-display-swapchain", "profiled-get-backbuffer", "overlays-enabled-plus-overlay-test-mode", "single-monitor-fast-path-or-coordinate-fallback", "built"),
            new DwmKnownProfile("26220.8754_beta25h2__26300.8758_exp26h2", "x64", "12B33AD2-4F89-E236-EB73-414E39695B45", 1u, "02BA53D0351FECA7F50A1D793718A8883B5729E286E957C2D6AFE504144ACDFB", "modern-profiled", "overlay-present-modern7", "profiled-display-swapchain", "profiled-get-backbuffer", "overlays-enabled-plus-overlay-test-mode", "single-monitor-fast-path-or-coordinate-fallback", "built"),
            new DwmKnownProfile("28000.2340_published26h1", "x64", "A501A027-A993-31EF-661B-19606968926E", 1u, "B7603B710B4EC607E0EF1DF2E18BEBF515FE39F2CEA29181966B5A1FCE60D252", "modern-profiled", "overlay-present-modern7", "profiled-display-swapchain", "profiled-get-backbuffer", "directflip-hook-plus-overlay-test-mode", "single-monitor-fast-path-or-coordinate-fallback", "built"),
            new DwmKnownProfile("28020.2366_beta26h1__28120.2374_exp26h1", "x64", "14BD81D0-E230-08E7-EECE-589B8D29E6EA", 1u, "93CF529C49A94A87927203C1241FB4F6E035A59D291428CEE5C350BEA2BABDEC", "modern-profiled", "overlay-present-modern7", "profiled-display-swapchain", "profiled-get-backbuffer", "directflip-hook-plus-overlay-test-mode", "single-monitor-fast-path-or-coordinate-fallback", "built"),
            new DwmKnownProfile("29617.1000_canary", "x64", "9D2F7102-1CDA-8409-2018-100E70836B6E", 1u, "0FCD486AB588F609BAF745B8CF5A7BE74377BB18FC8A9602153E6E1ABDFE3165", "modern-profiled", "overlay-present-modern7", "profiled-display-swapchain", "profiled-get-backbuffer", "directflip-hook-plus-overlay-test-mode", "single-monitor-fast-path-or-coordinate-fallback", "experimental"),
        };
    }
}
