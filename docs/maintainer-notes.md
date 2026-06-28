# Maintainer Notes

Do not chase Windows build labels alone. Always identify the loaded `dwmcore.dll` by CodeView PDB GUID/age.

The next major code task is replacing texture retrieval by private vtable ordinals with a symbol-backed path through `CSwapChainRealization` or an adjacent stable object relationship.

The next major multi-monitor task is mapping DWM display identity to GUI display identity. The current manifest gives enough logging context to debug that mapping on real systems.
