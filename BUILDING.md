# Building

This repo builds with Visual Studio Build Tools 2022, NuGet, and vcpkg. The helper scripts are written for PowerShell.

## One-command Release build

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\build-release.ps1
```

Set `-Platform x64` or `-Platform ARM64` explicitly when needed:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\build-release.ps1 -Platform x64
powershell -ExecutionPolicy Bypass -File .\scripts\build-release.ps1 -Platform ARM64
```

Output:

- Native DLL: `x64\Release\dwm_lut.dll`
- GUI folder: `src\DwmLutGUI\DwmLutGUI\bin\Release`
- ARM64 native DLL: `ARM64\Release\dwm_lut.dll`
- ARM64 GUI folder: `src\DwmLutGUI\DwmLutGUI\bin\ARM64\Release`

## ARM64 Toolchain

ARM64 builds require the Visual Studio Build Tools ARM64 C++ component. Run this from an elevated PowerShell window:

```powershell
& "C:\Program Files (x86)\Microsoft Visual Studio\Installer\setup.exe" modify --installPath "C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools" --add Microsoft.VisualStudio.Component.VC.Tools.ARM64 --quiet --norestart
```

Then confirm the compiler exists:

```powershell
Get-ChildItem "C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\VC\Tools\MSVC" -Recurse -Filter cl.exe | Where-Object FullName -match '\\Hostx64\\arm64\\cl.exe$'
```

## Programmatic smoke test

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\test-build.ps1
```

The test rebuilds Release, verifies expected files, verifies that modern DWM profiles are compiled in, and prints the native DLL SHA-256.

## Package

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\package-release.ps1 -Version "4.0.0-modern-windows"
powershell -ExecutionPolicy Bypass -File .\scripts\package-build-matrix.ps1 -Version "4.0.0-modern-windows" -Platform x64
```

The first command creates the general portable zip under `dist`, plus a stable alias folder and zip:

```text
dist\universal-win-x64\DwmLutGUI.exe
dist\dwm_lut_universal-win-x64.zip
```

That is the current "universal" app entry point for known x64 Windows builds. It is universal across compiled x64 DWM profiles, not across CPU architectures. Keep `DwmLutGUI.exe`, `dwm_lut.dll`, `WindowsDisplayAPI.dll`, and the `.config` file together in the same folder.

The second command creates per-build folders and zips under `artifacts\packages\build-matrix`.

## Refresh Public Build Catalog

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\update-build-catalog.ps1
```

The catalog is intentionally separate from the compiled profile table. It can list builds and architectures that are discoverable through UUP before the app can actually support them.

## Notes

- Packages are unsigned.
- Canary packages are diagnostic until validated on live Canary.
- The DLL selects its hook profile by loaded `dwmcore.dll` PDB GUID/age, not by package folder name.
