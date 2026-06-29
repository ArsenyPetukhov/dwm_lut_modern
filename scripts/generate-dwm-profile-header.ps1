param(
    [string]$ProfilesPath = "artifacts\profiles\compiled_dwm_profiles.json",
    [string]$HeaderPath = "src\lutdwm\DwmProfiles.generated.h",
    [string]$TablePath = "artifacts\profiles\dwm_profiles_table.md",
    [switch]$Check
)

$ErrorActionPreference = "Stop"
$repo = Resolve-Path (Join-Path $PSScriptRoot "..")
$profilesFile = Join-Path $repo $ProfilesPath
$headerFile = Join-Path $repo $HeaderPath
$tableFile = Join-Path $repo $TablePath

function To-HexLiteral {
    param([object]$Value)
    if ($null -eq $Value) { return "0x0" }
    return "0x{0:x}" -f ([uint32]$Value)
}

function Machine-Literal {
    param([string]$Machine)
    switch ($Machine.ToLowerInvariant()) {
        "x64" { return "DwmProfileMachineX64" }
        "arm64" { return "DwmProfileMachineArm64" }
        default { throw "Unsupported profile machine: $Machine" }
    }
}

function Bool-Literal {
    param([object]$Value)
    if ([bool]$Value) { return "true" }
    return "false"
}

function Normalize-Newline {
    param([string]$Text)
    return ((($Text -replace "`r`n", "`n") -replace "`r", "`n").TrimEnd("`n"))
}

$profiles = Get-Content -Raw -LiteralPath $profilesFile | ConvertFrom-Json
if ($profiles.Count -eq 0) {
    throw "No compiled DWM profiles found in $ProfilesPath"
}

$header = New-Object System.Collections.Generic.List[string]
$header.Add("// Generated from artifacts/profiles/compiled_dwm_profiles.json.")
$header.Add("// Run scripts/generate-dwm-profile-header.ps1 after editing profiles.")
$header.Add("#pragma once")
$header.Add("#include <stdint.h>")
$header.Add("")
$header.Add("enum DwmProfileMachine : uint32_t {")
$header.Add("    DwmProfileMachineX64 = 0x8664,")
$header.Add("    DwmProfileMachineArm64 = 0xAA64,")
$header.Add("};")
$header.Add("")
$header.Add("struct DwmSymbolProfile {")
$header.Add("    const char* id;")
$header.Add("    DwmProfileMachine machine;")
$header.Add("    const char* pdbGuid;")
$header.Add("    uint32_t pdbAge;")
$header.Add("    const char* sha256;")
$header.Add("    uint32_t presentRva;")
$header.Add("    uint32_t directFlipCompatibleRva;")
$header.Add("    uint32_t overlaysEnabledRva;")
$header.Add("    uint32_t overlayTestModeRva;")
$header.Add("    uint32_t displaySwapChainGetBackBufferRva;")
$header.Add("    uint32_t displaySwapChainPresentDFlipRva;")
$header.Add("    uint32_t d3dDevicePresentMpoRva;")
$header.Add("    bool requireOverlaysEnabled;")
$header.Add("};")
$header.Add("")
$header.Add("static const DwmSymbolProfile kDwmSymbolProfiles[] = {")
foreach ($profile in $profiles) {
    $header.Add(("    {{ ""{0}"", {1}, ""{2}"", {3}, ""{4}"", {5}, {6}, {7}, {8}, {9}, {10}, {11}, {12} }}," -f
        $profile.id,
        (Machine-Literal $profile.machine),
        $profile.pdbGuid,
        ([uint32]$profile.pdbAge),
        $profile.sha256,
        (To-HexLiteral $profile.presentRva),
        (To-HexLiteral $profile.directFlipCompatibleRva),
        (To-HexLiteral $profile.overlaysEnabledRva),
        (To-HexLiteral $profile.overlayTestModeRva),
        (To-HexLiteral $profile.displaySwapChainGetBackBufferRva),
        (To-HexLiteral $profile.displaySwapChainPresentDFlipRva),
        (To-HexLiteral $profile.d3dDevicePresentMpoRva),
        (Bool-Literal $profile.requireOverlaysEnabled)))
}
$header.Add("};")
$headerText = ($header -join "`r`n") + "`r`n"

$table = New-Object System.Collections.Generic.List[string]
$table.Add("| Build profile | Arch | PDB GUID | SHA-256 prefix | Present | DirectFlip | OverlaysEnabled | OverlayTestMode | GetBackBuffer | Notes |")
$table.Add("| --- | --- | --- | --- | ---: | ---: | ---: | ---: | ---: | --- |")
foreach ($profile in $profiles) {
    $shaPrefix = if ($profile.sha256.Length -gt 16) { $profile.sha256.Substring(0, 16) } else { $profile.sha256 }
    $notes = if ([string]::IsNullOrWhiteSpace($profile.notes)) { "-" } else { $profile.notes }
    $table.Add(("| ``{0}`` | {1} | ``{2}:{3}`` | ``{4}`` | ``{5}`` | ``{6}`` | ``{7}`` | ``{8}`` | ``{9}`` | {10} |" -f
        $profile.id,
        $profile.machine,
        $profile.pdbGuid,
        $profile.pdbAge,
        $shaPrefix,
        (To-HexLiteral $profile.presentRva),
        (To-HexLiteral $profile.directFlipCompatibleRva),
        (To-HexLiteral $profile.overlaysEnabledRva),
        (To-HexLiteral $profile.overlayTestModeRva),
        (To-HexLiteral $profile.displaySwapChainGetBackBufferRva),
        $notes))
}
$tableText = ($table -join "`r`n") + "`r`n"

if ($Check) {
    $existingHeader = if (Test-Path -LiteralPath $headerFile) { Get-Content -Raw -LiteralPath $headerFile } else { "" }
    $existingTable = if (Test-Path -LiteralPath $tableFile) { Get-Content -Raw -LiteralPath $tableFile } else { "" }
    if ((Normalize-Newline $existingHeader) -ne (Normalize-Newline $headerText)) {
        throw "$HeaderPath is out of date. Run scripts/generate-dwm-profile-header.ps1."
    }
    if ((Normalize-Newline $existingTable) -ne (Normalize-Newline $tableText)) {
        throw "$TablePath is out of date. Run scripts/generate-dwm-profile-header.ps1."
    }
    Write-Host "DWM profile generated files are up to date."
    return
}

$ascii = New-Object System.Text.ASCIIEncoding
[System.IO.File]::WriteAllText($headerFile, $headerText, $ascii)
[System.IO.File]::WriteAllText($tableFile, $tableText, $ascii)
Write-Host "Generated:"
Write-Host "  $HeaderPath"
Write-Host "  $TablePath"
