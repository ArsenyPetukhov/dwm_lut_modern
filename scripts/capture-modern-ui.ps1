param(
    [switch]$UpdateDocs
)

$ErrorActionPreference = "Stop"
$repo = Resolve-Path (Join-Path $PSScriptRoot "..")
$project = Join-Path $repo "src\DwmLutModern.App\DwmLutModern.App.csproj"
$smokeOut = Join-Path $repo "artifacts\ui-smoke"
$isolatedAppData = Join-Path $repo "artifacts\ui-state"
$stateRoot = Join-Path $isolatedAppData "DwmLutModern"
$imageRoot = if ($UpdateDocs) {
    Join-Path $repo "docs\images"
} else {
    Join-Path $repo "artifacts\ui-captures"
}

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
    [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr hwnd, IntPtr hdcBlt, uint flags);
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

function Test-MeaningfulBitmap([System.Drawing.Bitmap]$bitmap) {
    $colors = [Collections.Generic.HashSet[int]]::new()
    for ($x = 0; $x -lt $bitmap.Width; $x += [Math]::Max(1, [int]($bitmap.Width / 12))) {
        for ($y = 0; $y -lt $bitmap.Height; $y += [Math]::Max(1, [int]($bitmap.Height / 8))) {
            [void]$colors.Add($bitmap.GetPixel($x, $y).ToArgb())
        }
    }
    return $colors.Count -ge 6
}

function Save-WindowImage([IntPtr]$handle, [string]$path) {
    $rect = New-Object UiCaptureNative+RECT
    if (-not [UiCaptureNative]::GetWindowRect($handle, [ref]$rect)) { throw "GetWindowRect failed." }
    $width = $rect.Right - $rect.Left
    $height = $rect.Bottom - $rect.Top
    if ($width -le 0 -or $height -le 0) { throw "The app window has an invalid capture rectangle." }

    $lastError = $null
    for ($attempt = 1; $attempt -le 8; $attempt++) {
        $bitmap = New-Object System.Drawing.Bitmap $width, $height
        $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
        try {
            # CopyFromScreen can briefly lose its desktop DC while DWM finishes a transition.
            # Retry the complete bitmap operation rather than publishing a black/partial frame.
            $graphics.CopyFromScreen($rect.Left, $rect.Top, 0, 0, $bitmap.Size)
            if (-not (Test-MeaningfulBitmap $bitmap)) { throw "Screen capture produced a blank frame." }
            $bitmap.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
            return
        } catch {
            $lastError = $_
            $hdc = [IntPtr]::Zero
            try {
                $hdc = $graphics.GetHdc()
                if ([UiCaptureNative]::PrintWindow($handle, $hdc, 2)) {
                    $graphics.ReleaseHdc($hdc)
                    $hdc = [IntPtr]::Zero
                    if (Test-MeaningfulBitmap $bitmap) {
                        $bitmap.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
                        return
                    }
                }
            } finally {
                if ($hdc -ne [IntPtr]::Zero) { $graphics.ReleaseHdc($hdc) }
            }
            Start-Sleep -Milliseconds 250
        } finally {
            $graphics.Dispose()
            $bitmap.Dispose()
        }
    }

    throw "Window capture failed after 8 attempts: $($lastError.Exception.Message)"
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
