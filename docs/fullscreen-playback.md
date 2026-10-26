# Fullscreen playback

Use fullscreen windowed mode. DWM LUT cannot correct true exclusive fullscreen
or protected video that bypasses Windows composition.

The v5.0.4 preview updates fullscreen handling for Windows 11 24H2 build
`26100.4946`. Disable LUTs and close the old copy before extracting the new
portable package into a separate folder. Run `DwmLutModern.exe` as Administrator,
leave Compatibility on Auto, and apply the intended LUT.

## SDR workaround in MPC-HC

If the LUT disappears when the player controls hide:

1. Open **Options → Playback → Output** and select **MPC Video Renderer**.
2. Open its **Settings**. Keep **Use Direct3D 11** enabled and set
   **Swap effect** to **Discard**.
3. Disable **D3D Fullscreen / exclusive fullscreen** in the player. Use normal
   fullscreen windowed mode.
4. Close and reopen MPC-HC, then play the video with DWM LUT enabled.

This applies even if you previously used EVR or madVR: select MPC Video Renderer
for this workaround. You can return to the previous renderer afterwards.

Discard uses the older copying presentation path, which can cost some playback
performance. [Microsoft describes how this differs from Flip](https://learn.microsoft.com/en-us/windows/win32/api/dxgi/ne-dxgi-dxgi_swap_effect).
The [renderer exposes both settings](https://github.com/clsid2/MPCVideoRenderer/blob/d6674bbc21f8eb777c16ccbb302cc3c986c87e5f/Source/PropPage.cpp#L285).

If it still changes presentation mode, open **Windows Settings → System →
Display → Graphics**, select/add MPC-HC, and turn off **Optimizations for
windowed games** for it if that option is available. Restart the player. Windows
can otherwise upgrade older presentation paths to Flip; see
[Microsoft's per-app instructions](https://support.microsoft.com/en-us/windows/optimizations-for-windowed-games-in-windows-11-3f006843-2c7e-4ed0-9a5e-f9389e535952).

### HDR limitation

This workaround is for **SDR output**. An HDR file can be tone-mapped by the
renderer to SDR, but that changes the output you are calibrating. Use the LUT
appropriate for that SDR output.

For HDR output, MPC Video Renderer
[forces Flip regardless of the Discard selection](https://github.com/clsid2/MPCVideoRenderer/blob/d6674bbc21f8eb777c16ccbb302cc3c986c87e5f/Source/DX11VideoProcessor.cpp#L1845).
Windows Auto HDR can also re-enable windowed optimizations. Do not assume this
SDR workaround covers HDR. HDR correction needs an HDR LUT and working DWM
composition through the new native policy.

## If fullscreen still loses the LUT

Keep the Windows log at `%SystemRoot%\Temp\dwm_lut.log`. For the matching build,
v5.0.4 logs:

```text
Fullscreen policy active: native mode-5 OverlaysEnabled=true, DisableIndependentFlip=1, OverlayTestMode=5
```

An absent line or a rejected-policy message means that policy was not activated.
The affected-laptop result, renderer name, and SDR/HDR state are needed to
distinguish a remaining composition bypass from a LUT-domain mismatch.
