param(
    [string]$OutputDirectory = ".\artifacts\validation\diagnostic-luts"
)

$ErrorActionPreference = "Stop"
$culture = [Globalization.CultureInfo]::InvariantCulture
$output = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Force -Path $output | Out-Null

function Format-Channel([double]$value) {
    return $value.ToString("0.000000", $culture)
}

function Write-Lut([string]$path, [string]$title, [scriptblock]$transform) {
    $lines = [Collections.Generic.List[string]]::new()
    $lines.Add("TITLE `"$title`"")
    $lines.Add("LUT_3D_SIZE 2")
    $lines.Add("DOMAIN_MIN 0.0 0.0 0.0")
    $lines.Add("DOMAIN_MAX 1.0 1.0 1.0")
    $lines.Add("")

    # .cube order: blue changes fastest, then green, then red.
    foreach ($red in @(0.0, 1.0)) {
        foreach ($green in @(0.0, 1.0)) {
            foreach ($blue in @(0.0, 1.0)) {
                $mapped = & $transform $red $green $blue
                $lines.Add("$(Format-Channel $mapped[0]) $(Format-Channel $mapped[1]) $(Format-Channel $mapped[2])")
            }
        }
    }

    $lines | Set-Content -LiteralPath $path -Encoding UTF8
}

$identity = Join-Path $output "identity-2.cube"
$obvious = Join-Path $output "diagnostic-red-invert-2.cube"

Write-Lut $identity "DWM LUT identity validation" {
    param($red, $green, $blue)
    @($red, $green, $blue)
}

Write-Lut $obvious "DWM LUT obvious red inversion - validation only" {
    param($red, $green, $blue)
    @((1.0 - $red), $green, $blue)
}

Write-Host "Created diagnostic LUTs:"
Write-Host "  $identity"
Write-Host "  $obvious"
Write-Warning "The red-inversion LUT is intentionally wrong for visual routing tests. Never use it as a calibration."
