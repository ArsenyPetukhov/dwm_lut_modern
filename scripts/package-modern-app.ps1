param(
    [string]$Configuration = "Release",
    [switch]$SelfContained
)

# Publishes DwmLutModern.App into a drop-anywhere portable folder under dist\, bundling the
# native shader DLL. With -SelfContained the .NET runtime is included (no runtime prerequisite).

$ErrorActionPreference = "Stop"
$repo = Resolve-Path (Join-Path $PSScriptRoot "..")
$project = Join-Path $repo "src\DwmLutModern.App\DwmLutModern.App.csproj"
$nativeDll = Join-Path $repo "x64\$Configuration\dwm_lut.dll"

if (!(Test-Path $nativeDll)) {
    throw "Native dwm_lut.dll not found at $nativeDll. Run scripts\build-release.ps1 first."
}

$dotnet = (Get-Command dotnet).Source
$outName = if ($SelfContained) { "DwmLutModern-portable-selfcontained" } else { "DwmLutModern-portable" }
$outDir = Join-Path $repo "dist\$outName"

if (Test-Path $outDir) { Remove-Item -Recurse -Force $outDir }

$publishArgs = @(
    "publish", $project,
    "-c", $Configuration,
    "-r", "win-x64",
    "-p:Platform=x64",
    "-o", $outDir
)
if ($SelfContained) {
    $publishArgs += @("--self-contained", "true", "-p:SelfContained=true")
} else {
    $publishArgs += @("--self-contained", "false")
}

& $dotnet @publishArgs
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed with exit code $LASTEXITCODE" }

# Ensure the native DLL is present next to the exe (publish copies it via the csproj None item,
# but re-copy defensively so a stale build can never ship without it).
Copy-Item -LiteralPath $nativeDll -Destination (Join-Path $outDir "dwm_lut.dll") -Force

$hashFile = Join-Path $outDir "SHA256SUMS.txt"
$lines = Get-ChildItem $outDir -File -Recurse | Where-Object { $_.Name -ne "SHA256SUMS.txt" } | ForEach-Object {
    $hash = (Get-FileHash $_.FullName -Algorithm SHA256).Hash
    "$hash  $($_.FullName.Substring($outDir.Length + 1))"
}
Set-Content -Encoding ascii -Path $hashFile -Value $lines

$zip = Join-Path $repo "dist\$outName.zip"
if (Test-Path $zip) { Remove-Item -Force $zip }
Compress-Archive -Path (Join-Path $outDir "*") -DestinationPath $zip -Force

Write-Host "Portable package: $outDir"
Write-Host "Zip: $zip"
