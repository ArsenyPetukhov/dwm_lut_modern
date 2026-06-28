param(
    [string]$OutputPath = ".\system-info.txt"
)

$ErrorActionPreference = "Stop"
$lines = New-Object System.Collections.Generic.List[string]

$os = Get-CimInstance Win32_OperatingSystem
$lines.Add("Windows caption: $($os.Caption)")
$lines.Add("Windows version: $($os.Version)")
$lines.Add("Windows build: $($os.BuildNumber)")
$lines.Add("OS architecture: $($os.OSArchitecture)")

$lines.Add("")
$lines.Add("GPU:")
Get-CimInstance Win32_VideoController | ForEach-Object {
    $lines.Add("  Name: $($_.Name)")
    $lines.Add("  DriverVersion: $($_.DriverVersion)")
    $lines.Add("  PNPDeviceID: $($_.PNPDeviceID)")
}

$lines.Add("")
$lines.Add("DWM LUT log path: $env:SystemRoot\Temp\dwm_lut.log")

$lines | Set-Content -LiteralPath $OutputPath -Encoding UTF8
Write-Host "Wrote $OutputPath"
