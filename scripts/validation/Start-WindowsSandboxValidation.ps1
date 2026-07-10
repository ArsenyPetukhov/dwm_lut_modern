param(
    [string]$PackageDirectory = ".\dist",
    [string]$EvidenceDirectory = ".\artifacts\validation\sandbox-evidence",
    [switch]$Launch
)

$ErrorActionPreference = "Stop"
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot "..\.."))
$packageRoot = [IO.Path]::GetFullPath($PackageDirectory)
$evidenceRoot = [IO.Path]::GetFullPath($EvidenceDirectory)
$scriptRoot = [IO.Path]::GetFullPath($PSScriptRoot)
$artifactRoot = Join-Path $repo "artifacts\validation"

if (-not (Test-Path -LiteralPath $packageRoot -PathType Container)) {
    throw "Package directory does not exist: $packageRoot"
}
New-Item -ItemType Directory -Force -Path $evidenceRoot, $artifactRoot | Out-Null

function Escape-Xml([string]$value) {
    return [Security.SecurityElement]::Escape($value)
}

$configPath = Join-Path $artifactRoot "DwmLutModern.Validation.wsb"
$configuration = @"
<Configuration>
  <VGpu>Enable</VGpu>
  <Networking>Disable</Networking>
  <ClipboardRedirection>Disable</ClipboardRedirection>
  <PrinterRedirection>Disable</PrinterRedirection>
  <MemoryInMB>6144</MemoryInMB>
  <MappedFolders>
    <MappedFolder>
      <HostFolder>$(Escape-Xml $packageRoot)</HostFolder>
      <SandboxFolder>C:\DwmLutPackage</SandboxFolder>
      <ReadOnly>true</ReadOnly>
    </MappedFolder>
    <MappedFolder>
      <HostFolder>$(Escape-Xml $scriptRoot)</HostFolder>
      <SandboxFolder>C:\DwmLutValidation</SandboxFolder>
      <ReadOnly>true</ReadOnly>
    </MappedFolder>
    <MappedFolder>
      <HostFolder>$(Escape-Xml $evidenceRoot)</HostFolder>
      <SandboxFolder>C:\DwmLutEvidence</SandboxFolder>
      <ReadOnly>false</ReadOnly>
    </MappedFolder>
  </MappedFolders>
  <LogonCommand>
    <Command>powershell.exe -NoProfile -ExecutionPolicy Bypass -File C:\DwmLutValidation\Run-SandboxSmoke.ps1</Command>
  </LogonCommand>
</Configuration>
"@
$configuration | Set-Content -LiteralPath $configPath -Encoding UTF8

# Parse it now so malformed paths/configuration fail before Windows Sandbox is opened.
[xml](Get-Content -Raw -LiteralPath $configPath) | Out-Null
Write-Host "Windows Sandbox configuration written to $configPath"
Write-Host "Evidence will return to $evidenceRoot"

if ($Launch) {
    $feature = Get-WindowsOptionalFeature -Online -FeatureName Containers-DisposableClientVM -ErrorAction SilentlyContinue
    if (-not $feature -or $feature.State -ne "Enabled") {
        throw "Windows Sandbox is not enabled. See docs\validation-lab.md."
    }
    Start-Process -FilePath $configPath
}
