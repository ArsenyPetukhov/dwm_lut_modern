param(
    [switch]$SkipBuild
)

$ErrorActionPreference = "Stop"
$repo = Resolve-Path (Join-Path $PSScriptRoot "..")

if (-not $SkipBuild) {
    & (Join-Path $PSScriptRoot "build-release.ps1")
}

$checks = @(
    "DwmLutModern.sln",
    "src\lutdwm\dllmain.cpp",
    "src\lutdwm\DwmProfiles.generated.h",
    "src\DwmLutGUI\DwmLutGUI\Injector.cs",
    "src\DwmLutGUI\DwmLutGUI\bin\Release\DwmLutGUI.exe",
    "src\DwmLutGUI\DwmLutGUI\bin\Release\dwm_lut.dll",
    "docs\compatibility-matrix.md",
    "docs\architecture.md",
    "artifacts\profiles\dwm_profiles_table.md"
)

foreach ($relative in $checks) {
    $path = Join-Path $repo $relative
    if (!(Test-Path -LiteralPath $path)) { throw "Missing expected file: $relative" }
}

$profileHeader = Get-Content -Raw -LiteralPath (Join-Path $repo "src\lutdwm\DwmProfiles.generated.h")
foreach ($needle in @("26200.8737_stable", "28000.2340_published26h1", "29617.1000_canary")) {
    if ($profileHeader -notlike "*$needle*") { throw "Profile header missing $needle" }
}

$dll = Join-Path $repo "src\DwmLutGUI\DwmLutGUI\bin\Release\dwm_lut.dll"
$hash = Get-FileHash -LiteralPath $dll -Algorithm SHA256
Write-Host "dwm_lut.dll SHA256: $($hash.Hash)"
Write-Host "Programmatic build checks passed."
