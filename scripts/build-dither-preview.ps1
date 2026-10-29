param(
    [ValidatePattern('^\d+\.\d+\.\d+$')]
    [string]$Version = "5.0.5",
    [ValidatePattern('^[a-z0-9-]+$')]
    [string]$PreviewSuffix = "dithering-preview"
)

$ErrorActionPreference = "Stop"
$repo = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$out = Join-Path $repo "dist\sdr-dither-preview"
$payload = Join-Path $out "portable"
New-Item -ItemType Directory -Force -Path $out, $payload | Out-Null

# Compile every entry point with the same SM5 compiler used by the injected DLL.
# Shader compilation is a build gate; no DWM injection or test suite runs here.
$nativeSource = Join-Path $repo "src\lutdwm\dllmain.cpp"
$source = Get-Content -Raw -LiteralPath $nativeSource
$match = [regex]::Match($source, 'char shaders\[\] = R"\(([\s\S]*?)\)";')
if (-not $match.Success) { throw "Embedded HLSL source was not found" }
$hlsl = Join-Path $out "lut-shaders.hlsl"
Set-Content -LiteralPath $hlsl -Value $match.Groups[1].Value -Encoding ASCII
$sdk = Join-Path ${env:ProgramFiles(x86)} "Windows Kits\10\bin"
$fxc = Get-ChildItem $sdk -Filter fxc.exe -Recurse |
    Where-Object { $_.FullName -match '\\x64\\fxc.exe$' } |
    Sort-Object FullName -Descending | Select-Object -First 1
if (-not $fxc) { throw "Windows SDK fxc.exe was not found" }
foreach ($shader in @(@("VS", "vs_5_0"), @("PS", "ps_5_0"), @("PSCopy", "ps_5_0"), @("CSErrorDiffusion", "cs_5_0"))) {
    $defines = if ($shader[0] -eq "CSErrorDiffusion") { @("/D", "DITHER_COMPUTE=1") } else { @() }
    & $fxc.FullName /nologo /O3 /T $shader[1] /E $shader[0] @defines /Fo (Join-Path $out ($shader[0] + ".cso")) $hlsl
    if ($LASTEXITCODE -ne 0) { throw "Shader compilation failed: $($shader[0])" }
}

& (Join-Path $PSScriptRoot "generate-dwm-profile-header.ps1")
$nativeOut = Join-Path $repo "x64\Release"
$nativeObj = Join-Path $repo "x64\obj-dither-preview"
& msbuild (Join-Path $repo "src\lutdwm\lutdwm.vcxproj") /m /t:Build /p:Configuration=Release /p:Platform=x64 `
    "/p:OutDir=$nativeOut/" "/p:IntDir=$nativeObj/" /p:VcpkgTriplet=x64-windows-static
if ($LASTEXITCODE -ne 0) { throw "Native DLL compilation failed" }

$project = Join-Path $repo "src\DwmLutModern.App\DwmLutModern.App.csproj"
& dotnet publish $project -c Release -r win-x64 -p:Platform=x64 "-p:Version=$Version" `
    -p:IncludeSourceRevisionInInformationalVersion=false --self-contained true -o $payload
if ($LASTEXITCODE -ne 0) { throw "Portable GUI compilation failed" }
Copy-Item (Join-Path $nativeOut "dwm_lut.dll") $payload -Force
Copy-Item (Join-Path $nativeOut "dwm_lut.pdb") $out -Force
Copy-Item (Join-Path $nativeOut "dwm_lut.dll") $out -Force
Copy-Item (Join-Path $repo "LICENSE") (Join-Path $payload "LICENSE.txt")
Copy-Item (Join-Path $repo "NOTICE.md") $payload
Copy-Item (Join-Path $repo "docs\sdr-dithering.md") (Join-Path $payload "DITHERING.md")

$revision = (& git -C $repo rev-parse HEAD).Trim()
$receipt = [ordered]@{
    version = "v$Version-$PreviewSuffix"
    source_revision = $revision
    built_at_utc = [DateTimeOffset]::UtcNow.ToString("O")
    native_compiler = "MSVC v143, Windows x64"
    shader_compiler = $fxc.FullName
    managed_runtime = ".NET 8, self-contained win-x64"
    native_dll_sha256 = (Get-FileHash (Join-Path $payload "dwm_lut.dll") -Algorithm SHA256).Hash
    app_dll_sha256 = (Get-FileHash (Join-Path $payload "DwmLutModern.dll") -Algorithm SHA256).Hash
    scope = "Compiled native, HLSL and WPF app; settings rendered separately. DWM playback and GPU timings are not measured."
}
$receipt | ConvertTo-Json | Set-Content (Join-Path $payload "BUILD.json") -Encoding UTF8
$receipt | ConvertTo-Json | Set-Content (Join-Path $out "BUILD.json") -Encoding UTF8
$archive = Join-Path $out "DwmLutModern-v$Version-$PreviewSuffix-x64-portable.zip"
Compress-Archive -Path (Join-Path $payload "*") -DestinationPath $archive -Force
$sum = (Get-FileHash $archive -Algorithm SHA256).Hash
Set-Content ($archive + ".sha256") "$sum  $([IO.Path]::GetFileName($archive))" -Encoding ASCII
Write-Host "Compiled portable preview: $archive"
