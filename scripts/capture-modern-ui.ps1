param(
    [switch]$UpdateDocs,
    [switch]$DitheringOnly
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
Add-Type -AssemblyName System.Windows.Forms
Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class UiCaptureNative {
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hwnd, out RECT rect);
    [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr hwnd, IntPtr hdcBlt, uint flags);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hwnd, int command);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hwnd);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int width, int height, uint flags);
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

function Assert-VisibleElement(
    [System.Windows.Automation.AutomationElement]$root,
    [string]$name,
    [string]$description
) {
    $condition = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::NameProperty,
        $name)
    $element = $root.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $condition)
    if (-not $element) { throw "$description was not exposed to UI Automation: $name" }
    if ($element.Current.IsOffscreen) { throw "$description is off-screen: $name" }
    $bounds = $element.Current.BoundingRectangle
    if ($bounds.Width -le 0 -or $bounds.Height -le 0) { throw "$description has no visible bounds: $name" }
    return $element
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

            # PrintWindow can return a blank frame on some WPF/GPU combinations. A screen-copy
            # fallback is valid only after this exact HWND is restored and confirmed foreground;
            # otherwise an unrelated window covering the same rectangle would become evidence.
            [void][UiCaptureNative]::ShowWindow($handle, 9)
            [void][UiCaptureNative]::SetForegroundWindow($handle)
            Start-Sleep -Milliseconds 250
            if ([UiCaptureNative]::GetForegroundWindow() -ne $handle) {
                throw "The app window could not be brought to the foreground for capture."
            }
            $graphics.Clear([System.Drawing.Color]::Black)
            $graphics.CopyFromScreen($rect.Left, $rect.Top, 0, 0, $bitmap.Size)
            if (-not (Test-MeaningfulBitmap $bitmap)) { throw "Screen capture produced a blank frame." }
            $bitmap.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
            return
        } catch {
            $lastError = $_
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
    $pages = @(
        [pscustomobject]@{ Slug = "home"; NavLabel = "Home"; Heading = "Your displays, calibrated." }
        [pscustomobject]@{ Slug = "luts"; NavLabel = "LUT library"; Heading = "LUT library" }
        [pscustomobject]@{ Slug = "settings"; NavLabel = "Settings"; Heading = "Make it yours." }
        [pscustomobject]@{ Slug = "about"; NavLabel = "About"; Heading = "PROJECT & PROVENANCE" }
    )

    if ($navigation.Count -ne $pages.Count) {
        throw "Expected $($pages.Count) navigation items, found $($navigation.Count)."
    }
    for ($index = 0; $index -lt $pages.Count; $index++) {
        $page = $pages[$index]
        $item = $navigation[$index]
        if ($item.Current.Name -ne $page.NavLabel) {
            throw "Navigation accessible name mismatch: expected '$($page.NavLabel)', found '$($item.Current.Name)'."
        }
        if ($item.Current.IsOffscreen) { throw "Navigation item is off-screen: $($page.NavLabel)" }
        $bounds = $item.Current.BoundingRectangle
        if ($bounds.Width -le 0 -or $bounds.Height -le 0) {
            throw "Navigation item has no visible bounds: $($page.NavLabel)"
        }
        if (-not $item.Current.IsKeyboardFocusable) {
            throw "Navigation item is not keyboard focusable: $($page.NavLabel)"
        }
    }

    for ($index = 0; $index -lt $pages.Count; $index++) {
        $page = $pages[$index]
        if ($DitheringOnly -and $page.Slug -ne "settings") { continue }
        if ($index -gt 0) {
            $pattern = $navigation[$index].GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern)
            $pattern.Select()
            Start-Sleep -Milliseconds 600
        }
        [void](Assert-VisibleElement $root $page.Heading "Page heading")
        if ($DitheringOnly) {
            [void][UiCaptureNative]::SetWindowPos($process.MainWindowHandle, [IntPtr]::Zero, 0, 0, 1180, 1000, 0x0016)
            Start-Sleep -Milliseconds 400
            Save-WindowImage $process.MainWindowHandle (Join-Path $imageRoot "dithering-blue-noise.png")
            $condition = New-Object System.Windows.Automation.PropertyCondition(
                [System.Windows.Automation.AutomationElement]::AutomationIdProperty, "SdrDitherMode")
            $combo = $root.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $condition)
            if (-not $combo -or $combo.Current.IsOffscreen) { throw "Dithering selector is not visible" }
            $combo.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern).Expand()
            Start-Sleep -Milliseconds 250
            $condition = New-Object System.Windows.Automation.PropertyCondition(
                [System.Windows.Automation.AutomationElement]::NameProperty,
                "Error diffusion (experimental)")
            $option = $combo.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $condition)
            # A WPF data template can expose the text peer first. Select its
            # containing item, whose automation name now uses the model label.
            $selection = $null
            for ($depth = 0; $depth -lt 6 -and $option; $depth++) {
                if ($option.TryGetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern, [ref]$selection)) { break }
                $option = [System.Windows.Automation.TreeWalker]::RawViewWalker.GetParent($option)
            }
            if ($selection) {
                $selection.Select()
            } else {
                # Virtualized popup peers vary across Windows builds. Exercise
                # the normal keyboard path, limited to this process's window.
                [void][UiCaptureNative]::SetForegroundWindow($process.MainWindowHandle)
                if ([UiCaptureNative]::GetForegroundWindow() -ne $process.MainWindowHandle) {
                    throw "Cannot focus the owned settings window for keyboard selection"
                }
                $combo.SetFocus()
                [System.Windows.Forms.SendKeys]::SendWait("{END}{ENTER}")
            }
            $condition = New-Object System.Windows.Automation.PropertyCondition(
                [System.Windows.Automation.AutomationElement]::AutomationIdProperty, "DitherDetails")
            $help = $root.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $condition)
            $help.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern).Expand()
            Start-Sleep -Milliseconds 600
            Save-WindowImage $process.MainWindowHandle (Join-Path $imageRoot "dithering-error-diffusion.png")
            [void][UiCaptureNative]::SetWindowPos($process.MainWindowHandle, [IntPtr]::Zero, 0, 0, 960, 1000, 0x0016)
            Start-Sleep -Milliseconds 400
            Save-WindowImage $process.MainWindowHandle (Join-Path $imageRoot "dithering-narrow.png")
            $saved = Get-Content -Raw (Join-Path $stateRoot "state.json") | ConvertFrom-Json
            if ($saved.SdrDitherMode -ne "error-diffusion" -or $saved.IsEnabled) {
                throw "Dithering selection was not saved in the isolated disabled configuration"
            }
        } else {
            Save-WindowImage $process.MainWindowHandle (Join-Path $imageRoot ($page.Slug + ".png"))
        }
    }
} finally {
    $exitProcess = Start-IsolatedProcess @("--exit")
    if ($exitProcess) { $exitProcess.WaitForExit(10000) | Out-Null; $exitProcess.Dispose() }
    if (-not $process.WaitForExit(10000)) { $process.Kill() }
    $process.Dispose()
}

Write-Host "Current UI screenshots written to $imageRoot"
