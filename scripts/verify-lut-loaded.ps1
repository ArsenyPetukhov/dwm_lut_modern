param(
    [switch]$ShowLogTail
)

$ErrorActionPreference = "Stop"
$loaded = $false

Get-Process -Name dwm -ErrorAction SilentlyContinue | ForEach-Object {
    $process = $_
    try {
        foreach ($module in $process.Modules) {
            if ($module.ModuleName -ieq "dwm_lut.dll") {
                $loaded = $true
                Write-Host "dwm_lut.dll loaded in dwm.exe PID $($process.Id) at $($module.BaseAddress)"
            }
        }
    } catch {
        Write-Warning "Could not inspect modules for dwm.exe PID $($process.Id): $($_.Exception.Message)"
    }
}

if (-not $loaded) {
    Write-Host "dwm_lut.dll is not currently loaded in any visible dwm.exe process."
}

if ($ShowLogTail) {
    $log = Join-Path $env:SystemRoot "Temp\dwm_lut.log"
    if (Test-Path -LiteralPath $log) {
        Get-Content -LiteralPath $log -Tail 80
    } else {
        Write-Host "No DWM LUT log found at $log"
    }
}
