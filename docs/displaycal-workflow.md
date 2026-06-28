# DisplayCAL / ArgyllCMS Workflow

## Basic SDR Workflow

1. Create or load a display profile in DisplayCAL.
2. Enable 3D LUT generation.
3. Generate a `.cube` LUT.
4. Load the LUT into DWM LUT.
5. Apply to the target monitor.
6. Verify using DisplayCAL verification tools.

## Basic HDR Workflow

Use a LUT generated for the HDR transfer function and color primaries you are testing. For this fork, validate with:

1. Identity HDR LUT.
2. Obvious test HDR LUT.
3. Calibrator-produced LUT.

## Report Requirements

Calibration accuracy reports must include:

- Instrument model
- Display model
- Target colorspace
- LUT size
- Verification report
- Windows HDR state
- `%SystemRoot%\Temp\dwm_lut.log`
