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
$packageRoot = Join-Path $repo "dist\$Version"
$payload = Join-Path $packageRoot "dwm_lut_modern-$Version-win-$packageArch"

New-Item -ItemType Directory -Force -Path $payload | Out-Null

$required = @(
    "DwmLutGUI.exe",
    "DwmLutGUI.exe.config",
    "WindowsDisplayAPI.dll",
    "dwm_lut.dll"
)

foreach ($name in $required) {
    $path = Join-Path $guiOut $name
    if (!(Test-Path -LiteralPath $path)) { throw "Missing package file: $path" }
    Copy-Item -LiteralPath $path -Destination (Join-Path $payload $name) -Force
}

foreach ($optional in @("WindowsDisplayAPI.xml", "DwmLutGUI.pdb", "dwm_lut.pdb")) {
    $path = Join-Path $guiOut $optional
    if (Test-Path -LiteralPath $path) {
        Copy-Item -LiteralPath $path -Destination (Join-Path $payload $optional) -Force
    }
}

Copy-Item -LiteralPath (Join-Path $repo "README.md") -Destination (Join-Path $payload "README.md") -Force
Copy-Item -LiteralPath (Join-Path $repo "LICENSE") -Destination (Join-Path $payload "LICENSE") -Force
Copy-Item -LiteralPath (Join-Path $repo "docs\compatibility-matrix.md") -Destination (Join-Path $payload "compatibility-matrix.md") -Force
Copy-Item -LiteralPath (Join-Path $repo "docs\build-catalog.md") -Destination (Join-Path $payload "build-catalog.md") -Force
Copy-Item -LiteralPath (Join-Path $repo "docs\universal-build-strategy.md") -Destination (Join-Path $payload "universal-build-strategy.md") -Force
Copy-Item -LiteralPath (Join-Path $repo "docs\arm64-roadmap.md") -Destination (Join-Path $payload "arm64-roadmap.md") -Force
Copy-Item -LiteralPath (Join-Path $repo "docs\engineering\fix-all-technical-guide.md") -Destination (Join-Path $payload "fix-all-technical-guide.md") -Force
Copy-Item -LiteralPath (Join-Path $repo "artifacts\profiles\dwm_profiles_table.md") -Destination (Join-Path $payload "dwm_profiles_table.md") -Force
Copy-Item -LiteralPath (Join-Path $repo "artifacts\uup\build-catalog.generated.csv") -Destination (Join-Path $payload "build-catalog.generated.csv") -Force

& (Join-Path $PSScriptRoot "generate-checksums.ps1") -ReleaseDir $payload

$zip = Join-Path $packageRoot "dwm_lut_modern-$Version-win-$packageArch.zip"
if (Test-Path -LiteralPath $zip) { Remove-Item -LiteralPath $zip -Force }
Compress-Archive -Path (Join-Path $payload "*") -DestinationPath $zip

$alias = Join-Path $repo "dist\universal-win-$packageArch"
if (Test-Path -LiteralPath $alias) {
    try {
        Remove-Item -LiteralPath $alias -Recurse -Force -ErrorAction Stop
    } catch {
        Write-Warning "Could not replace $alias. Close any running DwmLutGUI.exe from that folder. Writing a side-by-side alias instead."
        $alias = Join-Path $repo "dist\universal-win-$packageArch-next"
        if (Test-Path -LiteralPath $alias) {
            Remove-Item -LiteralPath $alias -Recurse -Force
        }
    }
}
New-Item -ItemType Directory -Force -Path $alias | Out-Null
Copy-Item -Path (Join-Path $payload "*") -Destination $alias -Recurse -Force

$aliasSuffix = if ($alias -like "*-next") { "-next" } else { "" }
$aliasZip = Join-Path $repo "dist\dwm_lut_universal-win-$packageArch$aliasSuffix.zip"
if (Test-Path -LiteralPath $aliasZip) { Remove-Item -LiteralPath $aliasZip -Force }
Compress-Archive -Path (Join-Path $alias "*") -DestinationPath $aliasZip

Write-Host "Package complete: $zip"
Write-Host "Universal alias: $alias"
Write-Host "Universal alias zip: $aliasZip"
