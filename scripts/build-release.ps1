param(
    [string]$Version = "0.9.0"
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$root = Split-Path -Parent $PSScriptRoot
$desktop = Join-Path $root "src\MYWO.Desktop\MYWO.Desktop.csproj"
$updater = Join-Path $root "src\MYWO.Updater\MYWO.Updater.csproj"
$setup = Join-Path $root "src\MYWO.Setup\MYWO.Setup.csproj"
$setupPayload = Join-Path $root "src\MYWO.Setup\Payload\payload.zip"
$dist = Join-Path $root "dist"
$stage = Join-Path $root ".release-stage"
$appStage = Join-Path $stage "installed"
$updaterStage = Join-Path $stage "updater"
$setupStage = Join-Path $stage "setup"
$portableStage = Join-Path $stage "portable"

foreach ($path in @($dist, $stage)) {
    if (Test-Path $path) { Remove-Item $path -Recurse -Force }
    New-Item -ItemType Directory -Path $path | Out-Null
}
New-Item -ItemType Directory -Path $appStage, $updaterStage, $setupStage, $portableStage -Force | Out-Null

Write-Host "[1/9] Restoring MYWO projects..."
dotnet restore $root\MYWO.sln

$common = @(
    "-c", "Release",
    "-r", "win-x64",
    "--self-contained", "true",
    "-p:Version=$Version",
    "-p:FileVersion=$Version.0",
    "-p:AssemblyVersion=$Version.0",
    "-p:PublishSingleFile=true",
    "-p:IncludeNativeLibrariesForSelfExtract=true",
    "-p:EnableCompressionInSingleFile=true",
    "-p:DebugType=None",
    "-p:DebugSymbols=false"
)

Write-Host "[2/9] Building solution..."
dotnet build $root\MYWO.sln -c Release --no-restore -p:Version=$Version -p:FileVersion=$Version.0 -p:AssemblyVersion=$Version.0

Write-Host "[3/9] Publishing MYWO-Update.exe..."
dotnet publish $updater @common --no-restore -p:AssemblyName=MYWO-Update -o $updaterStage
$updaterExe = Join-Path $updaterStage "MYWO-Update.exe"
if (-not (Test-Path $updaterExe)) { throw "MYWO-Update.exe was not produced." }
Copy-Item $updaterExe (Join-Path $dist "MYWO-Update.exe") -Force

Write-Host "[4/9] Publishing installed MYWO.exe payload..."
dotnet publish $desktop @common --no-restore -p:AssemblyName=MYWO -o $appStage
$appExe = Join-Path $appStage "MYWO.exe"
if (-not (Test-Path $appExe)) { throw "MYWO.exe was not produced." }
Copy-Item $updaterExe (Join-Path $appStage "MYWO-Update.exe") -Force

$payloadName = "MYWO-Update-Payload-$Version.zip"
$payload = Join-Path $dist $payloadName
Compress-Archive -Path (Join-Path $appStage "*") -DestinationPath $payload -CompressionLevel Optimal -Force
$payloadHash = (Get-FileHash $payload -Algorithm SHA256).Hash.ToLowerInvariant()

$manifest = [ordered]@{
    version = $Version
    url = "https://github.com/bren-wp/MYWO/releases/download/v$Version/$payloadName"
    sha256 = $payloadHash
} | ConvertTo-Json
Set-Content -Path (Join-Path $dist "update.json") -Value $manifest -Encoding UTF8

Write-Host "[5/9] Building embedded MYWO-Setup.exe..."
Copy-Item $payload $setupPayload -Force
dotnet publish $setup @common --no-restore -p:AssemblyName=MYWO-Setup -o $setupStage
$setupExe = Join-Path $setupStage "MYWO-Setup.exe"
if (-not (Test-Path $setupExe)) { throw "MYWO-Setup.exe was not produced." }
Copy-Item $setupExe (Join-Path $dist "MYWO-Setup.exe") -Force

Write-Host "[6/9] Publishing one-file portable build..."
dotnet publish $desktop @common --no-restore -p:AssemblyName=MYWO-Portable -o $portableStage
$portableExe = Join-Path $portableStage "MYWO-Portable.exe"
if (-not (Test-Path $portableExe)) { throw "MYWO-Portable.exe was not produced." }
Copy-Item $portableExe (Join-Path $dist "MYWO-Portable.exe") -Force

Write-Host "[7/9] Creating diagnostic and source packages..."
Compress-Archive -Path (Join-Path $appStage "*") -DestinationPath (Join-Path $dist "MYWO-Installed-Files-$Version.zip") -CompressionLevel Optimal -Force

$sourceStage = Join-Path $stage "source"
New-Item -ItemType Directory -Path $sourceStage -Force | Out-Null
$sourceItems = @("docs", ".github", "scripts", "src", "MYWO.sln", "README.md", "CHANGELOG.md", ".gitignore", "BUILD-WINDOWS.cmd")
foreach ($item in $sourceItems) {
    $from = Join-Path $root $item
    if (Test-Path $from) { Copy-Item $from $sourceStage -Recurse -Force }
}
$embeddedPayload = Join-Path $sourceStage "src\MYWO.Setup\Payload\payload.zip"
if (Test-Path $embeddedPayload) { Remove-Item $embeddedPayload -Force }
New-Item -ItemType File -Path $embeddedPayload -Force | Out-Null
Compress-Archive -Path (Join-Path $sourceStage "*") -DestinationPath (Join-Path $dist "MYWO-$Version-source.zip") -CompressionLevel Optimal -Force

Write-Host "[8/9] Writing release checksums..."
$releaseFiles = Get-ChildItem $dist -File | Sort-Object Name
$lines = foreach ($file in $releaseFiles) {
    $hash = (Get-FileHash $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
    "$hash  $($file.Name)"
}
Set-Content -Path (Join-Path $dist "SHA256SUMS.txt") -Value $lines -Encoding ASCII

Write-Host "[9/9] Validating requested release artifacts..."
foreach ($required in @("MYWO-Setup.exe", "MYWO-Portable.exe", "MYWO-Update.exe", "update.json", "SHA256SUMS.txt", "MYWO-$Version-source.zip")) {
    $path = Join-Path $dist $required
    if (-not (Test-Path $path)) { throw "Missing release artifact: $required" }
    if ((Get-Item $path).Length -eq 0) { throw "Empty release artifact: $required" }
    Write-Host "  OK  $required -> $((Get-Item $path).Length) bytes"
}

Write-Host ""
Write-Host "MYWO $Version release is ready: $dist"
Write-Host "Uninstall is integrated through MYWO-Update.exe --uninstall; no standalone uninstall.exe/unins*.exe is shipped."
