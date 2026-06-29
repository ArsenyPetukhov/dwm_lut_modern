param(
    [string]$Version = "4.0.0-modern-windows",
    [ValidateSet("x64", "ARM64")]
    [string]$Platform = "x64",
    [switch]$SkipBuild
)

$ErrorActionPreference = "Stop"
$repo = Resolve-Path (Join-Path $PSScriptRoot "..")

if (-not $SkipBuild) {
    & (Join-Path $PSScriptRoot "build-release.ps1") -Platform $Platform
}

$packageArch = if ($Platform -eq "ARM64") { "arm64" } else { "x64" }
if ($Platform -eq "x64") {
    $guiOut = Join-Path $repo "src\DwmLutGUI\DwmLutGUI\bin\Release"
} else {
    $guiOut = Join-Path $repo "src\DwmLutGUI\DwmLutGUI\bin\$Platform\Release"
}
$matrixRoot = Join-Path $repo "artifacts\packages\build-matrix\$Version"
New-Item -ItemType Directory -Force -Path $matrixRoot | Out-Null

$builds = @(
    @{ Id = "19045.7417_win10-22H2-fallback"; Stability = "fallback only"; Channel = "Windows 10 22H2" },
    @{ Id = "26100.8737_24H2-fallback"; Stability = "fallback only"; Channel = "stable 24H2" },
    @{ Id = "26200.8655_current-machine"; Stability = "verified on this PC"; Channel = "local 25H2" },
    @{ Id = "26200.8737_stable-25H2-KB5095093"; Stability = "profiled"; Channel = "stable 25H2" },
    @{ Id = "26220.8754_beta-25H2"; Stability = "profiled"; Channel = "Beta 25H2" },
    @{ Id = "26300.8758_experimental-26H2"; Stability = "profiled"; Channel = "experimental 26H2" },
    @{ Id = "28000.2340_published-26H1"; Stability = "profiled"; Channel = "published 26H1" },
    @{ Id = "28020.2366_beta-26H1"; Stability = "profiled"; Channel = "Beta 26H1" },
    @{ Id = "28120.2374_experimental-26H1"; Stability = "profiled"; Channel = "experimental 26H1" },
    @{ Id = "29617.1000_canary-future-platforms"; Stability = "diagnostic only"; Channel = "Canary Future Platforms" }
)

$required = @(
    "DwmLutGUI.exe",
    "DwmLutGUI.exe.config",
    "WindowsDisplayAPI.dll",
    "dwm_lut.dll"
)

foreach ($name in $required) {
    $path = Join-Path $guiOut $name
    if (!(Test-Path -LiteralPath $path)) { throw "Missing package file: $path" }
}

foreach ($build in $builds) {
    $payload = Join-Path $matrixRoot ("dwm_lut_modern-$Version-win-$packageArch-" + $build.Id)
    if (Test-Path -LiteralPath $payload) {
        Remove-Item -LiteralPath $payload -Recurse -Force
    }
    New-Item -ItemType Directory -Force -Path $payload | Out-Null

    foreach ($name in $required) {
        Copy-Item -LiteralPath (Join-Path $guiOut $name) -Destination (Join-Path $payload $name) -Force
    }
    foreach ($optional in @("WindowsDisplayAPI.xml", "DwmLutGUI.pdb", "dwm_lut.pdb")) {
        $path = Join-Path $guiOut $optional
        if (Test-Path -LiteralPath $path) {
            Copy-Item -LiteralPath $path -Destination (Join-Path $payload $optional) -Force
        }
    }

    Copy-Item -LiteralPath (Join-Path $repo "LICENSE") -Destination (Join-Path $payload "LICENSE") -Force
    Copy-Item -LiteralPath (Join-Path $repo "README.md") -Destination (Join-Path $payload "README.md") -Force
    Copy-Item -LiteralPath (Join-Path $repo "docs\compatibility-matrix.md") -Destination (Join-Path $payload "compatibility-matrix.md") -Force
    Copy-Item -LiteralPath (Join-Path $repo "docs\build-catalog.md") -Destination (Join-Path $payload "build-catalog.md") -Force
    Copy-Item -LiteralPath (Join-Path $repo "docs\universal-build-strategy.md") -Destination (Join-Path $payload "universal-build-strategy.md") -Force
    Copy-Item -LiteralPath (Join-Path $repo "docs\arm64-roadmap.md") -Destination (Join-Path $payload "arm64-roadmap.md") -Force
    Copy-Item -LiteralPath (Join-Path $repo "artifacts\profiles\dwm_profiles_table.md") -Destination (Join-Path $payload "dwm_profiles_table.md") -Force
    Copy-Item -LiteralPath (Join-Path $repo "artifacts\uup\build-catalog.generated.csv") -Destination (Join-Path $payload "build-catalog.generated.csv") -Force

    $installDoc = Join-Path $payload "README_INSTALL.md"
    @"
# DWM LUT Modern - $($build.Id)

This package contains one multi-profile binary. The folder name is the Windows build profile it was prepared for:

- Build profile: $($build.Id)
- Channel: $($build.Channel)
- Stability: $($build.Stability)
- Log file: `%SystemRoot%\Temp\dwm_lut.log`

## Install

1. Extract this folder somewhere writable.
2. Run `DwmLutGUI.exe` as administrator.
3. Select your SDR LUT and, if needed, your HDR LUT.
4. Leave Resolver on Auto unless you are deliberately testing a manual fallback mode.
5. Press Apply.
6. Read `%SystemRoot%\Temp\dwm_lut.log` if the image does not change.

## Important

The DLL chooses its hook profile from the actual `dwmcore.dll` public PDB identity, not from the folder name. If Microsoft ships a new DWM binary with a new PDB GUID, the binary will log `No matching DWM symbol profile` and fall back to byte signatures. Treat Canary packages as diagnostic until verified on that build.
"@ | Set-Content -LiteralPath $installDoc -Encoding UTF8

    & (Join-Path $PSScriptRoot "generate-checksums.ps1") -ReleaseDir $payload

    $zip = Join-Path $matrixRoot ("dwm_lut_modern-$Version-win-$packageArch-" + $build.Id + ".zip")
    if (Test-Path -LiteralPath $zip) {
        Remove-Item -LiteralPath $zip -Force
    }
    Compress-Archive -Path (Join-Path $payload "*") -DestinationPath $zip
}

Write-Host "Build-matrix packages complete: $matrixRoot"
