param(
    [ValidateSet("x64", "ARM64")]
    [string]$Platform = "x64",
    [switch]$SkipBuild
)

$ErrorActionPreference = "Stop"
$repo = Resolve-Path (Join-Path $PSScriptRoot "..")

if (-not $SkipBuild) {
    & (Join-Path $PSScriptRoot "build-release.ps1") -Platform $Platform
}

$guiOut = if ($Platform -eq "x64") {
    "src\DwmLutGUI\DwmLutGUI\bin\Release"
} else {
    "src\DwmLutGUI\DwmLutGUI\bin\$Platform\Release"
}

$checks = @(
    "DwmLutModern.sln",
    "src\lutdwm\dllmain.cpp",
    "src\lutdwm\DwmProfiles.generated.h",
    "src\DwmLutGUI\DwmLutGUI\Injector.cs",
    "$guiOut\DwmLutGUI.exe",
    "$guiOut\dwm_lut.dll",
    "docs\compatibility-matrix.md",
    "docs\architecture.md",
    "docs\build-catalog.md",
    "docs\universal-build-strategy.md",
    "docs\arm64-roadmap.md",
    "docs\research\legacy-universal-support.md",
    "artifacts\uup\build-catalog.generated.csv",
    "artifacts\uup\build-catalog.generated.json",
    "artifacts\profiles\dwm_profiles_table.md",
    "artifacts\research\legacy-repos.generated.csv",
    "artifacts\research\legacy-native-engines.generated.csv"
)

foreach ($relative in $checks) {
    $path = Join-Path $repo $relative
    if (!(Test-Path -LiteralPath $path)) { throw "Missing expected file: $relative" }
}

$profileHeader = Get-Content -Raw -LiteralPath (Join-Path $repo "src\lutdwm\DwmProfiles.generated.h")
foreach ($needle in @("26200.8737_stable", "28000.2340_published26h1", "29617.1000_canary")) {
    if ($profileHeader -notlike "*$needle*") { throw "Profile header missing $needle" }
}

& (Join-Path $PSScriptRoot "generate-dwm-profile-header.ps1") -Check

$buildCatalog = Get-Content -Raw -LiteralPath (Join-Path $repo "artifacts\uup\build-catalog.generated.csv")
foreach ($needle in @("29617.1000"",""arm64", "26220.8754"",""arm64", "28020.2366"",""amd64")) {
    if ($buildCatalog -notlike "*$needle*") { throw "Build catalog missing $needle" }
}

$dll = Join-Path $repo "$guiOut\dwm_lut.dll"
$hash = Get-FileHash -LiteralPath $dll -Algorithm SHA256
Write-Host "dwm_lut.dll SHA256: $($hash.Hash)"
Write-Host "Programmatic build checks passed."
