param(
    [string[]]$Builds = @(
        "26100.8737",
        "26200.8737",
        "26220.8754",
        "26300.8758",
        "28000.2340",
        "28020.2366",
        "28120.2374",
        "29617.1000"
    ),
    [string[]]$Architectures = @("amd64", "arm64"),
    [switch]$Offline
)

$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"
$repo = Resolve-Path (Join-Path $PSScriptRoot "..")
$outDir = Join-Path $repo "artifacts\uup"
New-Item -ItemType Directory -Force -Path $outDir | Out-Null

$support = @{
    "26100.8737|amd64" = @{ Package = "x64 fallback"; Profile = "24H2 byte-signature fallback"; Notes = "Exact PDB profile not yet generated for 26100.8737." }
    "26200.8737|amd64" = @{ Package = "x64 compiled"; Profile = "26200.8737_stable"; Notes = "Exact PDB GUID/age profile compiled." }
    "26220.8754|amd64" = @{ Package = "x64 compiled"; Profile = "26220.8754_beta25h2__26300.8758_exp26h2"; Notes = "DWM payload alias with 26300.8758." }
    "26300.8758|amd64" = @{ Package = "x64 compiled"; Profile = "26220.8754_beta25h2__26300.8758_exp26h2"; Notes = "DWM payload alias with 26220.8754." }
    "28000.2340|amd64" = @{ Package = "x64 compiled"; Profile = "28000.2340_published26h1"; Notes = "Distinct PDB identity; same RVAs as 28020/28120 branch." }
    "28020.2366|amd64" = @{ Package = "x64 compiled"; Profile = "28020.2366_beta26h1__28120.2374_exp26h1"; Notes = "Exact PDB GUID/age profile compiled." }
    "28120.2374|amd64" = @{ Package = "x64 compiled"; Profile = "28020.2366_beta26h1__28120.2374_exp26h1"; Notes = "DWM payload alias with 28020.2366." }
    "29617.1000|amd64" = @{ Package = "x64 diagnostic"; Profile = "29617.1000_canary"; Notes = "Canary profile compiled; live VM validation still required." }
}

function Get-OfflineBuilds {
    param([string]$Build)
    $listFile = Join-Path $outDir "$Build.list.json"
    if (!(Test-Path -LiteralPath $listFile)) {
        return @()
    }

    $json = Get-Content -Raw -LiteralPath $listFile | ConvertFrom-Json
    if ($null -eq $json.response.builds) {
        return @()
    }

    return $json.response.builds.PSObject.Properties.Value
}

$rows = New-Object System.Collections.Generic.List[object]

foreach ($build in $Builds) {
    if ($Offline) {
        $entries = @(Get-OfflineBuilds -Build $build)
    } else {
        $url = "https://api.uupdump.net/listid.php?search=$([uri]::EscapeDataString($build))&sortByDate=1"
        $response = Invoke-RestMethod $url
        $response | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $outDir "$build.list.json") -Encoding UTF8
        $entries = @($response.response.builds.PSObject.Properties.Value)
    }

    foreach ($arch in $Architectures) {
        $match = $entries | Where-Object { $_.build -eq $build -and $_.arch -eq $arch } | Select-Object -First 1
        $key = "$build|$arch"
        $state = $support[$key]
        if ($null -eq $state) {
            if ($arch -eq "arm64") {
                $state = @{ Package = "not built"; Profile = "not profiled"; Notes = "Requires ARM64 native DLL and ARM64 DWM PDB/RVA profile." }
            } else {
                $state = @{ Package = "not built"; Profile = "not profiled"; Notes = "Requires payload extraction, PDB profile generation, build, and VM validation." }
            }
        }

        $rows.Add([pscustomobject]@{
            Build = $build
            Architecture = $arch
            UupFound = [bool]$match
            UupUuid = if ($match) { $match.uuid } else { "" }
            Title = if ($match) { $match.title } else { "" }
            Created = if ($match) { $match.created } else { "" }
            PackageStatus = $state.Package
            ProfileStatus = $state.Profile
            Notes = $state.Notes
        })
    }
}

$csv = Join-Path $outDir "build-catalog.generated.csv"
$jsonOut = Join-Path $outDir "build-catalog.generated.json"
$rows | Export-Csv -NoTypeInformation -LiteralPath $csv -Encoding UTF8
$rows | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $jsonOut -Encoding UTF8

Write-Host "Build catalog written:"
Write-Host "  $csv"
Write-Host "  $jsonOut"
