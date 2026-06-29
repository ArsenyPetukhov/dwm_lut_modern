param(
    [string]$ReferenceRoot,
    [switch]$IncludeForks
)

$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"

$repo = Resolve-Path (Join-Path $PSScriptRoot "..")
if ([string]::IsNullOrWhiteSpace($ReferenceRoot)) {
    $workspaceRoot = Split-Path -Parent $repo
    $ReferenceRoot = Join-Path $workspaceRoot "references\legacy-github"
}

$researchDir = Join-Path $repo "artifacts\research"
New-Item -ItemType Directory -Force -Path $ReferenceRoot | Out-Null
New-Item -ItemType Directory -Force -Path $researchDir | Out-Null

$seedRepos = @(
    "https://github.com/ledoge/dwm_lut.git",
    "https://github.com/lauralex/dwm_lut.git",
    "https://github.com/ed1ii/dwm_lut_fixed.git",
    "https://github.com/0x401gg/dwm_lut-windows-25h2.git",
    "https://github.com/hadoooooouken/dwm_lut_fps_boost.git",
    "https://github.com/RobWilton3000/DwmLUTGUI-copy.git"
)

function Get-SafeRepoName {
    param([string]$Url)
    $withoutGit = $Url -replace "\.git$", ""
    $parts = $withoutGit -split "/"
    if ($parts.Length -ge 2) {
        $owner = $parts[$parts.Length - 2]
        $repoName = $parts[$parts.Length - 1]
        return ("{0}_{1}" -f $owner, $repoName) -replace '[\\/:*?"<>|]', "_"
    }
    return (($withoutGit -split "/")[-1]) -replace '[\\/:*?"<>|]', "_"
}

function Sync-Repo {
    param(
        [string]$Url,
        [string]$Root
    )

    $name = Get-SafeRepoName -Url $Url
    $dest = Join-Path $Root $name
    if (Test-Path -LiteralPath (Join-Path $dest ".git")) {
        Write-Host "Updating $Url"
        git -C $dest fetch --all --tags --prune | Out-Host
    } else {
        Write-Host "Cloning $Url"
        git clone --recurse-submodules $Url $dest | Out-Host
    }
    return $dest
}

$repoUrls = New-Object System.Collections.Generic.List[string]
foreach ($url in $seedRepos) {
    $repoUrls.Add($url)
}

if ($IncludeForks) {
    try {
        $forkUrls = gh api repos/ledoge/dwm_lut/forks --paginate --jq '.[].clone_url'
        foreach ($url in $forkUrls) {
            if (-not [string]::IsNullOrWhiteSpace($url) -and -not $repoUrls.Contains($url)) {
                $repoUrls.Add($url)
            }
        }
    } catch {
        Write-Warning "Could not query GitHub forks through gh; continuing with seed repositories only. $($_.Exception.Message)"
    }
}

$syncedRepos = New-Object System.Collections.Generic.List[string]
foreach ($url in $repoUrls) {
    $root = $ReferenceRoot
    if ($IncludeForks -and $seedRepos -notcontains $url) {
        $root = Join-Path $ReferenceRoot "forks"
        New-Item -ItemType Directory -Force -Path $root | Out-Null
    }
    $syncedRepos.Add((Sync-Repo -Url $url -Root $root))
}

$repoRows = foreach ($path in $syncedRepos) {
    $remote = (git -C $path remote get-url origin) -join ""
    $head = (git -C $path rev-parse HEAD) -join ""
    $branches = ((git -C $path branch -r --format "%(refname:short)") | Where-Object { $_ -notmatch "/HEAD$" }) -join ";"
    $tags = (git -C $path tag --sort=v:refname) -join ";"
    [pscustomobject]@{
        Name = Split-Path -Leaf $path
        Remote = $remote
        Head = $head
        Branches = $branches
        Tags = $tags
        ReferencePath = $path.Substring($ReferenceRoot.Length + 1)
    }
}

$engineRows = foreach ($path in $syncedRepos) {
    Get-ChildItem -LiteralPath $path -Recurse -File |
        Where-Object { $_.FullName -notmatch "\\.git\\" -and ($_.Name -eq "dwm_lut.c" -or $_.Name -eq "dllmain.cpp") } |
        ForEach-Object {
            $hash = Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256
            [pscustomobject]@{
                Repo = Split-Path -Leaf $path
                Hash = $hash.Hash
                HashPrefix = $hash.Hash.Substring(0, 12)
                Bytes = $_.Length
                RelativePath = $_.FullName.Substring($path.Length + 1)
                ReferencePath = $_.FullName.Substring($ReferenceRoot.Length + 1)
            }
        }
}

$repoCsv = Join-Path $researchDir "legacy-repos.generated.csv"
$engineCsv = Join-Path $researchDir "legacy-native-engines.generated.csv"
$repoRows | Sort-Object Name | Export-Csv -NoTypeInformation -LiteralPath $repoCsv -Encoding UTF8
$engineRows | Sort-Object HashPrefix, Repo, RelativePath | Export-Csv -NoTypeInformation -LiteralPath $engineCsv -Encoding UTF8

Write-Host "Legacy reference update complete:"
Write-Host "  Repos: $repoCsv"
Write-Host "  Native engines: $engineCsv"
Write-Host "  Reference root: $ReferenceRoot"
