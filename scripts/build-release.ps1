param(
    [string]$Configuration = "Release",
    [string]$Platform = "x64"
)

$ErrorActionPreference = "Stop"
$repo = Resolve-Path (Join-Path $PSScriptRoot "..")
$solution = Join-Path $repo "DwmLutModern.sln"

function Find-MSBuild {
    $cmd = Get-Command msbuild -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }

    $vswhere = Join-Path ${env:ProgramFiles(x86)} "Microsoft Visual Studio\Installer\vswhere.exe"
    if (Test-Path $vswhere) {
        $msbuild = & $vswhere -latest -products * -requires Microsoft.Component.MSBuild -find "MSBuild\**\Bin\MSBuild.exe" | Select-Object -First 1
        if ($msbuild) { return $msbuild }
    }

    throw "MSBuild was not found. Install Visual Studio Build Tools with C++ and .NET desktop build tools."
}

$nuget = Get-Command nuget -ErrorAction SilentlyContinue
if ($nuget) {
    & $nuget.Source restore (Join-Path $repo "src\DwmLutGUI\DwmLutGUI.sln") -PackagesDirectory (Join-Path $repo "src\DwmLutGUI\packages")
} elseif (Test-Path (Join-Path $repo "..\tools\nuget.exe")) {
    & (Join-Path $repo "..\tools\nuget.exe") restore (Join-Path $repo "src\DwmLutGUI\DwmLutGUI.sln") -PackagesDirectory (Join-Path $repo "src\DwmLutGUI\packages")
} else {
    Write-Warning "nuget.exe was not found; continuing because packages may already be restored."
}

$msbuildPath = Find-MSBuild
& $msbuildPath $solution /p:Configuration=$Configuration /p:Platform=$Platform /m
if ($LASTEXITCODE -ne 0) { throw "MSBuild failed with exit code $LASTEXITCODE" }

$nativeDll = Join-Path $repo "x64\$Configuration\dwm_lut.dll"
$nativePdb = Join-Path $repo "x64\$Configuration\dwm_lut.pdb"
$guiOut = Join-Path $repo "src\DwmLutGUI\DwmLutGUI\bin\$Configuration"

if (!(Test-Path $nativeDll)) { throw "Native DLL not found at $nativeDll" }
if (!(Test-Path $guiOut)) { throw "GUI output not found at $guiOut" }

Copy-Item -LiteralPath $nativeDll -Destination (Join-Path $guiOut "dwm_lut.dll") -Force
if (Test-Path $nativePdb) {
    Copy-Item -LiteralPath $nativePdb -Destination (Join-Path $guiOut "dwm_lut.pdb") -Force
}

Write-Host "Build complete: $guiOut"
