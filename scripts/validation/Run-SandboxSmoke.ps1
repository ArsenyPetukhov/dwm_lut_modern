param(
    [string]$PackageRoot = "C:\DwmLutPackage",
    [string]$EvidenceRoot = "C:\DwmLutEvidence"
)

$ErrorActionPreference = "Stop"
$runRoot = Join-Path $EvidenceRoot ([DateTimeOffset]::Now.ToString("yyyyMMdd-HHmmss"))
$workRoot = "C:\DwmLutWork"
New-Item -ItemType Directory -Force -Path $runRoot, $workRoot | Out-Null

function Get-PeMachine([string]$path) {
    $stream = [IO.File]::OpenRead($path)
    $reader = [IO.BinaryReader]::new($stream)
    try {
        if ($reader.ReadUInt16() -ne 0x5A4D) { throw "Not an MZ executable: $path" }
        $stream.Position = 0x3C
        $peOffset = $reader.ReadInt32()
        $stream.Position = $peOffset
        if ($reader.ReadUInt32() -ne 0x00004550) { throw "Invalid PE header: $path" }
        return ('0x{0:X4}' -f $reader.ReadUInt16())
    } finally {
        $reader.Dispose()
        $stream.Dispose()
    }
}

$zip = Get-ChildItem -LiteralPath $PackageRoot -File -Filter "DwmLutModern*x64*.zip" | Select-Object -First 1
if ($zip) {
    Expand-Archive -LiteralPath $zip.FullName -DestinationPath $workRoot -Force
    $app = Get-ChildItem -LiteralPath $workRoot -Recurse -File -Filter "DwmLutModern.exe" | Select-Object -First 1
} else {
    $app = Get-ChildItem -LiteralPath $PackageRoot -Recurse -File -Filter "DwmLutModern.exe" | Select-Object -First 1
}

if (-not $app) { throw "DwmLutModern.exe was not found under $PackageRoot" }
$native = Get-ChildItem -LiteralPath $app.Directory.FullName -File -Filter "dwm_lut.dll" | Select-Object -First 1
if (-not $native) { throw "The package is missing dwm_lut.dll beside DwmLutModern.exe." }

$result = [ordered]@{
    capturedAt = [DateTimeOffset]::Now.ToString("O")
    windows = (Get-CimInstance Win32_OperatingSystem | Select-Object Caption,Version,BuildNumber,OSArchitecture)
    appPath = $app.FullName
    appMachine = Get-PeMachine $app.FullName
    nativePath = $native.FullName
    nativeMachine = Get-PeMachine $native.FullName
    appSha256 = (Get-FileHash -LiteralPath $app.FullName -Algorithm SHA256).Hash
    nativeSha256 = (Get-FileHash -LiteralPath $native.FullName -Algorithm SHA256).Hash
    packageSha256 = if ($zip) { (Get-FileHash -LiteralPath $zip.FullName -Algorithm SHA256).Hash } else { $null }
}

if ($result.appMachine -ne $result.nativeMachine) {
    throw "Managed launcher/native payload architecture mismatch: $($result.appMachine) vs $($result.nativeMachine)"
}

$result | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $runRoot "sandbox-smoke.json") -Encoding UTF8
@(
    "DWM LUT Windows Sandbox smoke test"
    ""
    "The package and PE architecture checks passed."
    "Accept the UAC prompt, verify the UI pages render, then close the app and Sandbox."
    ""
    "Important: Sandbox proves clean-machine startup and package integrity only."
    "Its virtual display cannot prove physical HDR, calibration accuracy, multi-monitor routing, or real GPU-driver behavior."
    "Use docs\validation-lab.md for Hyper-V and physical-hardware gates."
) | Set-Content -LiteralPath (Join-Path $runRoot "README.txt") -Encoding UTF8

Start-Process -FilePath $app.FullName
Start-Process explorer.exe -ArgumentList $runRoot
