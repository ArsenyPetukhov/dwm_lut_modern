$ErrorActionPreference = "Stop"
$repo = Resolve-Path (Join-Path $PSScriptRoot "..")
$project = Join-Path $repo "src\DwmLutModern.App\DwmLutModern.App.csproj"
$smokeOut = Join-Path $repo "artifacts\ui-smoke"
$isolatedAppData = Join-Path $repo "artifacts\ui-state"
$stateRoot = Join-Path $isolatedAppData "DwmLutModern"
$imageRoot = Join-Path $repo "docs\images"

$repoPrefix = [IO.Path]::GetFullPath($repo).TrimEnd('\') + '\'
foreach ($target in @($smokeOut, $isolatedAppData)) {
    $resolvedTarget = [IO.Path]::GetFullPath($target)
    if (-not $resolvedTarget.StartsWith($repoPrefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to clean UI harness path outside the repository: $resolvedTarget"
    }
}

if (Test-Path $smokeOut) { Remove-Item -Recurse -Force $smokeOut }
if (Test-Path $isolatedAppData) { Remove-Item -Recurse -Force $isolatedAppData }
New-Item -ItemType Directory -Force $smokeOut, $stateRoot, $imageRoot | Out-Null

& dotnet publish $project -c Release -p:Platform=x64 -p:ApplicationManifest=app.smoke.manifest -p:DwmLutSmokeBuild=true --self-contained false -o $smokeOut
if ($LASTEXITCODE -ne 0) { throw "UI smoke publish failed with exit code $LASTEXITCODE" }

@'
{
  "Version": 1,
  "IsEnabled": false,
  "ResolverMode": "auto",
  "StartWithWindows": false,
  "StartMinimized": false,
  "Theme": 2,
  "Language": "en",
  "OnboardingComplete": true,
  "Monitors": [],
  "Library": [
    { "Path": "C:\\Calibration\\Calibrated_sRGB.cube", "Name": "Calibrated sRGB", "Kind": 1, "LastUsedTicks": 2 },
    { "Path": "C:\\Calibration\\HDR1000_PQ.cube", "Name": "HDR1000 PQ", "Kind": 2, "LastUsedTicks": 1 }
  ]
}
'@ | Set-Content -Encoding UTF8 (Join-Path $stateRoot "state.json")

Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName System.Drawing
Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class UiCaptureNative {
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hwnd, out RECT rect);
}
'@

function Start-IsolatedProcess([string[]]$arguments) {
    $info = New-Object System.Diagnostics.ProcessStartInfo
    $info.FileName = Join-Path $smokeOut "DwmLutModern.exe"
    $info.UseShellExecute = $false
    $info.WorkingDirectory = $smokeOut
    $info.EnvironmentVariables["APPDATA"] = $isolatedAppData
    $info.EnvironmentVariables["DWM_LUT_STATE_ROOT"] = $stateRoot
    $info.Arguments = ($arguments | ForEach-Object { '"' + $_.Replace('"', '\"') + '"' }) -join ' '
    return [System.Diagnostics.Process]::Start($info)
}

function Save-WindowImage([IntPtr]$handle, [string]$path) {
    $rect = New-Object UiCaptureNative+RECT
    if (-not [UiCaptureNative]::GetWindowRect($handle, [ref]$rect)) { throw "GetWindowRect failed." }
    $width = $rect.Right - $rect.Left
    $height = $rect.Bottom - $rect.Top
    $bitmap = New-Object System.Drawing.Bitmap $width, $height
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    try {
        $graphics.CopyFromScreen($rect.Left, $rect.Top, 0, 0, $bitmap.Size)
        $bitmap.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    } finally {
        $graphics.Dispose()
        $bitmap.Dispose()
    }
}

$process = Start-IsolatedProcess @()
try {
    $deadline = [DateTime]::UtcNow.AddSeconds(20)
    do {
        Start-Sleep -Milliseconds 200
        $process.Refresh()
    } while ($process.MainWindowHandle -eq [IntPtr]::Zero -and [DateTime]::UtcNow -lt $deadline -and -not $process.HasExited)

    if ($process.HasExited) { throw "UI smoke app exited with code $($process.ExitCode)." }
    if ($process.MainWindowHandle -eq [IntPtr]::Zero) { throw "UI smoke app did not create a main window." }

    $root = [System.Windows.Automation.AutomationElement]::FromHandle($process.MainWindowHandle)
    $condition = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
        [System.Windows.Automation.ControlType]::RadioButton)
    $navigation = $root.FindAll([System.Windows.Automation.TreeScope]::Descendants, $condition)
    $names = @("home", "luts", "settings", "about")

    for ($index = 0; $index -lt $names.Count; $index++) {
        if ($index -gt 0) {
            $pattern = $navigation[$index].GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern)
            $pattern.Select()
            Start-Sleep -Milliseconds 600
        }
        Save-WindowImage $process.MainWindowHandle (Join-Path $imageRoot ($names[$index] + ".png"))
    }
} finally {
    $exitProcess = Start-IsolatedProcess @("--exit")
    if ($exitProcess) { $exitProcess.WaitForExit(10000) | Out-Null; $exitProcess.Dispose() }
    if (-not $process.WaitForExit(10000)) { $process.Kill() }
    $process.Dispose()
}

Write-Host "Current UI screenshots written to $imageRoot"
