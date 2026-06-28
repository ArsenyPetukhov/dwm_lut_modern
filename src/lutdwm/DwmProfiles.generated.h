// Generated/curated from Microsoft public symbols by research tooling.
#pragma once
#include <stdint.h>

enum DwmProfileMachine : uint32_t {
    DwmProfileMachineX64 = 0x8664,
    DwmProfileMachineArm64 = 0xAA64,
};

struct DwmSymbolProfile {
    const char* id;
    DwmProfileMachine machine;
    const char* pdbGuid;
    uint32_t pdbAge;
    const char* sha256;
    uint32_t presentRva;
    uint32_t directFlipCompatibleRva;
    uint32_t overlaysEnabledRva;
    uint32_t overlayTestModeRva;
    uint32_t displaySwapChainGetBackBufferRva;
    uint32_t displaySwapChainPresentDFlipRva;
    uint32_t d3dDevicePresentMpoRva;
    bool requireOverlaysEnabled;
};

static const DwmSymbolProfile kDwmSymbolProfiles[] = {
    { "26200.8655_current", DwmProfileMachineX64, "35D945A7-1171-77C7-B6CD-6D7D9ED0A2E8", 1, "03C820B55D3CF470C9E80C9905186F43937A7E79CC1D81BCE524DA823B6FCA8D", 0x231800, 0xb1414, 0x1a2be8, 0x3fb22c, 0x2c830, 0x217d90, 0x1d8bc0, true },
    { "26200.8737_stable", DwmProfileMachineX64, "AE59991D-5182-9D46-C2DF-79FD96B670FD", 1, "38225FEBFF2B43F6F19DD12E4F6833EBE12A2D68DEE631A9552D7D402E1991AE", 0x473c0, 0x0, 0x4ed64, 0x3e7ef4, 0x1de700, 0x206860, 0x46f70, true },
    { "26220.8754_beta25h2__26300.8758_exp26h2", DwmProfileMachineX64, "12B33AD2-4F89-E236-EB73-414E39695B45", 1, "02BA53D0351FECA7F50A1D793718A8883B5729E286E957C2D6AFE504144ACDFB", 0x473c0, 0x0, 0x4ed64, 0x3e7ef4, 0x1de700, 0x206860, 0x46f70, true },
    { "28000.2340_published26h1", DwmProfileMachineX64, "A501A027-A993-31EF-661B-19606968926E", 1, "B7603B710B4EC607E0EF1DF2E18BEBF515FE39F2CEA29181966B5A1FCE60D252", 0x18b510, 0x23e424, 0x0, 0x3e0c84, 0x1b05b0, 0x203a90, 0xf1bb0, false },
    { "28020.2366_beta26h1__28120.2374_exp26h1", DwmProfileMachineX64, "14BD81D0-E230-08E7-EECE-589B8D29E6EA", 1, "93CF529C49A94A87927203C1241FB4F6E035A59D291428CEE5C350BEA2BABDEC", 0x18b510, 0x23e424, 0x0, 0x3e0c84, 0x1b05b0, 0x203a90, 0xf1bb0, false },
    { "29617.1000_canary", DwmProfileMachineX64, "9D2F7102-1CDA-8409-2018-100E70836B6E", 1, "0FCD486AB588F609BAF745B8CF5A7BE74377BB18FC8A9602153E6E1ABDFE3165", 0x5e534, 0x17fabc, 0x0, 0x3b8688, 0x1acfb0, 0x1dcb10, 0x1ace74, false },
};
