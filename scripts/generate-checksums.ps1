param(
    [Parameter(Mandatory = $true)]
    [string]$ReleaseDir
)

$ErrorActionPreference = "Stop"
$resolved = Resolve-Path -LiteralPath $ReleaseDir
$files = Get-ChildItem -LiteralPath $resolved -File | Sort-Object Name

$lines = foreach ($file in $files) {
    $hash = Get-FileHash -Algorithm SHA256 -LiteralPath $file.FullName
    "$($hash.Hash.ToLowerInvariant())  $($file.Name)"
}

$lines | Set-Content -LiteralPath (Join-Path $resolved "SHA256SUMS.txt") -Encoding UTF8
