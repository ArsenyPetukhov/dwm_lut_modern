param(
    [string]$ProfilesPath = "artifacts\profiles\compiled_dwm_profiles.json",
    [string]$HeaderPath = "src\lutdwm\DwmProfiles.generated.h",
    [string]$CSharpPath = "src\DwmLutGUI\DwmLutGUI\DwmProfiles.generated.cs",
    [string]$TablePath = "artifacts\profiles\dwm_profiles_table.md",
    [switch]$Check
)

$ErrorActionPreference = "Stop"
$repo = Resolve-Path (Join-Path $PSScriptRoot "..")
$profilesFile = Join-Path $repo $ProfilesPath
$headerFile = Join-Path $repo $HeaderPath
$csharpFile = Join-Path $repo $CSharpPath
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

function String-Or-Empty {
    param([object]$Value)
    if ($null -eq $Value) { return "" }
    return [string]$Value
}

function CSharp-String {
    param([object]$Value)
    $text = String-Or-Empty $Value
    return '"' + ($text -replace '\\', '\\' -replace '"', '\"') + '"'
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
$header.Add("    const char* engineFamily;")
$header.Add("    const char* presentAbi;")
$header.Add("    const char* swapchainStrategy;")
$header.Add("    const char* backbufferStrategy;")
$header.Add("    const char* overlayStrategy;")
$header.Add("    const char* monitorIdentityStrategy;")
$header.Add("    const char* validationState;")
$header.Add("};")
$header.Add("")
$header.Add("static const DwmSymbolProfile kDwmSymbolProfiles[] = {")
foreach ($profile in $profiles) {
    $header.Add(("    {{ ""{0}"", {1}, ""{2}"", {3}, ""{4}"", {5}, {6}, {7}, {8}, {9}, {10}, {11}, {12}, ""{13}"", ""{14}"", ""{15}"", ""{16}"", ""{17}"", ""{18}"", ""{19}"" }}," -f
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
        (Bool-Literal $profile.requireOverlaysEnabled),
        (String-Or-Empty $profile.engineFamily),
        (String-Or-Empty $profile.presentAbi),
        (String-Or-Empty $profile.swapchainStrategy),
        (String-Or-Empty $profile.backbufferStrategy),
        (String-Or-Empty $profile.overlayStrategy),
        (String-Or-Empty $profile.monitorIdentityStrategy),
        (String-Or-Empty $profile.validationState)))
}
$header.Add("};")
$headerText = ($header -join "`r`n") + "`r`n"

$csharp = New-Object System.Collections.Generic.List[string]
$csharp.Add("// Generated from artifacts/profiles/compiled_dwm_profiles.json.")
$csharp.Add("// Run scripts/generate-dwm-profile-header.ps1 after editing profiles.")
$csharp.Add("namespace DwmLutGUI")
$csharp.Add("{")
$csharp.Add("    internal sealed class DwmKnownProfile")
$csharp.Add("    {")
$csharp.Add("        public DwmKnownProfile(string id, string machine, string pdbGuid, uint pdbAge, string sha256, string engineFamily, string presentAbi, string swapchainStrategy, string backbufferStrategy, string overlayStrategy, string monitorIdentityStrategy, string validationState)")
$csharp.Add("        {")
$csharp.Add("            Id = id;")
$csharp.Add("            Machine = machine;")
$csharp.Add("            PdbGuid = pdbGuid;")
$csharp.Add("            PdbAge = pdbAge;")
$csharp.Add("            Sha256 = sha256;")
$csharp.Add("            EngineFamily = engineFamily;")
$csharp.Add("            PresentAbi = presentAbi;")
$csharp.Add("            SwapchainStrategy = swapchainStrategy;")
$csharp.Add("            BackbufferStrategy = backbufferStrategy;")
$csharp.Add("            OverlayStrategy = overlayStrategy;")
$csharp.Add("            MonitorIdentityStrategy = monitorIdentityStrategy;")
$csharp.Add("            ValidationState = validationState;")
$csharp.Add("        }")
$csharp.Add("")
$csharp.Add("        public string Id { get; }")
$csharp.Add("        public string Machine { get; }")
$csharp.Add("        public string PdbGuid { get; }")
$csharp.Add("        public uint PdbAge { get; }")
$csharp.Add("        public string Sha256 { get; }")
$csharp.Add("        public string EngineFamily { get; }")
$csharp.Add("        public string PresentAbi { get; }")
$csharp.Add("        public string SwapchainStrategy { get; }")
$csharp.Add("        public string BackbufferStrategy { get; }")
$csharp.Add("        public string OverlayStrategy { get; }")
$csharp.Add("        public string MonitorIdentityStrategy { get; }")
$csharp.Add("        public string ValidationState { get; }")
$csharp.Add("    }")
$csharp.Add("")
$csharp.Add("    internal static class DwmKnownProfiles")
$csharp.Add("    {")
$csharp.Add("        public static readonly DwmKnownProfile[] All =")
$csharp.Add("        {")
foreach ($profile in $profiles) {
    $csharp.Add(("            new DwmKnownProfile({0}, {1}, {2}, {3}u, {4}, {5}, {6}, {7}, {8}, {9}, {10}, {11})," -f
        (CSharp-String $profile.id),
        (CSharp-String $profile.machine),
        (CSharp-String $profile.pdbGuid),
        ([uint32]$profile.pdbAge),
        (CSharp-String $profile.sha256),
        (CSharp-String $profile.engineFamily),
        (CSharp-String $profile.presentAbi),
        (CSharp-String $profile.swapchainStrategy),
        (CSharp-String $profile.backbufferStrategy),
        (CSharp-String $profile.overlayStrategy),
        (CSharp-String $profile.monitorIdentityStrategy),
        (CSharp-String $profile.validationState)))
}
$csharp.Add("        };")
$csharp.Add("    }")
$csharp.Add("}")
$csharpText = ($csharp -join "`r`n") + "`r`n"

$table = New-Object System.Collections.Generic.List[string]
$table.Add("| Build profile | Arch | PDB GUID | SHA-256 prefix | Engine | Validation | Present | DirectFlip | OverlaysEnabled | OverlayTestMode | GetBackBuffer | Notes |")
$table.Add("| --- | --- | --- | --- | --- | --- | ---: | ---: | ---: | ---: | ---: | --- |")
foreach ($profile in $profiles) {
    $shaPrefix = if ($profile.sha256.Length -gt 16) { $profile.sha256.Substring(0, 16) } else { $profile.sha256 }
    $notes = if ([string]::IsNullOrWhiteSpace($profile.notes)) { "-" } else { $profile.notes }
    $table.Add(("| ``{0}`` | {1} | ``{2}:{3}`` | ``{4}`` | ``{5}`` | ``{6}`` | ``{7}`` | ``{8}`` | ``{9}`` | ``{10}`` | ``{11}`` | {12} |" -f
        $profile.id,
        $profile.machine,
        $profile.pdbGuid,
        $profile.pdbAge,
        $shaPrefix,
        (String-Or-Empty $profile.engineFamily),
        (String-Or-Empty $profile.validationState),
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
    $existingCSharp = if (Test-Path -LiteralPath $csharpFile) { Get-Content -Raw -LiteralPath $csharpFile } else { "" }
    $existingTable = if (Test-Path -LiteralPath $tableFile) { Get-Content -Raw -LiteralPath $tableFile } else { "" }
    if ((Normalize-Newline $existingHeader) -ne (Normalize-Newline $headerText)) {
        throw "$HeaderPath is out of date. Run scripts/generate-dwm-profile-header.ps1."
    }
    if ((Normalize-Newline $existingCSharp) -ne (Normalize-Newline $csharpText)) {
        throw "$CSharpPath is out of date. Run scripts/generate-dwm-profile-header.ps1."
    }
    if ((Normalize-Newline $existingTable) -ne (Normalize-Newline $tableText)) {
        throw "$TablePath is out of date. Run scripts/generate-dwm-profile-header.ps1."
    }
    Write-Host "DWM profile generated files are up to date."
    return
}

$ascii = New-Object System.Text.ASCIIEncoding
[System.IO.File]::WriteAllText($headerFile, $headerText, $ascii)
[System.IO.File]::WriteAllText($csharpFile, $csharpText, $ascii)
[System.IO.File]::WriteAllText($tableFile, $tableText, $ascii)
Write-Host "Generated:"
Write-Host "  $HeaderPath"
Write-Host "  $CSharpPath"
Write-Host "  $TablePath"
