# Generated icon asset brief

Approved direction comes from the user's request: a monitor showing LUT/color
correction, Apple-like glass, neutral white/gray, no purple/blue ambient glow.

Produce using the built-in image generation tool, one asset per call:
- `Assets/Icons/app.png`: the application identity, transparent outside a clean
  rounded-square frosted-glass badge. Silver monitor, a small color-patch grid
  inside the screen, and one small lookup-cube detail. Clear at small sizes.
- `Assets/Icons/displays.png`: simple front-facing monitor pictogram.
- `Assets/Icons/luts.png`: simple cube/table pictogram for the LUT library.
- `Assets/Icons/settings.png`: simple sliders pictogram.
- `Assets/Icons/documentation.png`: simple open-book pictogram.
- `Assets/Icons/about.png`: simple circled information pictogram.

Navigation assets are simple, flat neutral-gray silhouettes/strokes (#808080),
transparent background, no text, no shadows, no badge, minimal fine detail. They
render at 20–24 DIP with visible labels. Generate large originals, inspect them,
then use deterministic size/format conversion for runtime PNG/ICO packaging.
Preserve prompt/provenance sidecars. Do not edit application code.
