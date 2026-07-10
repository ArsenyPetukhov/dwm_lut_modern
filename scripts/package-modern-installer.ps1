param(
    [string]$Configuration = "Release",
    [ValidatePattern('^\d+\.\d+\.\d+$')]
    [string]$Version = "5.0.0",
    [switch]$SkipBuild
)

$ErrorActionPreference = "Stop"
$repo = Resolve-Path (Join-Path $PSScriptRoot "..")
$payload = Join-Path $repo "dist\DwmLutModern-x64-portable-selfcontained"
$installerProject = Join-Path $repo "installer\DwmLutModern.Installer\DwmLutModern.Installer.wixproj"
$installerOutput = Join-Path $repo "dist\installer"

if (-not $SkipBuild) {
    & (Join-Path $PSScriptRoot "build-release.ps1") -Configuration $Configuration -Platform x64
    if ($LASTEXITCODE -ne 0) { throw "Release build failed with exit code $LASTEXITCODE" }
}

& (Join-Path $PSScriptRoot "package-modern-app.ps1") -Configuration $Configuration -Platform x64 -Version $Version -SelfContained
if ($LASTEXITCODE -ne 0) { throw "Modern portable packaging failed with exit code $LASTEXITCODE" }

if (Test-Path $installerOutput) { Remove-Item -Recurse -Force $installerOutput }
& dotnet build $installerProject -c $Configuration -p:PayloadDir=$payload -p:ProductVersion=$Version -o $installerOutput
if ($LASTEXITCODE -ne 0) { throw "WiX installer build failed with exit code $LASTEXITCODE" }

$msi = Get-ChildItem $installerOutput -Filter *.msi -File | Select-Object -First 1
if (-not $msi) { throw "Installer build completed without producing an MSI." }

$hash = (Get-FileHash $msi.FullName -Algorithm SHA256).Hash
Set-Content -Encoding ascii -Path ($msi.FullName + ".sha256") -Value "$hash  $($msi.Name)"

Write-Host "Installer: $($msi.FullName)"
Write-Host "SHA256: $hash"
