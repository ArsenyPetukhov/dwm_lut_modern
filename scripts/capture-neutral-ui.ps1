param([string]$Version = "5.1.0", [switch]$UpdateDocs)
$ErrorActionPreference = "Stop"
$repo = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$smokeOut = Join-Path $repo "artifacts\ui-smoke"
$isolatedAppData = Join-Path $repo "artifacts\ui-state-neutral"
$stateRoot = Join-Path $isolatedAppData "DwmLutModern"
$imageRoot = if ($UpdateDocs) { Join-Path $repo "docs\images" } else { Join-Path $repo "artifacts\ui-captures" }
foreach ($owned in @($smokeOut, $isolatedAppData)) {
    if (Test-Path $owned) { Remove-Item -Recurse -Force $owned }
}
New-Item -ItemType Directory -Force $smokeOut, $stateRoot, $imageRoot | Out-Null
& dotnet publish (Join-Path $repo "src\DwmLutModern.App\DwmLutModern.App.csproj") -c Release -p:Platform=x64 "-p:Version=$Version" -p:ApplicationManifest=app.smoke.manifest -p:DwmLutSmokeBuild=true --self-contained false -o $smokeOut
if ($LASTEXITCODE -ne 0) { throw "UI rendering build failed" }
@{
    Version=1; IsEnabled=$false; ResolverMode="auto"; SdrDitherMode="blue-noise"
    StartWithWindows=$false; StartMinimized=$false; Theme=1; Language="en"
    OnboardingComplete=$true; Monitors=@(); Library=@()
} | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $stateRoot "state.json") -Encoding UTF8

Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName PresentationFramework
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

function Set-CaptureWindowSize([IntPtr]$handle, [int]$width, [int]$height) {
    # Hosted Windows desktops can be smaller than the app's normal window.
    # Move the owned window fully onto the desktop before capturing it.
    $area = [System.Windows.SystemParameters]::WorkArea
    [void][UiCaptureNative]::SetWindowPos($handle, [IntPtr]::Zero,
        [int]$area.Left, [int]$area.Top,
        [Math]::Min($width, [int]$area.Width), [Math]::Min($height, [int]$area.Height), 0x0014)
    Start-Sleep -Milliseconds 600
}



function Find-Element([string]$id, $within=$script:root) {
    $condition = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::AutomationIdProperty, $id)
    return $within.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $condition)
}
function Select-Nav([string]$id) {
    $node = Find-Element $id
    if (-not $node) { throw "Missing navigation $id" }
    $node.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select()
    Start-Sleep -Milliseconds 250
}
function Scroll-Settings([double]$percent) {
    $viewer = Find-Element "SettingsScroll"
    $pattern = $null
    if ($viewer -and $viewer.TryGetCurrentPattern([System.Windows.Automation.ScrollPattern]::Pattern, [ref]$pattern) -and $pattern.Current.VerticallyScrollable) {
        $pattern.SetScrollPercent(-1, $percent)
        Start-Sleep -Milliseconds 150
    }
}
function Make-Visible([string]$id) {
    $node = Find-Element $id
    foreach ($percent in @(0,20,40,60,80,100)) {
        if ($node -and -not $node.Current.IsOffscreen) { return $node }
        Scroll-Settings $percent
        $node = Find-Element $id
    }
    throw "Control is not visible: $id"
}
function Select-Combo([string]$id, [int]$index) {
    $node = Make-Visible $id
    [void][UiCaptureNative]::SetForegroundWindow($script:handle)
    if ([UiCaptureNative]::GetForegroundWindow() -ne $script:handle) { throw "Owned window is not foreground" }
    $node.SetFocus()
    $keys = "{HOME}" + ("{DOWN}" * $index) + "{ENTER}"
    [System.Windows.Forms.SendKeys]::SendWait($keys)
    Start-Sleep -Milliseconds 300
}
function Saved-State { return Get-Content -Raw (Join-Path $stateRoot "state.json") | ConvertFrom-Json }
function Capture([string]$name) { Save-WindowImage $script:handle (Join-Path $imageRoot ($name + ".png")) }
function Find-Dialog {
    $conditions = New-Object System.Windows.Automation.AndCondition(
        (New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ProcessIdProperty, $script:process.Id)),
        (New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::AutomationIdProperty, "ShortcutCaptureWindow")))
    $deadline = [DateTime]::UtcNow.AddSeconds(8)
    do {
        $dialog = [System.Windows.Automation.AutomationElement]::RootElement.FindFirst([System.Windows.Automation.TreeScope]::Children, $conditions)
        if ($dialog) { return $dialog }
        Start-Sleep -Milliseconds 150
    } while ([DateTime]::UtcNow -lt $deadline)
    throw "Shortcut dialog did not open"
}
function Record-Shortcut([string]$buttonId, [string]$keys, [switch]$Cancel) {
    $button = Make-Visible $buttonId
    $button.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
    $dialog = Find-Dialog
    $target = Find-Element "CaptureTarget" $dialog
    $target.SetFocus()
    [System.Windows.Forms.SendKeys]::SendWait($keys)
    Start-Sleep -Milliseconds 200
    if ($Cancel) {
        [System.Windows.Forms.SendKeys]::SendWait("{ESC}")
    } else {
        $save = Find-Element "SaveButton" $dialog
        if (-not $save.Current.IsEnabled) { throw "A valid recorded shortcut did not enable Save" }
        $save.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
    }
    Start-Sleep -Milliseconds 350
}
$script:process = Start-IsolatedProcess @()
try {
    $deadline = [DateTime]::UtcNow.AddSeconds(20)
    do {
        Start-Sleep -Milliseconds 150
        $script:process.Refresh()
    } while ($script:process.MainWindowHandle -eq [IntPtr]::Zero -and [DateTime]::UtcNow -lt $deadline -and -not $script:process.HasExited)
    if ($script:process.HasExited) { throw "UI app exited: $($script:process.ExitCode)" }
    $script:handle = $script:process.MainWindowHandle
    if ($script:handle -eq [IntPtr]::Zero) { throw "No application window" }
    Set-CaptureWindowSize $script:handle 1120 900
    $script:root = [System.Windows.Automation.AutomationElement]::FromHandle($script:handle)

    Select-Nav "NavDisplays"; Capture "displays-en-light"
    Select-Nav "NavLuts"; Capture "library-en-light"
    Select-Nav "NavAbout"; Capture "about-en-light"
    $codes = @("en","ru","es","fr","de","pt","it","zh","ja","ko")
    for ($i=0; $i -lt $codes.Count; $i++) {
        Select-Nav "NavSettings"
        Select-Combo "LanguageSelector" $i
        if ((Saved-State).Language -ne $codes[$i]) { throw "Language was not saved: $($codes[$i])" }
        Scroll-Settings 0
        Capture ("settings-" + $codes[$i] + "-light")
        Select-Nav "NavDocumentation"
        Capture ("documentation-" + $codes[$i] + "-light")
    }
    Select-Nav "NavSettings"
    Select-Combo "LanguageSelector" 0
    Select-Combo "SdrDitherMode" 1
    if ((Saved-State).SdrDitherMode -ne "error-diffusion") { throw "Dithering mode was not saved" }
    Scroll-Settings 0; Capture "settings-error-diffusion-light"
    Set-CaptureWindowSize $script:handle 960 900
    Capture "settings-narrow-light"
    Select-Combo "ThemeSelector" 2
    Scroll-Settings 0; Capture "settings-en-dark"
    Select-Nav "NavDocumentation"; Capture "documentation-en-dark"
    Select-Nav "NavDisplays"; Capture "displays-en-dark"
    Select-Nav "NavSettings"
    Select-Combo "ThemeSelector" 1

    Record-Shortcut "EditToggleShortcut" "^%+{F9}"
    $saved = Saved-State
    if ($saved.ToggleHotkey.VirtualKey -ne 0x78 -or $saved.ToggleHotkey.Modifiers -ne 7) { throw "Toggle shortcut not saved" }
    Record-Shortcut "EditCycleShortcut" "^%+{F9}"
    if ((Saved-State).CycleHotkey) { throw "Duplicate shortcut was persisted" }
    Record-Shortcut "EditCycleShortcut" "^%+{F10}"
    if ((Saved-State).CycleHotkey.VirtualKey -ne 0x79) { throw "Cycle shortcut not saved" }
    Record-Shortcut "EditToggleShortcut" "^%+{F11}" -Cancel
    if ((Saved-State).ToggleHotkey.VirtualKey -ne 0x78) { throw "Cancel changed saved shortcut" }
    Capture "shortcuts-assigned-light"
    foreach ($id in @("RemoveToggleShortcut","RemoveCycleShortcut")) {
        $button = Make-Visible $id
        $button.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
        Start-Sleep -Milliseconds 200
    }
    $saved = Saved-State
    if ($saved.ToggleHotkey -or $saved.CycleHotkey -or $saved.IsEnabled) { throw "Shortcut removal or disabled intent failed" }
    Capture "shortcuts-cleared-light"
    @{ source="actual WPF controls"; languages=$codes; shortcutSave=$true; duplicateRejected=$true; cancelPreserved=$true; removePersisted=$true; injectedIntoDwm=$false } |
        ConvertTo-Json -Depth 3 | Set-Content (Join-Path $imageRoot "controls.json") -Encoding UTF8
} finally {
    $exitProcess = Start-IsolatedProcess @("--exit")
    if ($exitProcess) { $exitProcess.WaitForExit(10000) | Out-Null; $exitProcess.Dispose() }
    if (-not $script:process.WaitForExit(10000)) { $script:process.Kill() }
    $script:process.Dispose()
}
