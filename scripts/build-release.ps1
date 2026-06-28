param(
    [string]$Configuration = "Release",
    [ValidateSet("x64", "ARM64")]
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

function Test-Arm64Toolchain {
    $vswhere = Join-Path ${env:ProgramFiles(x86)} "Microsoft Visual Studio\Installer\vswhere.exe"
    if (Test-Path $vswhere) {
        $cl = & $vswhere -latest -products * -requires Microsoft.VisualStudio.Component.VC.Tools.ARM64 -find "VC\Tools\MSVC\**\bin\Hostx64\arm64\cl.exe" | Select-Object -First 1
        if ($cl) { return $true }
    }

    $roots = @(
        (Join-Path ${env:ProgramFiles(x86)} "Microsoft Visual Studio\2022\BuildTools\VC\Tools\MSVC"),
        (Join-Path ${env:ProgramFiles} "Microsoft Visual Studio\2022\Community\VC\Tools\MSVC")
    )
    foreach ($root in $roots) {
        if (!(Test-Path -LiteralPath $root)) { continue }
        $cl = Get-ChildItem -LiteralPath $root -Recurse -Filter cl.exe -ErrorAction SilentlyContinue |
            Where-Object { $_.FullName -match "\\bin\\Host(x64|x86)\\arm64\\cl\.exe$" } |
            Select-Object -First 1
        if ($cl) { return $true }
    }

    return $false
}

if ($Platform -eq "ARM64" -and -not (Test-Arm64Toolchain)) {
    throw "ARM64 C++ toolchain not found. Install Visual Studio component Microsoft.VisualStudio.Component.VC.Tools.ARM64, then rerun with -Platform ARM64."
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

$nativeDll = Join-Path $repo "$Platform\$Configuration\dwm_lut.dll"
$nativePdb = Join-Path $repo "$Platform\$Configuration\dwm_lut.pdb"
if ($Platform -eq "x64") {
    $guiOut = Join-Path $repo "src\DwmLutGUI\DwmLutGUI\bin\$Configuration"
} else {
    $guiOut = Join-Path $repo "src\DwmLutGUI\DwmLutGUI\bin\$Platform\$Configuration"
}

if (!(Test-Path $nativeDll)) { throw "Native DLL not found at $nativeDll" }
if (!(Test-Path $guiOut)) { throw "GUI output not found at $guiOut" }

Copy-Item -LiteralPath $nativeDll -Destination (Join-Path $guiOut "dwm_lut.dll") -Force
if (Test-Path $nativePdb) {
    Copy-Item -LiteralPath $nativePdb -Destination (Join-Path $guiOut "dwm_lut.pdb") -Force
}

Write-Host "Build complete ($Platform): $guiOut"
