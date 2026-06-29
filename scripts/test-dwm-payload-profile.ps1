param(
    [string]$DwmCorePath = "$env:SystemRoot\System32\dwmcore.dll",
    [string]$ProfilesPath = "artifacts\profiles\compiled_dwm_profiles.json"
)

$ErrorActionPreference = "Stop"
$repo = Resolve-Path (Join-Path $PSScriptRoot "..")
$dwmPath = Resolve-Path -LiteralPath $DwmCorePath
$profilesFullPath = Resolve-Path -LiteralPath (Join-Path $repo $ProfilesPath)

function Read-U16([byte[]]$Bytes, [int]$Offset) {
    return [BitConverter]::ToUInt16($Bytes, $Offset)
}

function Read-U32([byte[]]$Bytes, [int]$Offset) {
    return [BitConverter]::ToUInt32($Bytes, $Offset)
}

function Get-MachineName([uint16]$Machine) {
    switch ($Machine) {
        0x8664 { "x64"; break }
        0xAA64 { "arm64"; break }
        default { "0x{0:X4}" -f $Machine; break }
    }
}

function Convert-RvaToOffset($Sections, [uint32]$Rva) {
    foreach ($section in $Sections) {
        $span = [Math]::Max([uint32]$section.VirtualSize, [uint32]$section.RawSize)
        if ($Rva -ge $section.VirtualAddress -and $Rva -lt ($section.VirtualAddress + $span)) {
            return [int]($section.RawPointer + ($Rva - $section.VirtualAddress))
        }
    }
    return -1
}

function Test-RvaInImage($Sections, [uint32]$Rva, [uint32]$SizeOfImage) {
    if ($Rva -eq 0) { return "absent" }
    if ($Rva -ge $SizeOfImage) { return "outside-image" }
    foreach ($section in $Sections) {
        $span = [Math]::Max([uint32]$section.VirtualSize, [uint32]$section.RawSize)
        if ($Rva -ge $section.VirtualAddress -and $Rva -lt ($section.VirtualAddress + $span)) {
            return "ok:" + $section.Name
        }
    }
    return "outside-sections"
}

function Read-PeIdentity([string]$Path) {
    $bytes = [IO.File]::ReadAllBytes($Path)
    if ((Read-U16 $bytes 0) -ne 0x5A4D) { throw "Invalid MZ header: $Path" }

    $nt = [int](Read-U32 $bytes 0x3C)
    if ((Read-U32 $bytes $nt) -ne 0x00004550) { throw "Invalid PE signature: $Path" }

    $coff = $nt + 4
    $machine = Read-U16 $bytes $coff
    $sectionCount = Read-U16 $bytes ($coff + 2)
    $optionalSize = Read-U16 $bytes ($coff + 16)
    $optional = $coff + 20
    $magic = Read-U16 $bytes $optional
    $dataDirectory = if ($magic -eq 0x20B) { $optional + 112 } else { $optional + 96 }
    $sizeOfImage = Read-U32 $bytes ($optional + 56)

    $debugDirectoryRva = Read-U32 $bytes ($dataDirectory + 6 * 8)
    $debugDirectorySize = Read-U32 $bytes ($dataDirectory + 6 * 8 + 4)

    $sections = @()
    $sectionTable = $optional + $optionalSize
    for ($i = 0; $i -lt $sectionCount; $i++) {
        $offset = $sectionTable + 40 * $i
        $rawName = [Text.Encoding]::ASCII.GetString($bytes, $offset, 8).Trim([char]0)
        $sections += [pscustomobject]@{
            Name = $rawName
            VirtualSize = Read-U32 $bytes ($offset + 8)
            VirtualAddress = Read-U32 $bytes ($offset + 12)
            RawSize = Read-U32 $bytes ($offset + 16)
            RawPointer = Read-U32 $bytes ($offset + 20)
        }
    }

    $debugOffset = Convert-RvaToOffset $sections $debugDirectoryRva
    if ($debugOffset -lt 0 -or $debugDirectorySize -lt 28) {
        throw "No usable debug directory: $Path"
    }

    $entryCount = [int]($debugDirectorySize / 28)
    for ($i = 0; $i -lt $entryCount; $i++) {
        $entry = $debugOffset + 28 * $i
        $type = Read-U32 $bytes ($entry + 12)
        $sizeOfData = Read-U32 $bytes ($entry + 16)
        $rawDataPointer = Read-U32 $bytes ($entry + 24)
        if ($type -ne 2 -or $sizeOfData -lt 24) { continue }
        if ((Read-U32 $bytes $rawDataPointer) -ne 0x53445352) { continue }

        $guidBytes = New-Object byte[] 16
        [Array]::Copy($bytes, $rawDataPointer + 4, $guidBytes, 0, 16)
        $guid = ([Guid]::new($guidBytes)).ToString("D").ToUpperInvariant()
        $age = Read-U32 $bytes ($rawDataPointer + 20)
        $hash = (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash
        return [pscustomobject]@{
            Path = $Path
            Machine = Get-MachineName $machine
            PdbGuid = $guid
            PdbAge = $age
            Sha256 = $hash
            SizeOfImage = $sizeOfImage
            Sections = $sections
        }
    }

    throw "No CodeView RSDS record found: $Path"
}

$identity = Read-PeIdentity -Path $dwmPath
$profiles = Get-Content -Raw -LiteralPath $profilesFullPath | ConvertFrom-Json
$profile = $profiles | Where-Object {
    $_.machine -eq $identity.Machine -and
    $_.pdbGuid.ToUpperInvariant() -eq $identity.PdbGuid -and
    [uint32]$_.pdbAge -eq [uint32]$identity.PdbAge
} | Select-Object -First 1

Write-Host "DWM payload: $($identity.Path)"
Write-Host "Machine: $($identity.Machine)"
Write-Host "PDB: $($identity.PdbGuid):$($identity.PdbAge)"
Write-Host "SHA256: $($identity.Sha256)"
Write-Host ("SizeOfImage: 0x{0:X}" -f $identity.SizeOfImage)

if ($null -eq $profile) {
    Write-Host "Profile match: none"
    exit 2
}

Write-Host "Profile match: $($profile.id)"
Write-Host "Engine: $($profile.engineFamily) / $($profile.presentAbi) / $($profile.backbufferStrategy) / $($profile.validationState)"

if ($profile.sha256 -and $profile.sha256.ToUpperInvariant() -ne $identity.Sha256) {
    throw "SHA256 mismatch for matched profile $($profile.id)"
}

$rvaFields = @(
    "presentRva",
    "directFlipCompatibleRva",
    "overlaysEnabledRva",
    "overlayTestModeRva",
    "displaySwapChainGetBackBufferRva",
    "displaySwapChainPresentDFlipRva",
    "d3dDevicePresentMpoRva"
)

$bad = @()
foreach ($field in $rvaFields) {
    $rva = [uint32]$profile.$field
    $status = Test-RvaInImage $identity.Sections $rva $identity.SizeOfImage
    Write-Host ("{0}: 0x{1:X} {2}" -f $field, $rva, $status)
    if ($status -like "outside-*") {
        $bad += "$field=$status"
    }
}

if ($bad.Count -gt 0) {
    throw "Profile $($profile.id) has invalid RVAs: $($bad -join ', ')"
}

Write-Host "Static DWM payload profile validation passed."
