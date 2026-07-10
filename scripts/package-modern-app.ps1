param(
    [string]$Configuration = "Release",
    [ValidateSet("x64", "ARM64")]
    [string]$Platform = "x64",
    [string]$Version = "5.0.0",
    [switch]$SelfContained
)

# Publishes DwmLutModern.App into a drop-anywhere portable folder under dist\, bundling the
# native shader DLL. With -SelfContained the .NET runtime is included (no runtime prerequisite).

$ErrorActionPreference = "Stop"
$repo = Resolve-Path (Join-Path $PSScriptRoot "..")
$project = Join-Path $repo "src\DwmLutModern.App\DwmLutModern.App.csproj"
$nativeDll = Join-Path $repo "$Platform\$Configuration\dwm_lut.dll"

if (!(Test-Path $nativeDll)) {
    throw "Native dwm_lut.dll not found at $nativeDll. Run scripts\build-release.ps1 first."
}

$dotnetCommand = Get-Command dotnet -ErrorAction SilentlyContinue
if (-not $dotnetCommand) { throw "dotnet SDK not found; install .NET 8 or newer." }
$dotnet = $dotnetCommand.Source
$architecture = $Platform.ToLowerInvariant()
$outName = if ($SelfContained) { "DwmLutModern-$architecture-portable-selfcontained" } else { "DwmLutModern-$architecture-portable" }
$outDir = Join-Path $repo "dist\$outName"

if (Test-Path $outDir) { Remove-Item -Recurse -Force $outDir }

$publishArgs = @(
    "publish", $project,
    "-c", $Configuration,
    "-r", "win-$architecture",
    "-p:Platform=$Platform",
    "-p:Version=$Version",
    "-p:IncludeSourceRevisionInInformationalVersion=false",
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
Copy-Item -LiteralPath (Join-Path $repo "LICENSE") -Destination (Join-Path $outDir "LICENSE.txt") -Force
Copy-Item -LiteralPath (Join-Path $repo "NOTICE.md") -Destination (Join-Path $outDir "NOTICE.md") -Force
Copy-Item -LiteralPath (Join-Path $repo "docs\USER-GUIDE.md") -Destination (Join-Path $outDir "USER-GUIDE.md") -Force
$guideImages = Join-Path $outDir "images"
New-Item -ItemType Directory -Force -Path $guideImages | Out-Null
Copy-Item -Path (Join-Path $repo "docs\images\*.png") -Destination $guideImages -Force

$hashFile = Join-Path $outDir "SHA256SUMS.txt"
$lines = Get-ChildItem $outDir -File -Recurse | Where-Object { $_.Name -ne "SHA256SUMS.txt" } | ForEach-Object {
    $hash = (Get-FileHash $_.FullName -Algorithm SHA256).Hash
    "$hash  $($_.FullName.Substring($outDir.Length + 1))"
}
Set-Content -Encoding ascii -Path $hashFile -Value $lines

$zip = Join-Path $repo "dist\$outName.zip"
if (Test-Path $zip) { Remove-Item -Force $zip }
Compress-Archive -Path (Join-Path $outDir "*") -DestinationPath $zip -Force
$zipHash = (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash
Set-Content -Encoding ascii -Path ($zip + ".sha256") -Value "$zipHash  $(Split-Path $zip -Leaf)"

Write-Host "Portable package: $outDir"
Write-Host "Zip: $zip"
Write-Host "Zip SHA256: $zipHash"
