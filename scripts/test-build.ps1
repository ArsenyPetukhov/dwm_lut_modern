param(
    [ValidateSet("x64", "ARM64")]
    [string]$Platform = "x64",
    [switch]$SkipBuild
)

$ErrorActionPreference = "Stop"
$repo = Resolve-Path (Join-Path $PSScriptRoot "..")

function Find-MSBuild {
    $command = Get-Command msbuild -ErrorAction SilentlyContinue
    if ($command) { return $command.Source }

    $vswhere = Join-Path ${env:ProgramFiles(x86)} "Microsoft Visual Studio\Installer\vswhere.exe"
    if (Test-Path $vswhere) {
        $path = & $vswhere -latest -products * -requires Microsoft.Component.MSBuild -find "MSBuild\**\Bin\MSBuild.exe" |
            Select-Object -First 1
        if ($path) { return $path }
    }

    throw "MSBuild was not found; native routing tests cannot be compiled."
}

if (-not $SkipBuild) {
    & (Join-Path $PSScriptRoot "build-release.ps1") -Platform $Platform
}

$guiOut = if ($Platform -eq "x64") {
    "src\DwmLutGUI\DwmLutGUI\bin\Release"
} else {
    "src\DwmLutGUI\DwmLutGUI\bin\$Platform\Release"
}
$modernOut = "src\DwmLutModern.App\bin\$Platform\Release\net8.0-windows10.0.19041.0"

$checks = @(
    "DwmLutModern.sln",
    "src\lutdwm\dllmain.cpp",
    "src\lutdwm\LutRouting.h",
    "src\lutdwm\DwmProfiles.generated.h",
    "src\DwmLutGUI\DwmLutGUI\DwmProfiles.generated.cs",
    "src\DwmLutGUI\DwmLutGUI\Injector.cs",
    "$guiOut\DwmLutGUI.exe",
    "$guiOut\dwm_lut.dll",
    "$modernOut\DwmLutModern.exe",
    "$modernOut\DwmLutModern.dll",
    "$modernOut\dwm_lut.dll",
    "docs\compatibility-matrix.md",
    "docs\architecture.md",
    "docs\architecture-review-v5.md",
    "docs\build-catalog.md",
    "docs\validation-lab.md",
    "docs\version-history.md",
    "docs\roadmap.md",
    "docs\universal-build-strategy.md",
    "docs\arm64-roadmap.md",
    "docs\research\legacy-universal-support.md",
    "scripts\test-dwm-payload-profile.ps1",
    "scripts\validation\Collect-ValidationEvidence.ps1",
    "scripts\validation\New-DiagnosticLuts.ps1",
    "scripts\validation\Run-SandboxSmoke.ps1",
    "scripts\validation\Start-WindowsSandboxValidation.ps1",
    "src\DwmLutModern.App\Themes\LiquidGlass.xaml",
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

Get-ChildItem -LiteralPath (Join-Path $repo "scripts\validation") -Filter "*.ps1" | ForEach-Object {
    try {
        [void][ScriptBlock]::Create((Get-Content -Raw -LiteralPath $_.FullName))
    } catch {
        throw "Validation script does not parse: $($_.Name): $($_.Exception.Message)"
    }
}
Write-Host "Validation scripts parsed successfully."

$profileHeader = Get-Content -Raw -LiteralPath (Join-Path $repo "src\lutdwm\DwmProfiles.generated.h")
foreach ($needle in @("26200.8737_stable", "28000.2340_published26h1", "29617.1000_canary")) {
    if ($profileHeader -notlike "*$needle*") { throw "Profile header missing $needle" }
}

$nativeSource = Get-Content -Raw -LiteralPath (Join-Path $repo "src\lutdwm\dllmain.cpp")
$routingSource = Get-Content -Raw -LiteralPath (Join-Path $repo "src\lutdwm\LutRouting.h")
foreach ($forbidden in @(
    "Nearest multi-monitor LUT selected",
    "Falling back to first LUT with matching HDR state",
    "Falling back to first available LUT",
    "g_primaryHdrContext"
)) {
    if ($nativeSource -like "*$forbidden*") {
        throw "Unsafe multi-monitor LUT fallback remains in native source: $forbidden"
    }
}

foreach ($requiredSafetyFragment in @(
    "connectedMonitorCount != 1",
    "No exact multi-monitor LUT match; applying none"
)) {
    if (($nativeSource + $routingSource) -notlike "*$requiredSafetyFragment*") {
        throw "Native source is missing the fail-closed routing guard: $requiredSafetyFragment"
    }
}

& (Join-Path $PSScriptRoot "generate-dwm-profile-header.ps1") -Check

if ($Platform -eq "x64") {
    $nativeTestProject = Join-Path $repo "tests\LutRoutingTests\LutRoutingTests.vcxproj"
    & (Find-MSBuild) $nativeTestProject /p:Configuration=Release /p:Platform=x64 /m
    if ($LASTEXITCODE -ne 0) { throw "Native routing test build failed with exit code $LASTEXITCODE" }

    $nativeTestExe = Join-Path $repo "tests\LutRoutingTests\bin\x64\Release\LutRoutingTests.exe"
    & $nativeTestExe
    if ($LASTEXITCODE -ne 0) { throw "Native routing tests failed with exit code $LASTEXITCODE" }
    Write-Host "Native routing tests passed."

    & dotnet test (Join-Path $repo "tests\DwmLutModern.App.Tests\DwmLutModern.App.Tests.csproj") -c Release -p:Platform=x64 --no-restore
    if ($LASTEXITCODE -ne 0) { throw "Modern app tests failed with exit code $LASTEXITCODE" }
}

$buildCatalog = Get-Content -Raw -LiteralPath (Join-Path $repo "artifacts\uup\build-catalog.generated.csv")
foreach ($needle in @("29617.1000"",""arm64", "26220.8754"",""arm64", "28020.2366"",""amd64")) {
    if ($buildCatalog -notlike "*$needle*") { throw "Build catalog missing $needle" }
}

$dll = Join-Path $repo "$guiOut\dwm_lut.dll"
$hash = Get-FileHash -LiteralPath $dll -Algorithm SHA256
Write-Host "dwm_lut.dll SHA256: $($hash.Hash)"
Write-Host "Programmatic build checks passed."
