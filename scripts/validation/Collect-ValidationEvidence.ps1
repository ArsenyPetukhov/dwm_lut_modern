param(
    [string]$OutputDirectory = ".\artifacts\validation\evidence",
    [string]$AppPath = ".\dist\DwmLutModern-x64-portable-selfcontained\DwmLutModern.exe",
    [string]$PackageDirectory = ".\dist"
)

$ErrorActionPreference = "Stop"
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot "..\.."))
$timestamp = [DateTimeOffset]::Now
$runName = $timestamp.ToString("yyyyMMdd-HHmmss")
$outputRoot = [IO.Path]::GetFullPath($OutputDirectory)
$runRoot = Join-Path $outputRoot $runName
New-Item -ItemType Directory -Force -Path $runRoot | Out-Null

function Invoke-BestEffort([scriptblock]$action) {
    try { return & $action } catch { return [pscustomobject]@{ error = $_.Exception.Message } }
}

function Get-DwmModuleState {
    $items = [Collections.Generic.List[object]]::new()
    foreach ($process in @(Get-Process -Name dwm -ErrorAction SilentlyContinue)) {
        try {
            $module = $process.Modules | Where-Object { $_.ModuleName -ieq "dwm_lut.dll" } | Select-Object -First 1
            $items.Add([pscustomobject]@{
                pid = $process.Id
                inspected = $true
                lutLoaded = $null -ne $module
                modulePath = if ($module) { $module.FileName } else { $null }
            })
        } catch {
            $items.Add([pscustomobject]@{
                pid = $process.Id
                inspected = $false
                lutLoaded = $null
                error = $_.Exception.Message
            })
        } finally {
            $process.Dispose()
        }
    }
    return @($items)
}

function Get-AppStatus([string]$path) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        return [pscustomobject]@{ available = $false; error = "App not found: $path" }
    }

    $stdout = Join-Path $runRoot "app-status.stdout.txt"
    $stderr = Join-Path $runRoot "app-status.stderr.txt"
    try {
        $process = Start-Process -FilePath $path -ArgumentList "--status" -PassThru -Wait `
            -RedirectStandardOutput $stdout -RedirectStandardError $stderr
        $raw = if (Test-Path $stdout) { Get-Content -Raw -LiteralPath $stdout } else { "" }
        $jsonStart = $raw.IndexOf('{')
        if ($jsonStart -ge 0) {
            return [pscustomobject]@{
                available = $true
                exitCode = $process.ExitCode
                status = ($raw.Substring($jsonStart) | ConvertFrom-Json)
            }
        }
        return [pscustomobject]@{
            available = $false
            exitCode = $process.ExitCode
            error = (($raw + " " + (Get-Content -Raw -LiteralPath $stderr -ErrorAction SilentlyContinue)).Trim())
        }
    } catch {
        return [pscustomobject]@{ available = $false; error = $_.Exception.Message }
    }
}

$os = Get-CimInstance Win32_OperatingSystem
$computer = Get-CimInstance Win32_ComputerSystem
$dwmcorePath = Join-Path $env:SystemRoot "System32\dwmcore.dll"
$dwmcore = Get-Item -LiteralPath $dwmcorePath
$packageRoot = [IO.Path]::GetFullPath($PackageDirectory)
$resolvedApp = [IO.Path]::GetFullPath($AppPath)
$gitCommit = Invoke-BestEffort { (& git -C $repo rev-parse HEAD).Trim() }

$evidence = [ordered]@{
    schemaVersion = 1
    capturedAt = $timestamp.ToString("O")
    gitCommit = $gitCommit
    elevation = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole(
        [Security.Principal.WindowsBuiltInRole]::Administrator)
    os = [ordered]@{
        caption = $os.Caption
        version = $os.Version
        buildNumber = $os.BuildNumber
        architecture = $os.OSArchitecture
        model = $computer.Model
    }
    dwmcore = [ordered]@{
        path = $dwmcore.FullName
        fileVersion = $dwmcore.VersionInfo.FileVersion
        productVersion = $dwmcore.VersionInfo.ProductVersion
        sha256 = (Get-FileHash -LiteralPath $dwmcore.FullName -Algorithm SHA256).Hash
    }
    gpus = @(Get-CimInstance Win32_VideoController | ForEach-Object {
        [ordered]@{
            name = $_.Name
            driverVersion = $_.DriverVersion
            pnpDeviceId = $_.PNPDeviceID
            currentResolution = if ($_.CurrentHorizontalResolution) { "$($_.CurrentHorizontalResolution)x$($_.CurrentVerticalResolution)" } else { $null }
        }
    })
    monitors = @(Get-CimInstance Win32_DesktopMonitor | ForEach-Object {
        [ordered]@{
            name = $_.Name
            pnpDeviceId = $_.PNPDeviceID
            status = $_.Status
            width = $_.ScreenWidth
            height = $_.ScreenHeight
        }
    })
    app = Get-AppStatus $resolvedApp
    dwmProcesses = Get-DwmModuleState
    packages = if (Test-Path -LiteralPath $packageRoot) {
        @(Get-ChildItem -LiteralPath $packageRoot -Recurse -File |
          Where-Object { $_.Extension -in @(".zip", ".msi") } |
          ForEach-Object {
            [ordered]@{
                path = $_.FullName
                length = $_.Length
                sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash
            }
        })
    } else { @() }
}

function Copy-LogTail([string]$source, [string]$destination) {
    try {
        if (Test-Path -LiteralPath $source) {
            Get-Content -LiteralPath $source -Tail 300 |
                Set-Content -LiteralPath $destination -Encoding UTF8
        }
    } catch {
        @("Log unavailable: $source", $_.Exception.Message) |
            Set-Content -LiteralPath ($destination + ".error.txt") -Encoding UTF8
    }
}

$dwmLog = Join-Path $env:SystemRoot "Temp\dwm_lut.log"
Copy-LogTail $dwmLog (Join-Path $runRoot "dwm_lut.tail.log")
$guiLog = Join-Path $env:APPDATA "DwmLutModern\dwm_lut_gui.log"
Copy-LogTail $guiLog (Join-Path $runRoot "dwm_lut_gui.tail.log")

$jsonPath = Join-Path $runRoot "evidence.json"
$evidence | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $jsonPath -Encoding UTF8

$markdown = @(
    "# DWM LUT validation capture"
    ""
    "- Captured: $($timestamp.ToString('O'))"
    "- Git commit: $gitCommit"
    "- Windows: $($os.Caption) $($os.Version) (build $($os.BuildNumber))"
    "- DWM core: $($dwmcore.VersionInfo.FileVersion)"
    "- Elevated collector: $($evidence.elevation)"
    "- GPU count: $(@($evidence.gpus).Count)"
    "- Monitor records: $(@($evidence.monitors).Count)"
    "- DWM process records: $(@($evidence.dwmProcesses).Count)"
    "- Package hashes: $(@($evidence.packages).Count)"
    ""
    "This capture proves environment and module state only. Complete the manual visual and measurement matrix in `docs/validation/release-evidence-template.md`."
    ""
    "Review `evidence.json` for device identifiers before publishing it."
)
$markdown | Set-Content -LiteralPath (Join-Path $runRoot "README.md") -Encoding UTF8

$gpuSummary = @($evidence.gpus | ForEach-Object { "- $($_.name) - driver $($_.driverVersion)" })
$monitorSummary = @($evidence.monitors | ForEach-Object {
    $resolution = if ($_.width -and $_.height) { "$($_.width)x$($_.height)" } else { "resolution unavailable" }
    "- $($_.name) - $resolution"
})
$statusSummary = if ($evidence.app.available) {
    $evidence.app.status | ConvertTo-Json -Depth 8
} else {
    "Status unavailable: $($evidence.app.error)"
}
$compatibilityReport = @(
    "# Windows compatibility report"
    ""
    "- DWM LUT Modern version/package: TODO"
    "- Windows: $($os.Caption) $($os.Version) (build $($os.BuildNumber))"
    "- dwmcore.dll: $($dwmcore.VersionInfo.FileVersion)"
    "- dwmcore.dll SHA-256: $($evidence.dwmcore.sha256)"
    "- Validation environment: TODO (VM / physical / Sandbox package-only)"
    "- HDR coverage: TODO"
    "- Multi-monitor coverage: TODO"
    ""
    "## GPUs"
    ""
) + $gpuSummary + @(
    ""
    "## Monitors"
    ""
) + $monitorSummary + @(
    ""
    "## App status"
    ""
    '```json'
    $statusSummary
    '```'
    ""
    "## Result"
    ""
    "TODO: state whether the obvious LUT applied, whether Disable restored the baseline, and what failed."
    ""
    "## Relevant log lines"
    ""
    "TODO: paste sanitized lines from dwm_lut.tail.log."
)
$compatibilityReport | Set-Content -LiteralPath (Join-Path $runRoot "compatibility-report.md") -Encoding UTF8

Write-Host "Validation evidence written to $runRoot"
