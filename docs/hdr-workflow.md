# HDR Workflow

DWM LUT can be part of a Windows HDR calibration workflow when the display lacks hardware LUT calibration.

## Intended Workflow

1. Measure/profile the display with DisplayCAL/ArgyllCMS or equivalent.
2. Generate a 3D LUT for the intended SDR or HDR domain.
3. Load the `.cube` LUT in DWM LUT.
4. Apply correction through DWM.
5. Validate with measurement reports and visual checks.

## HDR Requirements

HDR LUTs should be generated for the same signal domain the shader expects. The current shader path treats HDR as BT.2100/PQ-ish through scRGB conversion. Do not assume a calibrator LUT is correct until an identity LUT and an obvious test LUT are verified first.

## What This Can Help

- Gamut correction within panel limits
- White point correction
- Tonal/color transform errors within panel limits

## What This Cannot Fix

- Panel uniformity
- ABL behavior
- Poor local dimming
- Insufficient peak brightness
- Apps that bypass DWM
- Hardware instability
